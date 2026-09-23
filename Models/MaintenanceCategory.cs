using System.ComponentModel.DataAnnotations;

namespace CMRP.Models;

/// <summary>Reference data: the "incident type" a reporter chooses.</summary>
public class MaintenanceCategory
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
