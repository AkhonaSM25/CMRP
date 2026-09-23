using System.Diagnostics;
using CMRP.Models;
using CMRP.Services;
using CMRP.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CMRP.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ReportService _reports;
    private readonly UserManager<ApplicationUser> _users;

    public HomeController(ReportService reports, UserManager<ApplicationUser> users)
    {
        _reports = reports;
        _users = users;
    }

    /// <summary>Reporters land on their own home page; staff go straight to their working screens.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (ReportAccess.IsManager(User)) return RedirectToAction("Index", "Facilities");
        if (User.IsInRole(Roles.Technician)) return RedirectToAction("Assigned", "Facilities");

        var userId = ReportAccess.UserId(User)!;
        var me = await _users.GetUserAsync(User);
        var mine = await _reports.SearchAsync(new ReportQuery { ReporterId = userId, Take = 200 });

        var vm = new ReporterHomeViewModel
        {
            FirstName = (me?.DisplayName ?? "there").Split(' ')[0],
            Total = mine.Count,
            Open = mine.Count(r => r.Status.IsActive()),
            Resolved = mine.Count(r => r.Status == ReportStatus.Resolved || r.Status == ReportStatus.Closed),
            Recent = mine.Take(4).ToList()
        };

        return View(vm);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
