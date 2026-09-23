namespace CMRP.Models;

public static class ReportExtensions
{
    /// <summary>Display title built from incident type and place, e.g. "Water leak (Library - Level 1)". Requires Category and Location to be loaded.</summary>
    public static string ReportTitle(this MaintenanceReport report)
    {
        var place = report.Location.Building;
        if (!string.IsNullOrWhiteSpace(report.LocationDetail))
        {
            place += " - " + report.LocationDetail;
        }
        return $"{report.Category.Name} ({place})";
    }
}
