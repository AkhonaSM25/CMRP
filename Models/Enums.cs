namespace CMRP.Models;

/// <summary>Controlled report lifecycle (Task 2 section 3.2, Task 3 section 3.2).</summary>
public enum ReportStatus
{
    Submitted = 0,
    UnderReview = 1,
    Assigned = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6
}

/// <summary>Priority model (Task 2 section 2.4). Only Coordinators/Administrators may change it.</summary>
public enum ReportPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
