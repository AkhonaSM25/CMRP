namespace CMRP.Models;

/// <summary>Immutable audit trail of status transitions (FR-14). Rows are only ever added.</summary>
public class ReportStatusHistory
{
    public int Id { get; set; }

    public int ReportId { get; set; }
    public MaintenanceReport Report { get; set; } = null!;

    public string ChangedById { get; set; } = string.Empty;
    public ApplicationUser ChangedBy { get; set; } = null!;

    /// <summary>Null for the initial "submitted" entry.</summary>
    public ReportStatus? FromStatus { get; set; }
    public ReportStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; }
}
