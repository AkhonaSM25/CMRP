using System.ComponentModel.DataAnnotations;

namespace CMRP.ViewModels;

public class UserRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class CreateUserInput
{
    [Required(ErrorMessage = "Please enter the full name.")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter an email address."), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a temporary password."), StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a role.")]
    public string Role { get; set; } = string.Empty;
}

public class UsersViewModel
{
    public List<UserRow> Users { get; set; } = new();
    public string[] RoleNames { get; set; } = Array.Empty<string>();
}
