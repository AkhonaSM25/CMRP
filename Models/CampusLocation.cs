using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMRP.Models;

/// <summary>Reference data: campus plus building/area.</summary>
public class CampusLocation
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Campus { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Building { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [NotMapped]
    public string DisplayName => $"{Campus} - {Building}";
}
