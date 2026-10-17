using CMRP.Models;
using CMRP.Services;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Tests;

/// <summary>
/// Integration tests for status changes, assignment, priority and notes (FR-05 to FR-14, US-04 to US-08).
/// Every test checks both the result AND what was (or was not) written to the database.
/// </summary>
public sealed class WorkflowServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private ApplicationUser _reporter = null!;
    private ApplicationUser _tech = null!;
    private ApplicationUser _tech2 = null!;
    private ApplicationUser _coordinator = null!;
    private ApplicationUser _admin = null!;

    public WorkflowServiceTests()
    {
        _reporter = _t.AddUserAsync("r@cmrp.test", Roles.Reporter, "Reporter").GetAwaiter().GetResult();
        _tech = _t.AddUserAsync("t@cmrp.test", Roles.Technician, "Tech One").GetAwaiter().GetResult();
        _tech2 = _t.AddUserAsync("t2@cmrp.test", Roles.Technician, "Tech Two").GetAwaiter().GetResult();
        _coordinator = _t.AddUserAsync("c@cmrp.test", Roles.Coordinator, "Coordinator").GetAwaiter().GetResult();
        _admin = _t.AddUserAsync("a@cmrp.test", Roles.Administrator, "Admin").GetAwaiter().GetResult();
    }

    public void Dispose() => _t.Dispose();

    private System.Security.Claims.ClaimsPrincipal Coordinator() => TestUser.Principal(_coordinator.Id, Roles.Coordinator);
    private System.Security.Claims.ClaimsPrincipal Admin() => TestUser.Principal(_admin.Id, Roles.Administrator);
    private System.Security.Claims.ClaimsPrincipal Tech() => TestUser.Principal(_tech.Id, Roles.Technician);
    private System.Security.Claims.ClaimsPrincipal Tech2() => TestUser.Principal(_tech2.Id, Roles.Technician);
    private System.Security.Claims.ClaimsPrincipal Reporter() => TestUser.Principal(_reporter.Id, Roles.Reporter);

    private async Task<MaintenanceReport> Reload(int id) =>
        await _t.Db.Reports.AsNoTracking().SingleAsync(r => r.Id == id);

    // ================= ChangeStatusAsync =================

    [Fact]
    public async Task ChangeStatus_ManagerMovesSubmittedToUnderReview()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.UnderReview, null, Coordinator());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ReportStatus.UnderReview, (await Reload(report.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_WritesAHistoryEntry()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.UnderReview, null, Coordinator());

        var history = await _t.Db.StatusHistory.SingleAsync(h => h.ReportId == report.Id);
        Assert.Equal(ReportStatus.Submitted, history.FromStatus);
        Assert.Equal(ReportStatus.UnderReview, history.ToStatus);
        Assert.Equal(_coordinator.Id, history.ChangedById);
    }

    [Fact]
    public async Task ChangeStatus_UpdatesTheUpdatedTimestamp()
    {
        var report = await _t.AddReportAsync(_reporter.Id, createdAt: DateTime.UtcNow.AddDays(-2));
        var before = report.UpdatedAt;

        await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.UnderReview, null, Coordinator());

        Assert.True((await Reload(report.Id)).UpdatedAt > before);
    }

    [Fact]
    public async Task ChangeStatus_AssignedTechnicianCanStartAndResolve()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.Assigned, assignedTo: _tech.Id);

        var start = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, null, Tech());
        var resolve = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Resolved, "Pipe replaced", Tech());

        Assert.True(start.Succeeded, start.Error);
        Assert.True(resolve.Succeeded, resolve.Error);
        var saved = await Reload(report.Id);
        Assert.Equal(ReportStatus.Resolved, saved.Status);
        Assert.NotNull(saved.ResolvedAt);
    }

    [Fact]
    public async Task ChangeStatus_ResolvingSetsResolvedAt()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.InProgress, assignedTo: _tech.Id);

        await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Resolved, null, Tech());

        var saved = await Reload(report.Id);
        Assert.InRange(saved.ResolvedAt!.Value, DateTime.UtcNow.AddSeconds(-10), DateTime.UtcNow.AddSeconds(10));
    }

    [Fact]
    public async Task ChangeStatus_ReopeningAResolvedReportClearsResolvedAt()
    {
        var report = await _t.AddReportAsync(
            _reporter.Id, ReportStatus.Resolved, assignedTo: _tech.Id, resolvedAt: DateTime.UtcNow.AddHours(-1));

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, "Leak is back", Coordinator());

        Assert.True(result.Succeeded, result.Error);
        var saved = await Reload(report.Id);
        Assert.Equal(ReportStatus.InProgress, saved.Status);
        Assert.Null(saved.ResolvedAt);
    }

    [Fact]
    public async Task ChangeStatus_ManagerCanCloseAResolvedReport()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.Resolved, resolvedAt: DateTime.UtcNow);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Closed, null, Admin());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ReportStatus.Closed, (await Reload(report.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_TechnicianCannotCloseAReport()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.Resolved, assignedTo: _tech.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Closed, null, Tech());

        Assert.False(result.Succeeded);
        Assert.Equal(ReportStatus.Resolved, (await Reload(report.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_AnotherTechnicianIsRefused()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.Assigned, assignedTo: _tech.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, null, Tech2());

        Assert.False(result.Succeeded);
        Assert.Equal(ReportStatus.Assigned, (await Reload(report.Id)).Status);
        Assert.Empty(_t.Db.StatusHistory);
    }

    [Fact]
    public async Task ChangeStatus_ReporterCannotChangeStatus()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Cancelled, "Changed my mind", Reporter());

        Assert.False(result.Succeeded);
        Assert.Equal(ReportStatus.Submitted, (await Reload(report.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransitionIsRefusedWithAClearMessage()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Closed, null, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("You cannot change a report from Submitted to Closed.", result.Error);
        Assert.Equal(ReportStatus.Submitted, (await Reload(report.Id)).Status);
        Assert.Empty(_t.Db.StatusHistory);
    }

    [Fact]
    public async Task ChangeStatus_AClosedReportCannotBeReopened()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.Closed);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, null, Admin());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChangeStatus_CancellingNeedsANote()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Cancelled, "   ", Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("Please add a note explaining why the report is being cancelled.", result.Error);
        Assert.Equal(ReportStatus.Submitted, (await Reload(report.Id)).Status);
    }

    [Fact]
    public async Task ChangeStatus_CancellingWithANoteSavesAPublicNote()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Cancelled, "  Duplicate of CMR-1  ", Coordinator());

        Assert.True(result.Succeeded, result.Error);
        var note = await _t.Db.Notes.SingleAsync();
        Assert.Equal("Duplicate of CMR-1", note.Text);
        Assert.False(note.IsInternal);
        Assert.Equal(_coordinator.Id, note.AuthorId);
    }

    [Fact]
    public async Task ChangeStatus_WithoutANoteCreatesNoNote()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.UnderReview, null, Coordinator());

        Assert.Empty(_t.Db.Notes);
    }

    [Fact]
    public async Task ChangeStatus_UnknownReportIsReportedGracefully()
    {
        var result = await _t.Workflow.ChangeStatusAsync(9999, ReportStatus.UnderReview, null, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("That report no longer exists.", result.Error);
    }

    [Fact]
    public async Task ChangeStatus_AnonymousUserIsAskedToSignInAgain()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.UnderReview, null, TestUser.Anonymous());

        Assert.False(result.Succeeded);
        Assert.Equal("You need to sign in again.", result.Error);
    }

    // ================= AssignAsync =================

    [Fact]
    public async Task Assign_NewReportBecomesAssigned()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        Assert.True(result.Succeeded, result.Error);
        var saved = await Reload(report.Id);
        Assert.Equal(_tech.Id, saved.AssignedToUserId);
        Assert.Equal(ReportStatus.Assigned, saved.Status);
    }

    [Fact]
    public async Task Assign_RecordsWhoAssignedWhomAndWhen()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        var entry = await _t.Db.AssignmentHistory.SingleAsync();
        Assert.Null(entry.FromUserId);
        Assert.Equal(_tech.Id, entry.ToUserId);
        Assert.Equal(_coordinator.Id, entry.ChangedById);
    }

    [Fact]
    public async Task Assign_AlsoWritesTheStatusHistoryEntry()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.UnderReview);

        await _t.Workflow.AssignAsync(report.Id, _tech.Id, Admin());

        var history = await _t.Db.StatusHistory.SingleAsync();
        Assert.Equal(ReportStatus.UnderReview, history.FromStatus);
        Assert.Equal(ReportStatus.Assigned, history.ToStatus);
    }

    [Fact]
    public async Task Assign_ReassigningAnInProgressReportKeepsItsStatus()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.InProgress, assignedTo: _tech.Id);

        var result = await _t.Workflow.AssignAsync(report.Id, _tech2.Id, Coordinator());

        Assert.True(result.Succeeded, result.Error);
        var saved = await Reload(report.Id);
        Assert.Equal(_tech2.Id, saved.AssignedToUserId);
        Assert.Equal(ReportStatus.InProgress, saved.Status);
        var entry = await _t.Db.AssignmentHistory.SingleAsync();
        Assert.Equal(_tech.Id, entry.FromUserId);
        Assert.Equal(_tech2.Id, entry.ToUserId);
    }

    [Fact]
    public async Task Assign_SameTechnicianTwiceIsRefused()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.Assigned, assignedTo: _tech.Id);

        var result = await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("This report is already assigned to Tech One.", result.Error);
        Assert.Empty(_t.Db.AssignmentHistory);
    }

    [Theory]
    [InlineData(ReportStatus.Resolved)]
    [InlineData(ReportStatus.Closed)]
    [InlineData(ReportStatus.Cancelled)]
    public async Task Assign_FinishedReportsCannotBeAssigned(ReportStatus status)
    {
        var report = await _t.AddReportAsync(_reporter.Id, status);

        var result = await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Null((await Reload(report.Id)).AssignedToUserId);
    }

    [Fact]
    public async Task Assign_OnlyManagersMayAssign()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var byTech = await _t.Workflow.AssignAsync(report.Id, _tech2.Id, Tech());
        var byReporter = await _t.Workflow.AssignAsync(report.Id, _tech.Id, Reporter());

        Assert.False(byTech.Succeeded);
        Assert.False(byReporter.Succeeded);
        Assert.Equal("Only a coordinator can assign reports.", byTech.Error);
        Assert.Null((await Reload(report.Id)).AssignedToUserId);
    }

    [Fact]
    public async Task Assign_CannotAssignToANonTechnician()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var toReporter = await _t.Workflow.AssignAsync(report.Id, _reporter.Id, Coordinator());
        var toCoordinator = await _t.Workflow.AssignAsync(report.Id, _coordinator.Id, Coordinator());
        var toNobody = await _t.Workflow.AssignAsync(report.Id, "no-such-user", Coordinator());

        Assert.False(toReporter.Succeeded);
        Assert.False(toCoordinator.Succeeded);
        Assert.False(toNobody.Succeeded);
        Assert.Equal("Please choose a technician.", toNobody.Error);
        Assert.Null((await Reload(report.Id)).AssignedToUserId);
    }

    [Fact]
    public async Task Assign_UnknownReportIsReportedGracefully()
    {
        var result = await _t.Workflow.AssignAsync(9999, _tech.Id, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("That report no longer exists.", result.Error);
    }

    [Fact]
    public async Task Assign_AfterAssignmentTheTechnicianCanWorkTheReport()
    {
        var report = await _t.AddReportAsync(_reporter.Id);
        await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        var start = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, null, Tech());

        Assert.True(start.Succeeded, start.Error);
    }

    // ---- Regression tests for defects found during testing (see the defect register in the final report) ----

    [Fact]
    public async Task Assign_DeactivatedTechnicianIsRefused_DEF02()
    {
        var report = await _t.AddReportAsync(_reporter.Id);
        await _t.Users.SetLockoutEnabledAsync(_tech, true);
        await _t.Users.SetLockoutEndDateAsync(_tech, DateTimeOffset.MaxValue); // how the admin screen deactivates an account

        var result = await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("Tech One's account is deactivated. Please choose another technician.", result.Error);
        Assert.Null((await Reload(report.Id)).AssignedToUserId);
        Assert.Empty(_t.Db.AssignmentHistory);
    }

    [Fact]
    public async Task Assign_ReactivatedTechnicianCanBeAssignedAgain_DEF02()
    {
        var report = await _t.AddReportAsync(_reporter.Id);
        await _t.Users.SetLockoutEnabledAsync(_tech, true);
        await _t.Users.SetLockoutEndDateAsync(_tech, DateTimeOffset.MaxValue);
        await _t.Users.SetLockoutEndDateAsync(_tech, null); // reactivated

        var result = await _t.Workflow.AssignAsync(report.Id, _tech.Id, Coordinator());

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task ChangeStatus_NoteOverOneThousandCharactersIsRefused_DEF01()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.InProgress, assignedTo: _tech.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Resolved, new string('x', 1001), Tech());

        Assert.False(result.Succeeded);
        Assert.Equal("Notes can be at most 1000 characters.", result.Error);
        Assert.Equal(ReportStatus.InProgress, (await Reload(report.Id)).Status);
        Assert.Empty(_t.Db.Notes);
    }

    [Fact]
    public async Task ChangeStatus_NoteOfExactlyOneThousandCharactersIsAccepted_DEF01()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.InProgress, assignedTo: _tech.Id);

        var result = await _t.Workflow.ChangeStatusAsync(report.Id, ReportStatus.Resolved, new string('x', 1000), Tech());

        Assert.True(result.Succeeded, result.Error);
    }

    // ================= SetPriorityAsync =================

    [Fact]
    public async Task Priority_ManagerCanChangeIt()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.SetPriorityAsync(report.Id, ReportPriority.Critical, Coordinator());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ReportPriority.Critical, (await Reload(report.Id)).Priority);
    }

    [Fact]
    public async Task Priority_ChangeIsRecordedAsAnInternalNote()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        await _t.Workflow.SetPriorityAsync(report.Id, ReportPriority.High, Coordinator());

        var note = await _t.Db.Notes.SingleAsync();
        Assert.True(note.IsInternal);
        Assert.Equal("Priority changed from Normal to High.", note.Text);
        Assert.Equal(_coordinator.Id, note.AuthorId);
    }

    [Fact]
    public async Task Priority_SettingTheSameValueDoesNothing()
    {
        var report = await _t.AddReportAsync(_reporter.Id, priority: ReportPriority.High);

        var result = await _t.Workflow.SetPriorityAsync(report.Id, ReportPriority.High, Coordinator());

        Assert.True(result.Succeeded);
        Assert.Empty(_t.Db.Notes);
    }

    [Fact]
    public async Task Priority_TechniciansAndReportersCannotChangeIt()
    {
        var report = await _t.AddReportAsync(_reporter.Id, assignedTo: _tech.Id);

        var byTech = await _t.Workflow.SetPriorityAsync(report.Id, ReportPriority.Critical, Tech());
        var byReporter = await _t.Workflow.SetPriorityAsync(report.Id, ReportPriority.Critical, Reporter());

        Assert.False(byTech.Succeeded);
        Assert.False(byReporter.Succeeded);
        Assert.Equal("Only a coordinator can change priority.", byTech.Error);
        Assert.Equal(ReportPriority.Normal, (await Reload(report.Id)).Priority);
    }

    [Fact]
    public async Task Priority_RejectsAValueOutsideTheEnum()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.SetPriorityAsync(report.Id, (ReportPriority)99, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("Please choose a valid priority.", result.Error);
        Assert.Equal(ReportPriority.Normal, (await Reload(report.Id)).Priority);
    }

    [Fact]
    public async Task Priority_UnknownReportIsReportedGracefully()
    {
        var result = await _t.Workflow.SetPriorityAsync(9999, ReportPriority.High, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("That report no longer exists.", result.Error);
    }

    // ================= AddNoteAsync =================

    [Fact]
    public async Task Note_ManagerCanAddAnInternalNote()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, "Call the contractor", true, Coordinator());

        Assert.True(result.Succeeded, result.Error);
        var note = await _t.Db.Notes.SingleAsync();
        Assert.True(note.IsInternal);
        Assert.Equal("Call the contractor", note.Text);
    }

    [Fact]
    public async Task Note_AssignedTechnicianCanAddAPublicNote()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.InProgress, assignedTo: _tech.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, "Waiting for parts", false, Tech());

        Assert.True(result.Succeeded, result.Error);
        Assert.False((await _t.Db.Notes.SingleAsync()).IsInternal);
    }

    [Fact]
    public async Task Note_TextIsTrimmed()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        await _t.Workflow.AddNoteAsync(report.Id, "   hello   ", false, Coordinator());

        Assert.Equal("hello", (await _t.Db.Notes.SingleAsync()).Text);
    }

    [Fact]
    public async Task Note_AddingOneUpdatesTheReport()
    {
        var report = await _t.AddReportAsync(_reporter.Id, createdAt: DateTime.UtcNow.AddDays(-3));
        var before = report.UpdatedAt;

        await _t.Workflow.AddNoteAsync(report.Id, "hello", false, Coordinator());

        Assert.True((await Reload(report.Id)).UpdatedAt > before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Note_EmptyTextIsRefused(string text)
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, text, false, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("Please write a note before saving.", result.Error);
        Assert.Empty(_t.Db.Notes);
    }

    [Fact]
    public async Task Note_OverOneThousandCharactersIsRefused()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, new string('x', 1001), false, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("Notes can be at most 1000 characters.", result.Error);
    }

    [Fact]
    public async Task Note_ExactlyOneThousandCharactersIsAccepted()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, new string('x', 1000), false, Coordinator());

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task Note_ReporterCannotAddNotes()
    {
        var report = await _t.AddReportAsync(_reporter.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, "Please hurry", false, Reporter());

        Assert.False(result.Succeeded);
        Assert.Equal("You can only add notes to reports assigned to you.", result.Error);
        Assert.Empty(_t.Db.Notes);
    }

    [Fact]
    public async Task Note_TechnicianCannotNoteAnotherTechniciansReport()
    {
        var report = await _t.AddReportAsync(_reporter.Id, ReportStatus.InProgress, assignedTo: _tech.Id);

        var result = await _t.Workflow.AddNoteAsync(report.Id, "Not mine", false, Tech2());

        Assert.False(result.Succeeded);
        Assert.Empty(_t.Db.Notes);
    }

    [Fact]
    public async Task Note_UnknownReportIsReportedGracefully()
    {
        var result = await _t.Workflow.AddNoteAsync(9999, "hello", false, Coordinator());

        Assert.False(result.Succeeded);
        Assert.Equal("That report no longer exists.", result.Error);
    }

    // ================= Whole life cycle =================

    [Fact]
    public async Task FullLifecycle_SubmitAssignWorkResolveClose_LeavesACompleteAuditTrail()
    {
        var created = await _t.Reports.CreateAsync(_reporter.Id, new ViewModels.CreateReportViewModel
        {
            CategoryId = _t.CategoryId,
            LocationId = _t.LocationId,
            Description = "Water is leaking."
        });
        Assert.True(created.Succeeded, created.Error);
        var id = created.ReportId;

        Assert.True((await _t.Workflow.SetPriorityAsync(id, ReportPriority.High, Coordinator())).Succeeded);
        Assert.True((await _t.Workflow.AssignAsync(id, _tech.Id, Coordinator())).Succeeded);
        Assert.True((await _t.Workflow.ChangeStatusAsync(id, ReportStatus.InProgress, null, Tech())).Succeeded);
        Assert.True((await _t.Workflow.ChangeStatusAsync(id, ReportStatus.Resolved, "Fixed the pipe", Tech())).Succeeded);
        Assert.True((await _t.Workflow.ChangeStatusAsync(id, ReportStatus.Closed, null, Coordinator())).Succeeded);

        var details = await _t.Reports.GetDetailsAsync(id);
        Assert.Equal(ReportStatus.Closed, details!.Status);
        Assert.Equal(
            new[] { ReportStatus.Submitted, ReportStatus.Assigned, ReportStatus.InProgress, ReportStatus.Resolved, ReportStatus.Closed },
            details.StatusHistory.OrderBy(h => h.Id).Select(h => h.ToStatus));
        Assert.Equal(2, details.Notes.Count); // internal priority note + public resolution note
        Assert.NotNull(details.ResolvedAt);
    }
}
