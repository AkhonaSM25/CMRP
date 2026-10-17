using CMRP.Models;
using CMRP.Services;
using static CMRP.Services.DashboardService;

namespace CMRP.Tests;

/// <summary>Quality metrics behind the facilities dashboard (FR-13). Uses the pure Build() method, no database.</summary>
public class DashboardCalculationTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static ReportRow Row(
        ReportStatus status = ReportStatus.Submitted,
        ReportPriority priority = ReportPriority.Normal,
        DateTime? created = null,
        DateTime? resolved = null,
        string? assignedTo = null,
        string category = "Water leak",
        string campus = "Howard College") => new()
    {
        Status = status,
        Priority = priority,
        CreatedAt = created ?? Now.AddDays(-1),
        ResolvedAt = resolved,
        AssignedToUserId = assignedTo,
        Category = category,
        Campus = campus
    };

    private static ReportRow Solved(double daysToFix, ReportStatus status = ReportStatus.Resolved)
    {
        var created = Now.AddDays(-20);
        return Row(status, created: created, resolved: created.AddDays(daysToFix), assignedTo: "t1");
    }

    [Fact]
    public void EmptyData_GivesZerosAndNoAverage()
    {
        var vm = Build(new List<ReportRow>(), Now);

        Assert.Equal(0, vm.TotalIncidents);
        Assert.Equal(0, vm.InProgress);
        Assert.Equal(0, vm.Resolved);
        Assert.Equal(0, vm.AwaitingAssignment);
        Assert.Null(vm.AverageFixDays);
        Assert.Null(vm.TotalDeltaPercent);
        Assert.Null(vm.ResolvedDeltaPercent);
        Assert.Empty(vm.TopCategories);
        Assert.Empty(vm.ByCampus);
    }

    [Fact]
    public void EmptyData_DonutIsAGreyRing_AndNothingDividesByZero()
    {
        var vm = Build(new List<ReportRow>(), Now);

        Assert.Equal("#e5e9f2 0% 100%", vm.DonutGradient);
        Assert.All(vm.FixTimeBuckets, b => Assert.Equal(0, b.Share));
        Assert.All(vm.ByStatus, s => Assert.Equal(0, s.Width));
    }

    [Fact]
    public void Totals_CountEveryReport()
    {
        var rows = new List<ReportRow> { Row(), Row(), Row(ReportStatus.Cancelled) };
        Assert.Equal(3, Build(rows, Now).TotalIncidents);
    }

    [Fact]
    public void InProgress_CountsOnlyInProgressReports()
    {
        var rows = new List<ReportRow>
        {
            Row(ReportStatus.InProgress), Row(ReportStatus.InProgress), Row(ReportStatus.Assigned), Row(ReportStatus.Submitted)
        };
        Assert.Equal(2, Build(rows, Now).InProgress);
    }

    [Fact]
    public void Resolved_CountsResolvedAndClosed()
    {
        var rows = new List<ReportRow> { Solved(1), Solved(2, ReportStatus.Closed), Row(ReportStatus.InProgress) };
        Assert.Equal(2, Build(rows, Now).Resolved);
    }

    [Fact]
    public void AwaitingAssignment_CountsUnassignedSubmittedAndUnderReview()
    {
        var rows = new List<ReportRow>
        {
            Row(ReportStatus.Submitted),                      // counts
            Row(ReportStatus.UnderReview),                    // counts
            Row(ReportStatus.Submitted, assignedTo: "t1"),    // already has an assignee
            Row(ReportStatus.InProgress),                     // not waiting for triage
            Row(ReportStatus.Cancelled)                       // finished
        };
        Assert.Equal(2, Build(rows, Now).AwaitingAssignment);
    }

    [Fact]
    public void AverageFixDays_IsTheMeanOfCreatedToResolved()
    {
        var rows = new List<ReportRow> { Solved(1), Solved(3) };
        Assert.Equal(2.0, Build(rows, Now).AverageFixDays);
    }

    [Fact]
    public void AverageFixDays_IsRoundedToOneDecimal()
    {
        var rows = new List<ReportRow> { Solved(1), Solved(1), Solved(2) }; // 1.333...
        Assert.Equal(1.3, Build(rows, Now).AverageFixDays);
    }

    [Fact]
    public void AverageFixDays_IgnoresReportsThatAreNotResolved()
    {
        var rows = new List<ReportRow> { Solved(4), Row(ReportStatus.InProgress, created: Now.AddDays(-30)) };
        Assert.Equal(4.0, Build(rows, Now).AverageFixDays);
    }

    [Fact]
    public void FixTimeBuckets_SplitReportsByHowLongTheyTook()
    {
        var rows = new List<ReportRow> { Solved(0.5), Solved(2), Solved(5), Solved(9) };
        var buckets = Build(rows, Now).FixTimeBuckets;

        Assert.Equal(4, buckets.Count);
        Assert.Equal(new[] { 1, 1, 1, 1 }, buckets.Select(b => b.Count));
        Assert.All(buckets, b => Assert.Equal(25.0, b.Share));
    }

    [Theory]
    [InlineData(0.99, 0)]
    [InlineData(1.0, 1)]
    [InlineData(3.9, 1)]
    [InlineData(4.0, 2)]
    [InlineData(7.9, 2)]
    [InlineData(8.0, 3)]
    [InlineData(30.0, 3)]
    public void FixTimeBuckets_BoundariesLandInTheRightBucket(double days, int expectedBucket)
    {
        var buckets = Build(new List<ReportRow> { Solved(days) }, Now).FixTimeBuckets;
        Assert.Equal(1, buckets[expectedBucket].Count);
        Assert.Equal(1, buckets.Sum(b => b.Count));
    }

    [Fact]
    public void Donut_ContainsOneSegmentPerNonEmptyBucket_EndingAt100Percent()
    {
        var rows = new List<ReportRow> { Solved(0.5), Solved(9) };
        var gradient = Build(rows, Now).DonutGradient;

        Assert.Contains("0% 50%", gradient);
        Assert.Contains("50% 100%", gradient);
        Assert.Equal(2, gradient.Split(',').Length);
    }

    [Fact]
    public void TotalDelta_ComparesLast30DaysWithThePrevious30()
    {
        var rows = new List<ReportRow>
        {
            Row(created: Now.AddDays(-5)), Row(created: Now.AddDays(-10)), Row(created: Now.AddDays(-20)), // recent: 3
            Row(created: Now.AddDays(-40)), Row(created: Now.AddDays(-50))                                 // previous: 2
        };
        Assert.Equal(50.0, Build(rows, Now).TotalDeltaPercent);
    }

    [Fact]
    public void TotalDelta_CanBeNegative()
    {
        var rows = new List<ReportRow>
        {
            Row(created: Now.AddDays(-5)),
            Row(created: Now.AddDays(-40)), Row(created: Now.AddDays(-41))
        };
        Assert.Equal(-50.0, Build(rows, Now).TotalDeltaPercent);
    }

    [Fact]
    public void TotalDelta_IsNullWhenThereIsNothingToCompare()
    {
        var rows = new List<ReportRow> { Row(created: Now.AddDays(-5)) };
        Assert.Null(Build(rows, Now).TotalDeltaPercent);
    }

    [Fact]
    public void ResolvedDelta_ComparesResolutionDates()
    {
        var rows = new List<ReportRow>
        {
            Row(ReportStatus.Resolved, created: Now.AddDays(-90), resolved: Now.AddDays(-3)),
            Row(ReportStatus.Resolved, created: Now.AddDays(-90), resolved: Now.AddDays(-4)),
            Row(ReportStatus.Resolved, created: Now.AddDays(-90), resolved: Now.AddDays(-45))
        };
        Assert.Equal(100.0, Build(rows, Now).ResolvedDeltaPercent);
    }

    [Fact]
    public void TopCategories_AreSortedByCount_AndLimitedToFive()
    {
        var rows = new List<ReportRow>();
        for (var i = 1; i <= 6; i++)
        {
            for (var n = 0; n < i; n++) rows.Add(Row(category: $"Type {i}"));
        }

        var vm = Build(rows, Now);

        Assert.Equal(5, vm.TopCategories.Count);
        Assert.Equal(6, vm.AllCategories.Count);
        Assert.Equal("Type 6", vm.TopCategories[0].Label);
        Assert.Equal(6, vm.TopCategories[0].Count);
        Assert.DoesNotContain(vm.TopCategories, c => c.Label == "Type 1");
    }

    [Fact]
    public void ChartItems_ShareIsAPercentageOfTheTotal_AndWidthIsRelativeToTheLargest()
    {
        var rows = new List<ReportRow>
        {
            Row(category: "A"), Row(category: "A"), Row(category: "A"), Row(category: "B")
        };
        var items = Build(rows, Now).AllCategories;

        Assert.Equal(75.0, items[0].Share);
        Assert.Equal(100.0, items[0].Width);
        Assert.Equal(25.0, items[1].Share);
        Assert.Equal(33.3, Math.Round(items[1].Width, 1));
        Assert.Equal(75, items[0].SharePercent);
        Assert.Equal("100%", items[0].WidthCss);
    }

    [Fact]
    public void ByCampus_GivesEachCampusItsOwnColour()
    {
        var rows = new List<ReportRow>
        {
            Row(campus: "Howard College"), Row(campus: "Howard College"), Row(campus: "Westville")
        };
        var campuses = Build(rows, Now).ByCampus;

        Assert.Equal("Howard College", campuses[0].Label);
        Assert.NotEqual(campuses[0].Color, campuses[1].Color);
    }

    [Fact]
    public void ByStatus_ListsEverySeriesStatus_EvenWhenZero()
    {
        var vm = Build(new List<ReportRow> { Row(ReportStatus.InProgress) }, Now);

        Assert.Equal(Enum.GetValues<ReportStatus>().Length, vm.ByStatus.Count);
        Assert.Equal(1, vm.ByStatus.Single(s => s.Label == "In Progress").Count);
        Assert.Equal(0, vm.ByStatus.Single(s => s.Label == "Closed").Count);
    }

    [Fact]
    public void ByPriority_StartsWithCritical()
    {
        var vm = Build(new List<ReportRow> { Row(priority: ReportPriority.Critical) }, Now);

        Assert.Equal("Critical", vm.ByPriority[0].Label);
        Assert.Equal(1, vm.ByPriority[0].Count);
        Assert.Equal(4, vm.ByPriority.Count);
    }

    [Fact]
    public void Trend_CoversTheLastSixMonthsEndingWithTheCurrentMonth()
    {
        var vm = Build(new List<ReportRow>(), Now);

        Assert.Equal(new[] { "May", "Jun", "Jul", "Aug", "Sep", "Oct" }, vm.Trend.Select(t => t.Label));
    }

    [Fact]
    public void Trend_CountsCreatedAndResolvedPerMonth()
    {
        var rows = new List<ReportRow>
        {
            Row(created: new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc)),
            Row(created: new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc)),
            Row(ReportStatus.Resolved,
                created: new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc),
                resolved: new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc))
        };
        var trend = Build(rows, Now).Trend;

        var october = trend[^1];
        var september = trend[^2];
        Assert.Equal(2, october.Created);
        Assert.Equal(1, october.Resolved);
        Assert.Equal(1, september.Created);
        Assert.Equal(0, september.Resolved);
    }
}
