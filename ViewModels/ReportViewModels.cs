using System.ComponentModel.DataAnnotations;
using CMRP.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CMRP.ViewModels;

/// <summary>The "Report Incident" form (FR-02, FR-03). Description is capped at 500 characters.</summary>
public class CreateReportViewModel
{
    [Required(ErrorMessage = "Please select an incident type.")]
    [Display(Name = "Incident type")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Please select a building or area.")]
    [Display(Name = "Building / Area")]
    public int? LocationId { get; set; }

    [StringLength(150)]
    [Display(Name = "More specific location (optional)")]
    public string? LocationDetail { get; set; }

    [Required(ErrorMessage = "Please describe the incident.")]
    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Add a photo (optional)")]
    public IFormFile? Photo { get; set; }

    // Lists used only to draw the form; never posted back.
    [ValidateNever]
    public List<SelectListItem> Categories { get; set; } = new();

    [ValidateNever]
    public List<CampusLocation> Locations { get; set; } = new();
}

/// <summary>One report shown as a card (My Reports, Track Progress, Assigned Work).</summary>
public record ReportCardModel(MaintenanceReport Report, bool ShowProgress = false);

public class ReportListViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Controller action the search form submits to.</summary>
    public string FormAction { get; set; } = "My";

    public string FormController { get; set; } = "Reports";
    public string? Search { get; set; }
    public ReportStatus? Status { get; set; }
    public bool ShowProgress { get; set; }
    public bool ShowStatusFilter { get; set; } = true;
    public string EmptyTitle { get; set; } = "No reports found";
    public string EmptyText { get; set; } = string.Empty;
    public List<MaintenanceReport> Reports { get; set; } = new();
}

public class ReporterHomeViewModel
{
    public string FirstName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Open { get; set; }
    public int Resolved { get; set; }
    public List<MaintenanceReport> Recent { get; set; } = new();
}

/// <summary>One line on the report timeline (status change, assignment or note).</summary>
public record TimelineItem(DateTime When, string Who, string Title, string? Text, bool IsInternal, string Kind);

public class ReportDetailsViewModel
{
    public MaintenanceReport Report { get; set; } = null!;
    public bool IsOwner { get; set; }
    public bool IsManager { get; set; }
    public bool IsAssignedTechnician { get; set; }
    public bool IsStaffView => IsManager || IsAssignedTechnician;

    public List<ReportStatus> AllowedStatuses { get; set; } = new();
    public bool CanUpdateStatus => AllowedStatuses.Count > 0;
    public bool CanAssign { get; set; }
    public bool CanSetPriority { get; set; }
    public bool CanAddNote { get; set; }
    public bool OpenUpdatePanel { get; set; }

    public List<SelectListItem> Technicians { get; set; } = new();
    public List<TimelineItem> Timeline { get; set; } = new();

    /// <summary>Builds the merged timeline. Reporters never see internal notes or technician names.</summary>
    public static List<TimelineItem> BuildTimeline(MaintenanceReport report, bool staffView)
    {
        var items = new List<TimelineItem>();

        foreach (var h in report.StatusHistory)
        {
            var title = h.FromStatus is null
                ? "Report submitted"
                : $"Status changed from {h.FromStatus.Value.Label()} to {h.ToStatus.Label()}";
            items.Add(new TimelineItem(h.ChangedAt, h.ChangedBy?.DisplayName ?? "System", title, null, false, "status"));
        }

        foreach (var a in report.AssignmentHistory)
        {
            var title = staffView
                ? $"Assigned to {a.ToUser?.DisplayName ?? "a technician"}"
                : "Assigned to the maintenance team";
            items.Add(new TimelineItem(a.ChangedAt, "Facilities", title, null, false, "assign"));
        }

        foreach (var n in report.Notes.Where(n => staffView || !n.IsInternal))
        {
            items.Add(new TimelineItem(n.CreatedAt, n.Author?.DisplayName ?? "Staff", "Note", n.Text, n.IsInternal, "note"));
        }

        return items.OrderBy(i => i.When).ToList();
    }
}
