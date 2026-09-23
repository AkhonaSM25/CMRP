using CMRP.Data;
using CMRP.Models;
using CMRP.Services;
using CMRP.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ReportService _reports;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ReportsController(ReportService reports, ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _reports = reports;
        _db = db;
        _users = users;
    }

    // ---- Create a report (FR-02, FR-03, FR-04) -------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Nav"] = "report";
        return View(await PopulateAsync(new CreateReportViewModel()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReportViewModel model)
    {
        ViewData["Nav"] = "report";

        if (ModelState.IsValid)
        {
            var result = await _reports.CreateAsync(ReportAccess.UserId(User)!, model);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Confirmation), new { id = result.ReportId });
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "We could not save your report.");
        }

        return View(await PopulateAsync(model));
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var report = await _reports.GetDetailsAsync(id);
        if (report is null) return NotFound();
        if (!ReportAccess.IsOwner(User, report)) return Forbid();

        ViewData["Nav"] = "report";
        return View(report);
    }

    // ---- Reporter lists (FR-05) ----------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> My(string? q, ReportStatus? status)
    {
        ViewData["Nav"] = "my";
        var reports = await _reports.SearchAsync(new ReportQuery
        {
            ReporterId = ReportAccess.UserId(User),
            Search = q,
            Status = status
        });

        return View("List", new ReportListViewModel
        {
            Title = "My Reports",
            Subtitle = "Every incident you have reported.",
            FormAction = nameof(My),
            Search = q,
            Status = status,
            EmptyTitle = "No reports found",
            EmptyText = "Reports you submit will appear here.",
            Reports = reports
        });
    }

    [HttpGet]
    public async Task<IActionResult> Track(string? q)
    {
        ViewData["Nav"] = "track";
        var reports = await _reports.SearchAsync(new ReportQuery
        {
            ReporterId = ReportAccess.UserId(User),
            Search = q,
            ActiveOnly = true
        });

        return View("List", new ReportListViewModel
        {
            Title = "Track Your Report",
            Subtitle = "View the status and progress of your submitted incidents.",
            FormAction = nameof(Track),
            Search = q,
            ShowProgress = true,
            ShowStatusFilter = false,
            EmptyTitle = "Nothing in progress",
            EmptyText = "Reports that are still being worked on appear here. Resolved reports are listed under My Reports.",
            Reports = reports
        });
    }

    // ---- Report detail (owner, assigned technician or coordinator/administrator) ---------------

    [HttpGet]
    public async Task<IActionResult> Details(int id, bool update = false)
    {
        var report = await _reports.GetDetailsAsync(id);
        if (report is null) return NotFound();

        // Ownership and role are re-checked on every request. Guessing an ID never grants access (US-03).
        if (!ReportAccess.CanView(User, report)) return Forbid();

        var isManager = ReportAccess.IsManager(User);
        var isTechnician = ReportAccess.IsAssignedTechnician(User, report);

        var vm = new ReportDetailsViewModel
        {
            Report = report,
            IsOwner = ReportAccess.IsOwner(User, report),
            IsManager = isManager,
            IsAssignedTechnician = isTechnician,
            AllowedStatuses = WorkflowRules.AllowedFor(report.Status, isManager, isTechnician).ToList(),
            CanAssign = isManager && WorkflowRules.CanAssign(report.Status),
            CanSetPriority = isManager,
            CanAddNote = isManager || isTechnician,
            Timeline = ReportDetailsViewModel.BuildTimeline(report, isManager || isTechnician)
        };
        vm.OpenUpdatePanel = update && vm.CanUpdateStatus;

        if (vm.CanAssign)
        {
            var technicians = await _users.GetUsersInRoleAsync(Roles.Technician);
            vm.Technicians = technicians
                .OrderBy(t => t.DisplayName)
                .Select(t => new SelectListItem(t.DisplayName, t.Id, t.Id == report.AssignedToUserId))
                .ToList();
        }

        ViewData["Nav"] = isManager ? "complaints" : (isTechnician ? "assigned" : "my");
        return View(vm);
    }

    private async Task<CreateReportViewModel> PopulateAsync(CreateReportViewModel model)
    {
        model.Categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        model.Locations = await _db.Locations
            .AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Campus)
            .ThenBy(l => l.Building)
            .ToListAsync();

        return model;
    }
}
