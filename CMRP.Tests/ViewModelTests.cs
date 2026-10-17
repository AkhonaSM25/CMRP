using System.ComponentModel.DataAnnotations;
using CMRP.Models;
using CMRP.ViewModels;

namespace CMRP.Tests;

/// <summary>Form validation (FR-02) and the report timeline privacy rules (FR-11).</summary>
public class ViewModelTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static CreateReportViewModel ValidReport() => new()
    {
        CategoryId = 1,
        LocationId = 1,
        Description = "The ceiling is leaking."
    };

    // ---- Report Incident form ----

    [Fact]
    public void CreateReport_ValidFormPasses() => Assert.Empty(Validate(ValidReport()));

    [Fact]
    public void CreateReport_RequiresAnIncidentType()
    {
        var model = ValidReport();
        model.CategoryId = null;
        Assert.Contains(Validate(model), r => r.ErrorMessage == "Please select an incident type.");
    }

    [Fact]
    public void CreateReport_RequiresALocation()
    {
        var model = ValidReport();
        model.LocationId = null;
        Assert.Contains(Validate(model), r => r.ErrorMessage == "Please select a building or area.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateReport_RequiresADescription(string? description)
    {
        var model = ValidReport();
        model.Description = description!;
        Assert.Contains(Validate(model), r => r.ErrorMessage == "Please describe the incident.");
    }

    [Fact]
    public void CreateReport_DescriptionAtTheLimitPasses()
    {
        var model = ValidReport();
        model.Description = new string('a', 500);
        Assert.Empty(Validate(model));
    }

    [Fact]
    public void CreateReport_DescriptionOverTheLimitFails()
    {
        var model = ValidReport();
        model.Description = new string('a', 501);
        Assert.Contains(Validate(model), r => r.ErrorMessage == "The description can be at most 500 characters.");
    }

    [Fact]
    public void CreateReport_LocationDetailIsCappedAt150()
    {
        var model = ValidReport();
        model.LocationDetail = new string('a', 151);
        Assert.NotEmpty(Validate(model));
    }

    // ---- Account forms ----

    [Fact]
    public void Login_RequiresEmailAndPassword()
    {
        var errors = Validate(new LoginViewModel());
        Assert.Contains(errors, r => r.ErrorMessage == "Please enter your email address.");
        Assert.Contains(errors, r => r.ErrorMessage == "Please enter your password.");
    }

    [Fact]
    public void Login_RejectsAMalformedEmail()
    {
        var errors = Validate(new LoginViewModel { Email = "not-an-email", Password = "x" });
        Assert.Contains(errors, r => r.ErrorMessage == "Please enter a valid email address.");
    }

    [Fact]
    public void Register_ValidFormPasses()
    {
        var model = new RegisterViewModel
        {
            FullName = "Akhona Mkhwanazi",
            Email = "akhona@example.com",
            Password = "Secret1",
            ConfirmPassword = "Secret1"
        };
        Assert.Empty(Validate(model));
    }

    [Fact]
    public void Register_PasswordMustBeAtLeastSixCharacters()
    {
        var model = new RegisterViewModel
        {
            FullName = "A", Email = "a@example.com", Password = "abc", ConfirmPassword = "abc"
        };
        Assert.Contains(Validate(model), r => r.ErrorMessage == "The password must be at least 6 characters.");
    }

    [Fact]
    public void Register_PasswordsMustMatch()
    {
        var model = new RegisterViewModel
        {
            FullName = "A", Email = "a@example.com", Password = "Secret1", ConfirmPassword = "Secret2"
        };
        Assert.Contains(Validate(model), r => r.ErrorMessage == "The passwords do not match.");
    }

    [Fact]
    public void CreateUser_RequiresARole()
    {
        var model = new CreateUserInput { FullName = "A", Email = "a@example.com", Password = "Secret1" };
        Assert.Contains(Validate(model), r => r.ErrorMessage == "Please choose a role.");
    }

    // ---- Timeline ----

    private static MaintenanceReport ReportWithHistory()
    {
        var report = Make.Report();
        var t0 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var author = new ApplicationUser { FullName = "Sipho Khumalo" };
        var reporter = new ApplicationUser { FullName = "Lerato Mokoena" };

        report.StatusHistory.Add(new ReportStatusHistory
        {
            ChangedAt = t0, FromStatus = null, ToStatus = ReportStatus.Submitted, ChangedBy = reporter
        });
        report.StatusHistory.Add(new ReportStatusHistory
        {
            ChangedAt = t0.AddHours(3), FromStatus = ReportStatus.Submitted, ToStatus = ReportStatus.UnderReview, ChangedBy = author
        });
        report.AssignmentHistory.Add(new ReportAssignmentHistory
        {
            ChangedAt = t0.AddHours(2), ToUser = new ApplicationUser { FullName = "Thandi Zulu" }, ChangedBy = author
        });
        report.Notes.Add(new ReportNote
        {
            CreatedAt = t0.AddHours(4), Text = "Visible to the reporter", IsInternal = false, Author = author
        });
        report.Notes.Add(new ReportNote
        {
            CreatedAt = t0.AddHours(5), Text = "Internal only", IsInternal = true, Author = author
        });
        return report;
    }

    [Fact]
    public void Timeline_ForReporters_HidesInternalNotes()
    {
        var items = ReportDetailsViewModel.BuildTimeline(ReportWithHistory(), staffView: false);

        Assert.DoesNotContain(items, i => i.Text == "Internal only");
        Assert.Contains(items, i => i.Text == "Visible to the reporter");
    }

    [Fact]
    public void Timeline_ForStaff_ShowsInternalNotes()
    {
        var items = ReportDetailsViewModel.BuildTimeline(ReportWithHistory(), staffView: true);

        var internalNote = Assert.Single(items, i => i.Text == "Internal only");
        Assert.True(internalNote.IsInternal);
    }

    [Fact]
    public void Timeline_ForReporters_DoesNotNameTheTechnician()
    {
        var items = ReportDetailsViewModel.BuildTimeline(ReportWithHistory(), staffView: false);

        var assignment = Assert.Single(items, i => i.Kind == "assign");
        Assert.Equal("Assigned to the maintenance team", assignment.Title);
        Assert.DoesNotContain("Thandi", assignment.Title);
    }

    [Fact]
    public void Timeline_ForStaff_NamesTheTechnician()
    {
        var items = ReportDetailsViewModel.BuildTimeline(ReportWithHistory(), staffView: true);

        Assert.Equal("Assigned to Thandi Zulu", Assert.Single(items, i => i.Kind == "assign").Title);
    }

    [Fact]
    public void Timeline_IsInChronologicalOrder()
    {
        var items = ReportDetailsViewModel.BuildTimeline(ReportWithHistory(), staffView: true);

        Assert.Equal(items.OrderBy(i => i.When).ToList(), items);
        Assert.Equal("Report submitted", items[0].Title);
    }

    [Fact]
    public void Timeline_DescribesStatusChanges()
    {
        var items = ReportDetailsViewModel.BuildTimeline(ReportWithHistory(), staffView: true);

        Assert.Contains(items, i => i.Title == "Status changed from Submitted to Under Review");
    }

    [Fact]
    public void Timeline_OfAReportWithNoHistoryIsEmpty() =>
        Assert.Empty(ReportDetailsViewModel.BuildTimeline(Make.Report(), staffView: true));
}
