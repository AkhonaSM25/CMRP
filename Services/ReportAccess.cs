using System.Security.Claims;
using CMRP.Models;

namespace CMRP.Services;

/// <summary>
/// Ownership and role rules, checked on the server for every protected request (US-03, US-07).
/// Reporters see their own reports, technicians see reports assigned to them, coordinators/administrators see all.
/// </summary>
public static class ReportAccess
{
    public static string? UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static bool IsManager(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Coordinator) || user.IsInRole(Roles.Administrator);

    public static bool IsAssignedTechnician(ClaimsPrincipal user, MaintenanceReport report) =>
        user.IsInRole(Roles.Technician)
        && report.AssignedToUserId is not null
        && report.AssignedToUserId == UserId(user);

    public static bool IsOwner(ClaimsPrincipal user, MaintenanceReport report) =>
        report.ReporterId == UserId(user);

    public static bool CanView(ClaimsPrincipal user, MaintenanceReport report) =>
        IsOwner(user, report) || IsManager(user) || IsAssignedTechnician(user, report);
}
