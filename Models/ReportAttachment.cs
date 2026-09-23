using System.ComponentModel.DataAnnotations;

namespace CMRP.Models;

/// <summary>Metadata for an uploaded photo. The file itself lives outside wwwroot and is streamed only after an access check.</summary>
public class ReportAttachment
{
    public int Id { get; set; }

    public int ReportId { get; set; }
    public MaintenanceReport Report { get; set; } = null!;

    /// <summary>Server-generated file name. Never derived from the client file name.</summary>
    [Required, StringLength(80)]
    public string StoredName { get; set; } = string.Empty;

    /// <summary>Client file name, kept as metadata only.</summary>
    [StringLength(200)]
    public string OriginalName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}
