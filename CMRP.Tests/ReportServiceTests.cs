using System.Text.RegularExpressions;
using CMRP.Models;
using CMRP.Services;
using CMRP.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Tests;

/// <summary>Integration tests: the real ReportService against a SQLite in-memory database (FR-02, FR-03, US-01).</summary>
public sealed class ReportServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private CreateReportViewModel Input(string description = "The ceiling is leaking.") => new()
    {
        CategoryId = _t.CategoryId,
        LocationId = _t.LocationId,
        Description = description
    };

    // ---- CreateAsync ----

    [Fact]
    public async Task Create_SavesTheReportAndReturnsAReference()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        var result = await _t.Reports.CreateAsync(reporter.Id, Input());

        Assert.True(result.Succeeded, result.Error);
        Assert.Matches(new Regex(@"^CMR-\d{4}-\d{4}$"), result.ReferenceNumber);
        var saved = await _t.Db.Reports.SingleAsync();
        Assert.Equal(result.ReportId, saved.Id);
        Assert.Equal(result.ReferenceNumber, saved.ReferenceNumber);
    }

    [Fact]
    public async Task Create_StartsAsSubmittedWithNormalPriority()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        await _t.Reports.CreateAsync(reporter.Id, Input());

        var saved = await _t.Db.Reports.SingleAsync();
        Assert.Equal(ReportStatus.Submitted, saved.Status);
        Assert.Equal(ReportPriority.Normal, saved.Priority);
        Assert.Equal(reporter.Id, saved.ReporterId);
        Assert.Null(saved.AssignedToUserId);
        Assert.Null(saved.ResolvedAt);
    }

    [Fact]
    public async Task Create_ReferenceUsesTheCurrentYearAndPaddedId()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        var result = await _t.Reports.CreateAsync(reporter.Id, Input());

        Assert.Equal($"CMR-{DateTime.UtcNow.Year}-{result.ReportId:0000}", result.ReferenceNumber);
    }

    [Fact]
    public async Task Create_EveryReportGetsADifferentReference()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        var first = await _t.Reports.CreateAsync(reporter.Id, Input());
        var second = await _t.Reports.CreateAsync(reporter.Id, Input());

        Assert.NotEqual(first.ReferenceNumber, second.ReferenceNumber);
        Assert.NotEqual(first.ReportId, second.ReportId);
    }

    [Fact]
    public async Task Create_WritesTheFirstStatusHistoryEntry()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        var result = await _t.Reports.CreateAsync(reporter.Id, Input());

        var history = await _t.Db.StatusHistory.SingleAsync(h => h.ReportId == result.ReportId);
        Assert.Null(history.FromStatus);
        Assert.Equal(ReportStatus.Submitted, history.ToStatus);
        Assert.Equal(reporter.Id, history.ChangedById);
    }

    [Fact]
    public async Task Create_TrimsTheDescriptionAndLocationDetail()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var input = Input("   Broken tap   ");
        input.LocationDetail = "  Level 2  ";

        await _t.Reports.CreateAsync(reporter.Id, input);

        var saved = await _t.Db.Reports.SingleAsync();
        Assert.Equal("Broken tap", saved.Description);
        Assert.Equal("Level 2", saved.LocationDetail);
    }

    [Fact]
    public async Task Create_BlankLocationDetailIsStoredAsNull()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var input = Input();
        input.LocationDetail = "   ";

        await _t.Reports.CreateAsync(reporter.Id, input);

        Assert.Null((await _t.Db.Reports.SingleAsync()).LocationDetail);
    }

    [Fact]
    public async Task Create_RecordsUtcTimestamps()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var before = DateTime.UtcNow.AddSeconds(-5);

        await _t.Reports.CreateAsync(reporter.Id, Input());

        var saved = await _t.Db.Reports.SingleAsync();
        Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
    }

    [Fact]
    public async Task Create_RejectsAnUnknownIncidentType()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var input = Input();
        input.CategoryId = 9999;

        var result = await _t.Reports.CreateAsync(reporter.Id, input);

        Assert.False(result.Succeeded);
        Assert.Equal("Please choose a valid incident type and location.", result.Error);
        Assert.Empty(_t.Db.Reports);
    }

    [Fact]
    public async Task Create_RejectsAMissingIncidentTypeOrLocation()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        var noCategory = Input();
        noCategory.CategoryId = null;
        var noLocation = Input();
        noLocation.LocationId = null;

        Assert.False((await _t.Reports.CreateAsync(reporter.Id, noCategory)).Succeeded);
        Assert.False((await _t.Reports.CreateAsync(reporter.Id, noLocation)).Succeeded);
        Assert.Empty(_t.Db.Reports);
    }

    [Fact]
    public async Task Create_RejectsAnInactiveIncidentType()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        (await _t.Db.Categories.SingleAsync()).IsActive = false;
        await _t.Db.SaveChangesAsync();

        var result = await _t.Reports.CreateAsync(reporter.Id, Input());

        Assert.False(result.Succeeded);
        Assert.Empty(_t.Db.Reports);
    }

    [Fact]
    public async Task Create_RejectsAnInactiveLocation()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        (await _t.Db.Locations.SingleAsync()).IsActive = false;
        await _t.Db.SaveChangesAsync();

        var result = await _t.Reports.CreateAsync(reporter.Id, Input());

        Assert.False(result.Succeeded);
        Assert.Empty(_t.Db.Reports);
    }

    [Fact]
    public async Task Create_WithAValidPhotoStoresTheAttachment()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var input = Input();
        input.Photo = Make.File(Make.PngBytes, "leak.png");

        var result = await _t.Reports.CreateAsync(reporter.Id, input);

        Assert.True(result.Succeeded, result.Error);
        var attachment = await _t.Db.Attachments.SingleAsync();
        Assert.Equal(result.ReportId, attachment.ReportId);
        Assert.Equal("leak.png", attachment.OriginalName);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal(Make.PngBytes.Length, attachment.SizeBytes);
        Assert.True(File.Exists(Path.Combine(_t.UploadDir, attachment.StoredName)));
    }

    [Fact]
    public async Task Create_WithAnInvalidPhotoSavesNothing()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var input = Input();
        input.Photo = Make.File(Make.ExeBytes, "virus.png");

        var result = await _t.Reports.CreateAsync(reporter.Id, input);

        Assert.False(result.Succeeded);
        Assert.Equal("The file is not a valid PNG or JPG image.", result.Error);
        Assert.Empty(_t.Db.Reports);
        Assert.Empty(_t.Db.Attachments);
        Assert.Empty(Directory.GetFiles(_t.UploadDir));
    }

    [Fact]
    public async Task Create_WithAnUnknownReporterRollsBackCompletely()
    {
        // The reporter id is not a real account, so the foreign key fails and the whole transaction must be undone.
        var input = Input();
        input.Photo = Make.File(Make.PngBytes, "leak.png");

        var result = await _t.Reports.CreateAsync("no-such-user", input);

        Assert.False(result.Succeeded);
        Assert.Equal("We could not save your report. Please try again.", result.Error);
        Assert.Equal(0, await _t.Db.Reports.AsNoTracking().CountAsync());
        Assert.Equal(0, await _t.Db.StatusHistory.AsNoTracking().CountAsync());
        Assert.Empty(Directory.GetFiles(_t.UploadDir));
    }

    // ---- SearchAsync ----

    private async Task<(ApplicationUser r1, ApplicationUser r2, ApplicationUser tech)> SeedMixAsync()
    {
        var r1 = await _t.AddUserAsync("r1@cmrp.test", Roles.Reporter, "Reporter One");
        var r2 = await _t.AddUserAsync("r2@cmrp.test", Roles.Reporter, "Reporter Two");
        var tech = await _t.AddUserAsync("t@cmrp.test", Roles.Technician, "Tech");

        var now = DateTime.UtcNow;
        await _t.AddReportAsync(r1.Id, ReportStatus.Submitted, ReportPriority.Low, createdAt: now.AddDays(-3), description: "Broken tap");
        await _t.AddReportAsync(r1.Id, ReportStatus.InProgress, ReportPriority.Critical, tech.Id, now.AddDays(-2), description: "Burst pipe");
        await _t.AddReportAsync(r2.Id, ReportStatus.Resolved, ReportPriority.High, tech.Id, now.AddDays(-1), now, "Flooded floor");
        await _t.AddReportAsync(r2.Id, ReportStatus.Cancelled, ReportPriority.Normal, createdAt: now, description: "Duplicate report");
        return (r1, r2, tech);
    }

    [Fact]
    public async Task Search_NoFiltersReturnsEverythingNewestFirst()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery());

        Assert.Equal(4, list.Count);
        Assert.Equal(list.OrderByDescending(r => r.CreatedAt).Select(r => r.Id), list.Select(r => r.Id));
    }

    [Fact]
    public async Task Search_ReportersOnlySeeTheirOwn()
    {
        var (r1, _, _) = await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { ReporterId = r1.Id });

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.Equal(r1.Id, r.ReporterId));
    }

    [Fact]
    public async Task Search_TechniciansOnlySeeAssignedWork()
    {
        var (_, _, tech) = await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { AssignedToUserId = tech.Id });

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.Equal(tech.Id, r.AssignedToUserId));
    }

    [Fact]
    public async Task Search_FiltersByStatus()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { Status = ReportStatus.Resolved });

        Assert.Single(list);
        Assert.Equal(ReportStatus.Resolved, list[0].Status);
    }

    [Fact]
    public async Task Search_FiltersByPriority()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { Priority = ReportPriority.Critical });

        Assert.Single(list);
    }

    [Fact]
    public async Task Search_ActiveOnlyHidesFinishedReports()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { ActiveOnly = true });

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.True(r.Status.IsActive()));
    }

    [Fact]
    public async Task Search_UnassignedOnlyReturnsReportsWithoutAnAssignee()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { UnassignedOnly = true });

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.Null(r.AssignedToUserId));
    }

    [Fact]
    public async Task Search_TextMatchesTheDescriptionIgnoringCase()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { Search = "BURST" });

        Assert.Single(list);
        Assert.Equal("Burst pipe", list[0].Description);
    }

    [Fact]
    public async Task Search_TextMatchesTheReferenceNumber()
    {
        await SeedMixAsync();
        var reference = (await _t.Db.Reports.OrderBy(r => r.Id).FirstAsync()).ReferenceNumber;

        var list = await _t.Reports.SearchAsync(new ReportQuery { Search = reference });

        Assert.Single(list);
    }

    [Fact]
    public async Task Search_TextMatchesCategoryAndBuilding()
    {
        await SeedMixAsync();

        Assert.Equal(4, (await _t.Reports.SearchAsync(new ReportQuery { Search = "water leak" })).Count);
        Assert.Equal(4, (await _t.Reports.SearchAsync(new ReportQuery { Search = "library" })).Count);
        Assert.Empty(await _t.Reports.SearchAsync(new ReportQuery { Search = "no such thing" }));
    }

    [Fact]
    public async Task Search_CanOrderByPriority()
    {
        await SeedMixAsync();

        var list = await _t.Reports.SearchAsync(new ReportQuery { OrderByPriority = true });

        Assert.Equal(
            new[] { ReportPriority.Critical, ReportPriority.High, ReportPriority.Normal, ReportPriority.Low },
            list.Select(r => r.Priority));
    }

    [Fact]
    public async Task Search_RespectsTake()
    {
        await SeedMixAsync();

        Assert.Equal(2, (await _t.Reports.SearchAsync(new ReportQuery { Take = 2 })).Count);
    }

    [Fact]
    public async Task Search_LoadsTheRelatedDataTheListNeeds()
    {
        await SeedMixAsync();

        var first = (await _t.Reports.SearchAsync(new ReportQuery())).First();

        Assert.NotNull(first.Category);
        Assert.NotNull(first.Location);
        Assert.NotNull(first.Reporter);
    }

    // ---- GetDetailsAsync ----

    [Fact]
    public async Task Details_ReturnsNullForAnUnknownId() =>
        Assert.Null(await _t.Reports.GetDetailsAsync(12345));

    [Fact]
    public async Task Details_LoadsHistoryNotesAndAssignee()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var tech = await _t.AddUserAsync("t@cmrp.test", Roles.Technician, "Sipho");
        var coordinator = await _t.AddUserAsync("c@cmrp.test", Roles.Coordinator);
        var created = await _t.Reports.CreateAsync(reporter.Id, Input());
        var principal = TestUser.Principal(coordinator.Id, Roles.Coordinator);
        await _t.Workflow.AssignAsync(created.ReportId, tech.Id, principal);
        await _t.Workflow.AddNoteAsync(created.ReportId, "On my way", false, principal);

        var details = await _t.Reports.GetDetailsAsync(created.ReportId);

        Assert.NotNull(details);
        Assert.Equal("Sipho", details!.AssignedTo!.DisplayName);
        Assert.Equal(2, details.StatusHistory.Count);
        Assert.Single(details.AssignmentHistory);
        Assert.Single(details.Notes);
        Assert.Equal(coordinator.Id, details.Notes.Single().Author.Id);
    }
}
