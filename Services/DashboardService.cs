using System.Globalization;
using CMRP.Data;
using CMRP.Models;
using CMRP.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Services;

/// <summary>
/// Summary figures for the facilities dashboard and analytics page (FR-13). All numbers come from stored report data.
/// Aggregation is done in memory over a small projection, which keeps the SQL portable between SQLite and a hosted database.
/// </summary>
public class DashboardService
{
    private static readonly string[] CampusColours = { "#1466f5", "#7c3aed", "#16a34a", "#f59e0b", "#94a3b8" };

    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Flat projection of a report used for aggregation, so the maths can be tested without a database.</summary>
    public sealed class ReportRow
    {
        public ReportStatus Status { get; set; }
        public ReportPriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? AssignedToUserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Campus { get; set; } = string.Empty;
    }

    public async Task<DashboardViewModel> GetAsync()
    {
        var rows = await _db.Reports
            .AsNoTracking()
            .Select(r => new ReportRow
            {
                Status = r.Status,
                Priority = r.Priority,
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt,
                AssignedToUserId = r.AssignedToUserId,
                Category = r.Category.Name,
                Campus = r.Location.Campus
            })
            .ToListAsync();

        return Build(rows, DateTime.UtcNow);
    }

    /// <summary>Pure calculation of every dashboard figure (Task 3 section 6.6).</summary>
    public static DashboardViewModel Build(IReadOnlyList<ReportRow> rows, DateTime now)
    {
        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Deltas compare the last 30 days with the 30 days before that, so a part-finished month is never compared with a whole one.
        var recentStart = now.AddDays(-30);
        var previousStart = now.AddDays(-60);

        var solved = rows.Where(r => r.Status == ReportStatus.Resolved || r.Status == ReportStatus.Closed).ToList();

        var vm = new DashboardViewModel
        {
            TotalIncidents = rows.Count,
            InProgress = rows.Count(r => r.Status == ReportStatus.InProgress),
            Resolved = solved.Count,
            AwaitingAssignment = rows.Count(r =>
                r.AssignedToUserId == null && (r.Status == ReportStatus.Submitted || r.Status == ReportStatus.UnderReview)),
            TotalDeltaPercent = PercentChange(
                rows.Count(r => r.CreatedAt >= recentStart),
                rows.Count(r => r.CreatedAt >= previousStart && r.CreatedAt < recentStart)),
            ResolvedDeltaPercent = PercentChange(
                solved.Count(r => r.ResolvedAt >= recentStart),
                solved.Count(r => r.ResolvedAt >= previousStart && r.ResolvedAt < recentStart))
        };

        // Fix time = created to resolved, for reports that have been resolved.
        var fixDays = solved
            .Where(r => r.ResolvedAt.HasValue)
            .Select(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalDays)
            .ToList();

        vm.AverageFixDays = fixDays.Count > 0 ? Math.Round(fixDays.Average(), 1) : null;
        vm.FixTimeBuckets = BuildFixBuckets(fixDays);
        vm.DonutGradient = BuildDonutGradient(vm.FixTimeBuckets);

        var byCategory = rows
            .GroupBy(r => r.Category)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        vm.TopCategories = ToItems(byCategory.Take(5), rows.Count, "#1466f5");
        vm.AllCategories = ToItems(byCategory, rows.Count, "#1466f5");

        var byCampus = rows
            .GroupBy(r => r.Campus)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        vm.ByCampus = ToItems(byCampus, rows.Count, null);
        for (var i = 0; i < vm.ByCampus.Count; i++)
        {
            vm.ByCampus[i].Color = CampusColours[Math.Min(i, CampusColours.Length - 1)];
        }

        vm.ByStatus = ToItems(
            Enum.GetValues<ReportStatus>().Select(s => (Label: s.Label(), Count: rows.Count(r => r.Status == s))),
            rows.Count, "#1466f5");

        vm.ByPriority = ToItems(
            Enum.GetValues<ReportPriority>().Reverse().Select(p => (Label: p.Label(), Count: rows.Count(r => r.Priority == p))),
            rows.Count, "#f59e0b");

        var trend = new List<TrendPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var start = thisMonth.AddMonths(-i);
            var end = start.AddMonths(1);
            trend.Add(new TrendPoint
            {
                Label = start.ToString("MMM", CultureInfo.InvariantCulture),
                Created = rows.Count(r => r.CreatedAt >= start && r.CreatedAt < end),
                Resolved = solved.Count(r => r.ResolvedAt >= start && r.ResolvedAt < end)
            });
        }
        vm.Trend = trend;

        return vm;
    }

    private static double? PercentChange(int current, int previous) =>
        previous == 0 ? null : Math.Round((current - previous) * 100.0 / previous);

    private static List<ChartItem> BuildFixBuckets(List<double> days)
    {
        var buckets = new List<ChartItem>
        {
            new() { Label = "< 1 day", Count = days.Count(d => d < 1), Color = "#22c55e" },
            new() { Label = "1-3 days", Count = days.Count(d => d >= 1 && d < 4), Color = "#1466f5" },
            new() { Label = "4-7 days", Count = days.Count(d => d >= 4 && d < 8), Color = "#f59e0b" },
            new() { Label = "8+ days", Count = days.Count(d => d >= 8), Color = "#ef4444" }
        };

        foreach (var b in buckets)
        {
            b.Share = days.Count == 0 ? 0 : b.Count * 100.0 / days.Count;
            b.Width = b.Share;
        }

        return buckets;
    }

    private static string BuildDonutGradient(List<ChartItem> buckets)
    {
        if (buckets.All(b => b.Count == 0))
        {
            return "#e5e9f2 0% 100%";
        }

        var parts = new List<string>();
        double from = 0;
        foreach (var b in buckets.Where(b => b.Count > 0))
        {
            var to = from + b.Share;
            parts.Add($"{b.Color} {from.ToString("0.##", CultureInfo.InvariantCulture)}% {to.ToString("0.##", CultureInfo.InvariantCulture)}%");
            from = to;
        }

        return string.Join(", ", parts);
    }

    private static List<ChartItem> ToItems(IEnumerable<(string Label, int Count)> data, int total, string? color)
    {
        var list = data.ToList();
        var max = list.Count == 0 ? 0 : list.Max(x => x.Count);

        return list.Select(x => new ChartItem
        {
            Label = x.Label,
            Count = x.Count,
            Share = total == 0 ? 0 : x.Count * 100.0 / total,
            Width = max == 0 ? 0 : x.Count * 100.0 / max,
            Color = color ?? "#1466f5"
        }).ToList();
    }
}
