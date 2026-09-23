using System.Globalization;
using CMRP.Models;

namespace CMRP.ViewModels;

public class ChartItem
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>Percentage of the total (0-100), shown as text.</summary>
    public double Share { get; set; }

    /// <summary>Bar width relative to the largest item (0-100).</summary>
    public double Width { get; set; }

    public string Color { get; set; } = "#1466f5";

    /// <summary>Invariant-culture CSS width, e.g. "42.5%".</summary>
    public string WidthCss => Width.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public int SharePercent => (int)Math.Round(Share);
}

public class TrendPoint
{
    public string Label { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Resolved { get; set; }
}

public class DashboardViewModel
{
    public int TotalIncidents { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int AwaitingAssignment { get; set; }

    /// <summary>Change over the last 30 days compared with the 30 days before; null when there is nothing to compare.</summary>
    public double? TotalDeltaPercent { get; set; }

    public double? ResolvedDeltaPercent { get; set; }
    public double? AverageFixDays { get; set; }

    public List<ChartItem> TopCategories { get; set; } = new();
    public List<ChartItem> AllCategories { get; set; } = new();
    public List<ChartItem> ByCampus { get; set; } = new();
    public List<ChartItem> ByStatus { get; set; } = new();
    public List<ChartItem> ByPriority { get; set; } = new();
    public List<ChartItem> FixTimeBuckets { get; set; } = new();
    public List<TrendPoint> Trend { get; set; } = new();

    /// <summary>CSS conic-gradient stops for the fix-time donut.</summary>
    public string DonutGradient { get; set; } = "#e5e9f2 0% 100%";

    public List<MaintenanceReport> Recent { get; set; } = new();
}

public class ComplaintsViewModel
{
    public List<MaintenanceReport> Reports { get; set; } = new();
    public string? Search { get; set; }
    public ReportStatus? Status { get; set; }
    public ReportPriority? Priority { get; set; }
    public int? CategoryId { get; set; }
    public bool Unassigned { get; set; }
    public bool ActiveOnly { get; set; }
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Categories { get; set; } = new();
}
