using System.Security.Claims;
using CMRP.Data;
using CMRP.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Services;

/// <summary>
/// Status changes, assignment, priority and notes. Every state-changing call follows the same order (Task 3 section 6.4):
/// authorise role/ownership, validate the workflow rule, persist, append the history record.
/// </summary>
public class WorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public WorkflowService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<OperationResult> ChangeStatusAsync(int reportId, ReportStatus target, string? note, ClaimsPrincipal user)
    {
        var userId = ReportAccess.UserId(user);
        if (userId is null) return OperationResult.Fail("You need to sign in again.");

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId);
        if (report is null) return OperationResult.Fail("That report no longer exists.");

        var allowed = WorkflowRules.AllowedFor(
            report.Status, ReportAccess.IsManager(user), ReportAccess.IsAssignedTechnician(user, report));

        if (!allowed.Contains(target))
        {
            return OperationResult.Fail(
                $"You cannot change a report from {report.Status.Label()} to {target.Label()}.");
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (target == ReportStatus.Cancelled && note is null)
        {
            return OperationResult.Fail("Please add a note explaining why the report is being cancelled.");
        }

        var now = DateTime.UtcNow;
        var from = report.Status;

        _db.StatusHistory.Add(new ReportStatusHistory
        {
            ReportId = report.Id,
            ChangedById = userId,
            FromStatus = from,
            ToStatus = target,
            ChangedAt = now
        });

        report.Status = target;
        report.UpdatedAt = now;

        if (target == ReportStatus.Resolved)
        {
            report.ResolvedAt = now;
        }
        else if (from == ReportStatus.Resolved && target == ReportStatus.InProgress)
        {
            report.ResolvedAt = null; // reopened
        }

        if (note is not null)
        {
            _db.Notes.Add(new ReportNote
            {
                ReportId = report.Id,
                AuthorId = userId,
                Text = note,
                IsInternal = false,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> AssignAsync(int reportId, string assigneeId, ClaimsPrincipal user)
    {
        var userId = ReportAccess.UserId(user);
        if (userId is null) return OperationResult.Fail("You need to sign in again.");
        if (!ReportAccess.IsManager(user)) return OperationResult.Fail("Only a coordinator can assign reports.");

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId);
        if (report is null) return OperationResult.Fail("That report no longer exists.");
        if (!WorkflowRules.CanAssign(report.Status))
        {
            return OperationResult.Fail($"A {report.Status.Label().ToLowerInvariant()} report cannot be assigned.");
        }

        var assignee = await _users.FindByIdAsync(assigneeId);
        if (assignee is null || !await _users.IsInRoleAsync(assignee, Roles.Technician))
        {
            return OperationResult.Fail("Please choose a technician.");
        }

        if (report.AssignedToUserId == assignee.Id)
        {
            return OperationResult.Fail($"This report is already assigned to {assignee.DisplayName}.");
        }

        var now = DateTime.UtcNow;

        _db.AssignmentHistory.Add(new ReportAssignmentHistory
        {
            ReportId = report.Id,
            FromUserId = report.AssignedToUserId,
            ToUserId = assignee.Id,
            ChangedById = userId,
            ChangedAt = now
        });

        report.AssignedToUserId = assignee.Id;
        report.UpdatedAt = now;

        // Assigning a new report also means it has been reviewed.
        if (report.Status is ReportStatus.Submitted or ReportStatus.UnderReview)
        {
            _db.StatusHistory.Add(new ReportStatusHistory
            {
                ReportId = report.Id,
                ChangedById = userId,
                FromStatus = report.Status,
                ToStatus = ReportStatus.Assigned,
                ChangedAt = now
            });
            report.Status = ReportStatus.Assigned;
        }

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetPriorityAsync(int reportId, ReportPriority priority, ClaimsPrincipal user)
    {
        var userId = ReportAccess.UserId(user);
        if (userId is null) return OperationResult.Fail("You need to sign in again.");
        if (!ReportAccess.IsManager(user)) return OperationResult.Fail("Only a coordinator can change priority.");
        if (!Enum.IsDefined(priority)) return OperationResult.Fail("Please choose a valid priority.");

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId);
        if (report is null) return OperationResult.Fail("That report no longer exists.");
        if (report.Priority == priority) return OperationResult.Ok();

        var now = DateTime.UtcNow;

        // Recorded as an internal note so the change is attributable and timestamped (FR-14).
        _db.Notes.Add(new ReportNote
        {
            ReportId = report.Id,
            AuthorId = userId,
            Text = $"Priority changed from {report.Priority.Label()} to {priority.Label()}.",
            IsInternal = true,
            CreatedAt = now
        });

        report.Priority = priority;
        report.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> AddNoteAsync(int reportId, string text, bool isInternal, ClaimsPrincipal user)
    {
        var userId = ReportAccess.UserId(user);
        if (userId is null) return OperationResult.Fail("You need to sign in again.");

        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0) return OperationResult.Fail("Please write a note before saving.");
        if (text.Length > 1000) return OperationResult.Fail("Notes can be at most 1000 characters.");

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId);
        if (report is null) return OperationResult.Fail("That report no longer exists.");

        if (!ReportAccess.IsManager(user) && !ReportAccess.IsAssignedTechnician(user, report))
        {
            return OperationResult.Fail("You can only add notes to reports assigned to you.");
        }

        var now = DateTime.UtcNow;
        _db.Notes.Add(new ReportNote
        {
            ReportId = report.Id,
            AuthorId = userId,
            Text = text,
            IsInternal = isInternal,
            CreatedAt = now
        });
        report.UpdatedAt = now;

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }
}
