using CMRP.Models;

namespace CMRP.Services;

/// <summary>
/// Pure business rules for the report lifecycle (Task 2 section 6.3, NFR-09). No database access, so it is easy to unit test.
/// "Assigned" is reached only through the assignment action, because it needs an assignee.
/// </summary>
public static class WorkflowRules
{
    private static readonly Dictionary<ReportStatus, ReportStatus[]> Transitions = new()
    {
        [ReportStatus.Submitted] = new[] { ReportStatus.UnderReview, ReportStatus.Cancelled },
        [ReportStatus.UnderReview] = new[] { ReportStatus.Cancelled },
        [ReportStatus.Assigned] = new[] { ReportStatus.InProgress, ReportStatus.Cancelled },
        [ReportStatus.InProgress] = new[] { ReportStatus.Resolved, ReportStatus.Cancelled },
        [ReportStatus.Resolved] = new[] { ReportStatus.Closed, ReportStatus.InProgress },
        [ReportStatus.Closed] = Array.Empty<ReportStatus>(),
        [ReportStatus.Cancelled] = Array.Empty<ReportStatus>()
    };

    /// <summary>Every status reachable from <paramref name="current"/> by a direct status change.</summary>
    public static IReadOnlyList<ReportStatus> NextStatuses(ReportStatus current) => Transitions[current];

    public static bool IsValidTransition(ReportStatus from, ReportStatus to) => Transitions[from].Contains(to);

    /// <summary>Statuses the caller may move the report to. Technicians can only start and resolve their own jobs.</summary>
    public static IReadOnlyList<ReportStatus> AllowedFor(ReportStatus current, bool isManager, bool isAssignedTechnician)
    {
        if (isManager)
        {
            return Transitions[current];
        }

        if (isAssignedTechnician)
        {
            return Transitions[current].Where(target => TechnicianMaySet(current, target)).ToList();
        }

        return Array.Empty<ReportStatus>();
    }

    private static bool TechnicianMaySet(ReportStatus current, ReportStatus target) =>
        (current == ReportStatus.Assigned && target == ReportStatus.InProgress) ||
        (current == ReportStatus.InProgress && target == ReportStatus.Resolved);

    /// <summary>A report can be (re)assigned while it is still being worked on.</summary>
    public static bool CanAssign(ReportStatus status) =>
        status is ReportStatus.Submitted or ReportStatus.UnderReview or ReportStatus.Assigned or ReportStatus.InProgress;
}
