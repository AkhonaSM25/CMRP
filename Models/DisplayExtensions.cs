namespace CMRP.Models;

public static class DisplayExtensions
{
    // South Africa Standard Time is UTC+2 all year (no daylight saving).
    private static readonly TimeSpan SastOffset = TimeSpan.FromHours(2);

    /// <summary>Times are stored in UTC (Task 3 section 4.3) and shown in SAST.</summary>
    public static string Sast(this DateTime utc, string format = "dd MMM yyyy, HH:mm") =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(SastOffset).ToString(format);

    public static string Sast(this DateTime? utc, string format = "dd MMM yyyy, HH:mm") =>
        utc.HasValue ? utc.Value.Sast(format) : "-";

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
