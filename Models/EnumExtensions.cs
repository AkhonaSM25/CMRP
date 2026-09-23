namespace CMRP.Models;

public static class EnumExtensions
{
    public static string Label(this ReportStatus status) => status switch
    {
        ReportStatus.Submitted => "Submitted",
        ReportStatus.UnderReview => "Under Review",
        ReportStatus.Assigned => "Assigned",
        ReportStatus.InProgress => "In Progress",
        ReportStatus.Resolved => "Resolved",
        ReportStatus.Closed => "Closed",
        ReportStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    /// <summary>CSS class for the status pill. Pills always carry a text label as well (NFR-08).</summary>
    public static string CssClass(this ReportStatus status) => "st-" + status.ToString().ToLowerInvariant();

    /// <summary>True while the report still needs work.</summary>
    public static bool IsActive(this ReportStatus status) =>
        status is ReportStatus.Submitted or ReportStatus.UnderReview or ReportStatus.Assigned or ReportStatus.InProgress;

    /// <summary>Position on the five-step progress bar; -1 for cancelled reports.</summary>
    public static int StepIndex(this ReportStatus status) => status switch
    {
        ReportStatus.Submitted => 0,
        ReportStatus.UnderReview => 1,
        ReportStatus.Assigned => 2,
        ReportStatus.InProgress => 3,
        ReportStatus.Resolved => 4,
        ReportStatus.Closed => 4,
        _ => -1
    };

    public static string Label(this ReportPriority priority) => priority.ToString();

    public static string CssClass(this ReportPriority priority) => "pr-" + priority.ToString().ToLowerInvariant();
}
