using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace CMRP.Models;

/// <summary>Application account. Extends ASP.NET Core Identity with a display name only (NFR-04: collect minimal data).</summary>
public class ApplicationUser : IdentityUser
{
    [StringLength(100)]
    public string? FullName { get; set; }

    [NotMapped]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FullName) ? FullName : (UserName ?? Email ?? "User");
}
