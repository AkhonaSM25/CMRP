using System.ComponentModel.DataAnnotations;

namespace CMRP.Models;

/// <summary>A maintenance complaint. Holds current state; history lives in the child tables.</summary>
public class MaintenanceReport
{
    public int Id { get; set; }

    /// <summary>Human-friendly, immutable, unique, generated server-side (e.g. CMR-2026-0042).</summary>
    [Required, StringLength(30)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public MaintenanceCategory Category { get; set; } = null!;

    public int LocationId { get; set; }
    public CampusLocation Location { get; set; } = null!;

    /// <summary>Optional free text such as room number or "near the library entrance".</summary>
    [StringLength(150)]
    public string? LocationDetail { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Submitted;
    public ReportPriority Priority { get; set; } = ReportPriority.Normal;

    public string ReporterId { get; set; } = string.Empty;
    public ApplicationUser Reporter { get; set; } = null!;

    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    // All timestamps are UTC.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ICollection<ReportAttachment> Attachments { get; set; } = new List<ReportAttachment>();
    public ICollection<ReportNote> Notes { get; set; } = new List<ReportNote>();
    public ICollection<ReportStatusHistory> StatusHistory { get; set; } = new List<ReportStatusHistory>();
    public ICollection<ReportAssignmentHistory> AssignmentHistory { get; set; } = new List<ReportAssignmentHistory>();
}
