using CMRP.Models;
using CMRP.Services;

namespace CMRP.Tests;

/// <summary>Unit tests for the report lifecycle rules (Task 2 section 6.3).</summary>
public class WorkflowRulesTests
{
    public static IEnumerable<object[]> AllStatuses() =>
        Enum.GetValues<ReportStatus>().Select(s => new object[] { s });

    [Theory]
    [InlineData(ReportStatus.Submitted, ReportStatus.UnderReview)]
    [InlineData(ReportStatus.Submitted, ReportStatus.Cancelled)]
    [InlineData(ReportStatus.UnderReview, ReportStatus.Cancelled)]
    [InlineData(ReportStatus.Assigned, ReportStatus.InProgress)]
    [InlineData(ReportStatus.Assigned, ReportStatus.Cancelled)]
    [InlineData(ReportStatus.InProgress, ReportStatus.Resolved)]
    [InlineData(ReportStatus.InProgress, ReportStatus.Cancelled)]
    [InlineData(ReportStatus.Resolved, ReportStatus.Closed)]
    [InlineData(ReportStatus.Resolved, ReportStatus.InProgress)]
    public void IsValidTransition_AllowsPermittedMoves(ReportStatus from, ReportStatus to) =>
        Assert.True(WorkflowRules.IsValidTransition(from, to));

    [Theory]
    [InlineData(ReportStatus.Submitted, ReportStatus.Assigned)]
    [InlineData(ReportStatus.Submitted, ReportStatus.InProgress)]
    [InlineData(ReportStatus.Submitted, ReportStatus.Resolved)]
    [InlineData(ReportStatus.Submitted, ReportStatus.Closed)]
    [InlineData(ReportStatus.UnderReview, ReportStatus.InProgress)]
    [InlineData(ReportStatus.Assigned, ReportStatus.Resolved)]
    [InlineData(ReportStatus.InProgress, ReportStatus.Closed)]
    [InlineData(ReportStatus.Resolved, ReportStatus.Cancelled)]
    [InlineData(ReportStatus.Closed, ReportStatus.InProgress)]
    [InlineData(ReportStatus.Closed, ReportStatus.Submitted)]
    [InlineData(ReportStatus.Cancelled, ReportStatus.Submitted)]
    [InlineData(ReportStatus.Cancelled, ReportStatus.InProgress)]
    public void IsValidTransition_RejectsForbiddenMoves(ReportStatus from, ReportStatus to) =>
        Assert.False(WorkflowRules.IsValidTransition(from, to));

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void IsValidTransition_NeverAllowsMovingToTheSameStatus(ReportStatus status) =>
        Assert.False(WorkflowRules.IsValidTransition(status, status));

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void NextStatuses_IsDefinedForEveryStatus(ReportStatus status) =>
        Assert.NotNull(WorkflowRules.NextStatuses(status));

    [Theory]
    [InlineData(ReportStatus.Closed)]
    [InlineData(ReportStatus.Cancelled)]
    public void TerminalStatuses_HaveNoNextStatus(ReportStatus status) =>
        Assert.Empty(WorkflowRules.NextStatuses(status));

    [Fact]
    public void AssignedStatus_IsNeverReachableByAStatusChange()
    {
        // "Assigned" needs an assignee, so it may only be reached through the Assign action.
        foreach (var status in Enum.GetValues<ReportStatus>())
        {
            Assert.DoesNotContain(ReportStatus.Assigned, WorkflowRules.NextStatuses(status));
        }
    }

    [Fact]
    public void AllowedFor_Manager_GetsEveryValidTransition()
    {
        var allowed = WorkflowRules.AllowedFor(ReportStatus.Resolved, isManager: true, isAssignedTechnician: false);
        Assert.Equal(new[] { ReportStatus.InProgress, ReportStatus.Closed }, allowed.OrderBy(s => s));
    }

    [Fact]
    public void AllowedFor_AssignedTechnician_CanStartWork()
    {
        var allowed = WorkflowRules.AllowedFor(ReportStatus.Assigned, false, true);
        Assert.Equal(new[] { ReportStatus.InProgress }, allowed);
    }

    [Fact]
    public void AllowedFor_AssignedTechnician_CanResolveWork()
    {
        var allowed = WorkflowRules.AllowedFor(ReportStatus.InProgress, false, true);
        Assert.Equal(new[] { ReportStatus.Resolved }, allowed);
    }

    [Theory]
    [InlineData(ReportStatus.Submitted)]
    [InlineData(ReportStatus.UnderReview)]
    [InlineData(ReportStatus.Resolved)]
    [InlineData(ReportStatus.Closed)]
    [InlineData(ReportStatus.Cancelled)]
    public void AllowedFor_AssignedTechnician_HasNothingElseToDo(ReportStatus current) =>
        Assert.Empty(WorkflowRules.AllowedFor(current, false, true));

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void AllowedFor_TechnicianCanNeverCancel(ReportStatus current) =>
        Assert.DoesNotContain(ReportStatus.Cancelled, WorkflowRules.AllowedFor(current, false, true));

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void AllowedFor_SomeoneWithNoRights_GetsNothing(ReportStatus current) =>
        Assert.Empty(WorkflowRules.AllowedFor(current, isManager: false, isAssignedTechnician: false));

    [Theory]
    [InlineData(ReportStatus.Submitted)]
    [InlineData(ReportStatus.UnderReview)]
    [InlineData(ReportStatus.Assigned)]
    [InlineData(ReportStatus.InProgress)]
    public void CanAssign_IsTrueWhileWorkIsOpen(ReportStatus status) =>
        Assert.True(WorkflowRules.CanAssign(status));

    [Theory]
    [InlineData(ReportStatus.Resolved)]
    [InlineData(ReportStatus.Closed)]
    [InlineData(ReportStatus.Cancelled)]
    public void CanAssign_IsFalseOnceWorkIsFinished(ReportStatus status) =>
        Assert.False(WorkflowRules.CanAssign(status));
}
