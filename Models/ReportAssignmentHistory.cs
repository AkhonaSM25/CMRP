namespace CMRP.Models;

/// <summary>Who assigned the report to whom, and when. The current assignee is also stored on the report for fast queries.</summary>
public class ReportAssignmentHistory
{
    public int Id { get; set; }

    public int ReportId { get; set; }
    public MaintenanceReport Report { get; set; } = null!;

    public string? FromUserId { get; set; }
    public ApplicationUser? FromUser { get; set; }

    public string ToUserId { get; set; } = string.Empty;
    public ApplicationUser ToUser { get; set; } = null!;

    public string ChangedById { get; set; } = string.Empty;
    public ApplicationUser ChangedBy { get; set; } = null!;

    public DateTime ChangedAt { get; set; }
}
