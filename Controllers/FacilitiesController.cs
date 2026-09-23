using CMRP.Models;
using CMRP.Services;
using CMRP.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CMRP.Data;

namespace CMRP.Controllers;

/// <summary>Operational screens for staff: dashboard, complaints queue, assigned work, analytics and the workflow actions.</summary>
[Authorize(Roles = Roles.AllStaff)]
public class FacilitiesController : Controller
{
    private readonly ReportService _reports;
    private readonly WorkflowService _workflow;
    private readonly DashboardService _dashboard;
    private readonly ApplicationDbContext _db;

    public FacilitiesController(ReportService reports, WorkflowService workflow, DashboardService dashboard, ApplicationDbContext db)
    {
        _reports = reports;
        _workflow = workflow;
        _dashboard = dashboard;
        _db = db;
    }

    // ---- Screens -------------------------------------------------------------------------------

    [HttpGet("Facilities")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Index()
    {
        ViewData["Nav"] = "dashboard";
        var vm = await _dashboard.GetAsync();
        vm.Recent = await _reports.SearchAsync(new ReportQuery { Take = 6 });
        return View(vm);
    }

    [HttpGet("Facilities/Complaints")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Complaints(
        string? q, ReportStatus? status, ReportPriority? priority, int? categoryId, bool unassigned = false, bool active = false)
    {
        // "Update Status" in the sidebar is this same queue, limited to open work and sorted by urgency.
        ViewData["Nav"] = active ? "update" : "complaints";

        var reports = await _reports.SearchAsync(new ReportQuery
        {
            Search = q,
            Status = status,
            Priority = priority,
            CategoryId = categoryId,
            UnassignedOnly = unassigned,
            ActiveOnly = active,
            OrderByPriority = active
        });

        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        return View(new ComplaintsViewModel
        {
            Reports = reports,
            Search = q,
            Status = status,
            Priority = priority,
            CategoryId = categoryId,
            Unassigned = unassigned,
            ActiveOnly = active,
            Categories = categories
        });
    }

    [HttpGet("Facilities/Assigned")]
    [Authorize(Roles = Roles.Technician)]
    public async Task<IActionResult> Assigned(string? q, bool all = false)
    {
        ViewData["Nav"] = "assigned";
        var reports = await _reports.SearchAsync(new ReportQuery
        {
            AssignedToUserId = ReportAccess.UserId(User),
            Search = q,
            ActiveOnly = !all,
            OrderByPriority = true
        });

        return View("~/Views/Reports/List.cshtml", new ReportListViewModel
        {
            Title = "Assigned Work",
            Subtitle = all ? "Everything assigned to you, including finished jobs." : "Jobs assigned to you that still need work.",
            FormAction = nameof(Assigned),
            FormController = "Facilities",
            Search = q,
            ShowProgress = true,
            ShowStatusFilter = false,
            EmptyTitle = "No open jobs",
            EmptyText = "When a coordinator assigns you a report it will appear here.",
            Reports = reports
        });
    }

    [HttpGet("Facilities/Analytics")]
    [Authorize(Roles = Roles.Managers)]
    public async Task<IActionResult> Analytics()
    {
        ViewData["Nav"] = "analytics";
        return View(await _dashboard.GetAsync());
    }

    // ---- Workflow actions (all POST, anti-forgery protected, re-authorised in WorkflowService) --

    [HttpPost("Facilities/Reports/{id:int}/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ReportStatus? newStatus, string? note)
    {
        if (newStatus is null)
        {
            TempData["Error"] = "Please choose the new status.";
            return BackToReport(id);
        }

        if (note is { Length: > 500 })
        {
            TempData["Error"] = "Update notes can be at most 500 characters.";
            return BackToReport(id);
        }

        var result = await _workflow.ChangeStatusAsync(id, newStatus.Value, note, User);
        return Finish(result, "Status updated.", id);
    }

    [HttpPost("Facilities/Reports/{id:int}/Assign")]
    [Authorize(Roles = Roles.Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, string? assigneeId)
    {
        if (string.IsNullOrWhiteSpace(assigneeId))
        {
            TempData["Error"] = "Please choose a technician.";
            return BackToReport(id);
        }

        var result = await _workflow.AssignAsync(id, assigneeId, User);
        return Finish(result, "Report assigned.", id);
    }

    [HttpPost("Facilities/Reports/{id:int}/Priority")]
    [Authorize(Roles = Roles.Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPriority(int id, ReportPriority priority)
    {
        var result = await _workflow.SetPriorityAsync(id, priority, User);
        return Finish(result, "Priority updated.", id);
    }

    [HttpPost("Facilities/Reports/{id:int}/Notes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string? text, bool isInternal = false)
    {
        var result = await _workflow.AddNoteAsync(id, text ?? string.Empty, isInternal, User);
        return Finish(result, "Note added.", id);
    }

    private IActionResult Finish(OperationResult result, string successMessage, int id)
    {
        if (result.Succeeded) TempData["Success"] = successMessage;
        else TempData["Error"] = result.Error;
        return BackToReport(id);
    }

    private IActionResult BackToReport(int id) => RedirectToAction("Details", "Reports", new { id });
}
