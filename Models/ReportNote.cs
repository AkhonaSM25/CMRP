using System.ComponentModel.DataAnnotations;

namespace CMRP.Models;

/// <summary>Timestamped progress note. Internal notes are hidden from reporters (FR-11).</summary>
public class ReportNote
{
    public int Id { get; set; }

    public int ReportId { get; set; }
    public MaintenanceReport Report { get; set; } = null!;

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    [Required, StringLength(1000)]
    public string Text { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}
