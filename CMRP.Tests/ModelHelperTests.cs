using CMRP.Models;

namespace CMRP.Tests;

/// <summary>Display and helper logic in Models/: labels, time zone, initials, icons, report title.</summary>
public class ModelHelperTests
{
    // ---- EnumExtensions ----

    [Theory]
    [InlineData(ReportStatus.Submitted, "Submitted")]
    [InlineData(ReportStatus.UnderReview, "Under Review")]
    [InlineData(ReportStatus.Assigned, "Assigned")]
    [InlineData(ReportStatus.InProgress, "In Progress")]
    [InlineData(ReportStatus.Resolved, "Resolved")]
    [InlineData(ReportStatus.Closed, "Closed")]
    [InlineData(ReportStatus.Cancelled, "Cancelled")]
    public void StatusLabel_IsHumanReadable(ReportStatus status, string expected) =>
        Assert.Equal(expected, status.Label());

    [Theory]
    [InlineData(ReportStatus.Submitted, true)]
    [InlineData(ReportStatus.UnderReview, true)]
    [InlineData(ReportStatus.Assigned, true)]
    [InlineData(ReportStatus.InProgress, true)]
    [InlineData(ReportStatus.Resolved, false)]
    [InlineData(ReportStatus.Closed, false)]
    [InlineData(ReportStatus.Cancelled, false)]
    public void IsActive_IsTrueOnlyWhileWorkRemains(ReportStatus status, bool expected) =>
        Assert.Equal(expected, status.IsActive());

    [Theory]
    [InlineData(ReportStatus.Submitted, 0)]
    [InlineData(ReportStatus.UnderReview, 1)]
    [InlineData(ReportStatus.Assigned, 2)]
    [InlineData(ReportStatus.InProgress, 3)]
    [InlineData(ReportStatus.Resolved, 4)]
    [InlineData(ReportStatus.Closed, 4)]
    [InlineData(ReportStatus.Cancelled, -1)]
    public void StepIndex_MatchesTheProgressBar(ReportStatus status, int expected) =>
        Assert.Equal(expected, status.StepIndex());

    [Theory]
    [InlineData(ReportStatus.InProgress, "st-inprogress")]
    [InlineData(ReportStatus.Cancelled, "st-cancelled")]
    public void StatusCssClass_IsLowercasePrefixed(ReportStatus status, string expected) =>
        Assert.Equal(expected, status.CssClass());

    [Theory]
    [InlineData(ReportPriority.Low, "Low", "pr-low")]
    [InlineData(ReportPriority.Normal, "Normal", "pr-normal")]
    [InlineData(ReportPriority.High, "High", "pr-high")]
    [InlineData(ReportPriority.Critical, "Critical", "pr-critical")]
    public void PriorityLabelAndCssClass(ReportPriority priority, string label, string css)
    {
        Assert.Equal(label, priority.Label());
        Assert.Equal(css, priority.CssClass());
    }

    [Fact]
    public void Priorities_AreOrderedFromLowToCritical()
    {
        Assert.True(ReportPriority.Critical > ReportPriority.High);
        Assert.True(ReportPriority.High > ReportPriority.Normal);
        Assert.True(ReportPriority.Normal > ReportPriority.Low);
    }

    // ---- DisplayExtensions ----

    [Fact]
    public void Sast_AddsTwoHoursToUtc()
    {
        var utc = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal("17 Oct 2026, 12:00", utc.Sast());
    }

    [Fact]
    public void Sast_RollsOverMidnight()
    {
        var utc = new DateTime(2026, 10, 17, 22, 30, 0, DateTimeKind.Utc);
        Assert.Equal("18 Oct 2026, 00:30", utc.Sast());
    }

    [Fact]
    public void Sast_TreatsUnspecifiedKindAsUtc()
    {
        // EF Core returns DateTimeKind.Unspecified from SQLite; the value is still UTC.
        var fromDb = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("01 Jan 2026, 10:00", fromDb.Sast());
    }

    [Fact]
    public void Sast_HonoursACustomFormat()
    {
        var utc = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal("12:00", utc.Sast("HH:mm"));
    }

    [Fact]
    public void Sast_NullableNullShowsADash() =>
        Assert.Equal("-", ((DateTime?)null).Sast());

    [Fact]
    public void Sast_NullableValueIsFormatted()
    {
        DateTime? utc = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal("17 Oct 2026, 12:00", utc.Sast());
    }

    [Theory]
    [InlineData(null, "?")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    [InlineData("akhona", "A")]
    [InlineData("Akhona Mkhwanazi", "AM")]
    [InlineData("Akhona Sphumelele Mkhwanazi", "AM")]
    [InlineData("  lerato   mokoena ", "LM")]
    public void Initials_UsesFirstAndLastName(string? name, string expected) =>
        Assert.Equal(expected, DisplayExtensions.Initials(name));

    // ---- CategoryStyles ----

    [Theory]
    [InlineData("Electrical fault", "zap")]
    [InlineData("Lighting", "zap")]
    [InlineData("Water leak", "droplet")]
    [InlineData("Plumbing blockage", "droplet")]
    [InlineData("Vandalism", "shield")]
    [InlineData("Broken window", "image")]
    [InlineData("Sanitation issue", "trash")]
    [InlineData("Wi-Fi / network", "monitor")]
    [InlineData("Projector / AV equipment", "monitor")]
    [InlineData("Computer lab equipment", "monitor")]
    [InlineData("Furniture damage", "tool")]
    [InlineData("Other", "tool")]
    public void CategoryStyle_PicksTheRightIcon(string category, string icon) =>
        Assert.Equal(icon, CategoryStyles.For(category).Icon);

    [Fact]
    public void CategoryStyle_IsCaseInsensitive() =>
        Assert.Equal("zap", CategoryStyles.For("ELECTRICAL FAULT").Icon);

    [Fact]
    public void CategoryStyle_ReturnsACssClass() =>
        Assert.StartsWith("cat-", CategoryStyles.For("Water leak").Css);

    // ---- ReportExtensions ----

    [Fact]
    public void ReportTitle_CombinesTypeAndBuilding() =>
        Assert.Equal("Water leak (Library)", Make.Report().ReportTitle());

    [Fact]
    public void ReportTitle_AddsTheSpecificLocation() =>
        Assert.Equal("Water leak (Library - Level 1)", Make.Report(detail: "Level 1").ReportTitle());

    [Fact]
    public void ReportTitle_IgnoresBlankLocationDetail() =>
        Assert.Equal("Water leak (Library)", Make.Report(detail: "   ").ReportTitle());

    // ---- Roles / user model ----

    [Fact]
    public void Roles_AllContainsTheFourRoles() =>
        Assert.Equal(
            new[] { "Administrator", "Coordinator", "Reporter", "Technician" },
            Roles.All.OrderBy(r => r));

    [Fact]
    public void Roles_ManagersAreCoordinatorAndAdministratorOnly() =>
        Assert.Equal(new[] { "Administrator", "Coordinator" }, Roles.Managers.Split(',').OrderBy(r => r));

    [Fact]
    public void Roles_AllStaffIncludesTechnicians() =>
        Assert.Contains(Roles.Technician, Roles.AllStaff.Split(','));

    [Fact]
    public void DisplayName_PrefersFullName() =>
        Assert.Equal("Akhona M", new ApplicationUser { FullName = "Akhona M", UserName = "a@x.com" }.DisplayName);

    [Fact]
    public void DisplayName_FallsBackToUserNameThenEmail()
    {
        Assert.Equal("a@x.com", new ApplicationUser { UserName = "a@x.com" }.DisplayName);
        Assert.Equal("b@x.com", new ApplicationUser { Email = "b@x.com" }.DisplayName);
        Assert.Equal("User", new ApplicationUser().DisplayName);
    }

    [Fact]
    public void CampusLocation_DisplayNameJoinsCampusAndBuilding() =>
        Assert.Equal("Westville - Library", new CampusLocation { Campus = "Westville", Building = "Library" }.DisplayName);
}
