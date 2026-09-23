using CMRP.Models;
using CMRP.Services;
using CMRP.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Controllers;

/// <summary>User and role management (US-07). Administrators only.</summary>
[Authorize(Roles = Roles.Administrator)]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _users;

    public AdminController(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        ViewData["Nav"] = "users";

        var currentId = CurrentUserId();
        var all = await _users.Users.AsNoTracking().OrderBy(u => u.FullName).ThenBy(u => u.Email).ToListAsync();
        var rows = new List<UserRow>();

        foreach (var u in all)
        {
            var roles = await _users.GetRolesAsync(u);
            rows.Add(new UserRow
            {
                Id = u.Id,
                Name = u.DisplayName,
                Email = u.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "-",
                IsActive = !IsDeactivated(u),
                IsCurrentUser = u.Id == currentId
            });
        }

        return View(new UsersViewModel { Users = rows, RoleNames = Roles.All });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserInput input)
    {
        if (!ModelState.IsValid || !Roles.All.Contains(input.Role))
        {
            TempData["Error"] = "Please fill in every field and choose a valid role. The password needs at least 6 characters.";
            return RedirectToAction(nameof(Users));
        }

        var email = input.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = input.FullName.Trim(),
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users));
        }

        await _users.AddToRoleAsync(user, input.Role);
        TempData["Success"] = $"Account created for {user.DisplayName}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        if (id == CurrentUserId())
        {
            TempData["Error"] = "You cannot change your own role.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _users.FindByIdAsync(id);
        if (user is null || !Roles.All.Contains(role))
        {
            TempData["Error"] = "That user or role could not be found.";
            return RedirectToAction(nameof(Users));
        }

        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current);
        await _users.AddToRoleAsync(user, role);
        await _users.UpdateSecurityStampAsync(user); // forces a fresh sign-in so the new role applies

        TempData["Success"] = $"{user.DisplayName} is now a {role}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        if (id == CurrentUserId())
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "That user could not be found.";
            return RedirectToAction(nameof(Users));
        }

        await _users.SetLockoutEnabledAsync(user, true);
        if (IsDeactivated(user))
        {
            await _users.SetLockoutEndDateAsync(user, null);
            TempData["Success"] = $"{user.DisplayName} can sign in again.";
        }
        else
        {
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _users.UpdateSecurityStampAsync(user); // ends any active session
            TempData["Success"] = $"{user.DisplayName} has been deactivated.";
        }

        return RedirectToAction(nameof(Users));
    }

    private static bool IsDeactivated(ApplicationUser user) =>
        user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private string? CurrentUserId() => ReportAccess.UserId(User);
}
