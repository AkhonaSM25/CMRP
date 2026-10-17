using CMRP.Models;
using CMRP.Services;

namespace CMRP.Tests;

/// <summary>Ownership and role rules (US-03, US-07): who may see or touch which report.</summary>
public class ReportAccessTests
{
    [Fact]
    public void UserId_ReadsTheNameIdentifierClaim() =>
        Assert.Equal("u1", ReportAccess.UserId(TestUser.Principal("u1")));

    [Fact]
    public void UserId_IsNullWhenNotSignedIn() =>
        Assert.Null(ReportAccess.UserId(TestUser.Anonymous()));

    [Theory]
    [InlineData(Roles.Coordinator, true)]
    [InlineData(Roles.Administrator, true)]
    [InlineData(Roles.Technician, false)]
    [InlineData(Roles.Reporter, false)]
    public void IsManager_OnlyCoordinatorsAndAdministrators(string role, bool expected) =>
        Assert.Equal(expected, ReportAccess.IsManager(TestUser.Principal("u1", role)));

    [Fact]
    public void IsAssignedTechnician_TrueForTheAssignee()
    {
        var report = Make.Report(assignedTo: "tech-1");
        Assert.True(ReportAccess.IsAssignedTechnician(TestUser.Principal("tech-1", Roles.Technician), report));
    }

    [Fact]
    public void IsAssignedTechnician_FalseForAnotherTechnician()
    {
        var report = Make.Report(assignedTo: "tech-1");
        Assert.False(ReportAccess.IsAssignedTechnician(TestUser.Principal("tech-2", Roles.Technician), report));
    }

    [Fact]
    public void IsAssignedTechnician_FalseWhenReportIsUnassigned()
    {
        var report = Make.Report(assignedTo: null);
        Assert.False(ReportAccess.IsAssignedTechnician(TestUser.Principal("tech-1", Roles.Technician), report));
    }

    [Fact]
    public void IsAssignedTechnician_FalseWhenUserLacksTheTechnicianRole()
    {
        // Same id as the assignee, but the account is not a technician (for example the role was removed).
        var report = Make.Report(assignedTo: "u1");
        Assert.False(ReportAccess.IsAssignedTechnician(TestUser.Principal("u1", Roles.Reporter), report));
    }

    [Fact]
    public void IsOwner_TrueForTheReporter() =>
        Assert.True(ReportAccess.IsOwner(TestUser.Principal("reporter-1", Roles.Reporter), Make.Report()));

    [Fact]
    public void IsOwner_FalseForAnotherReporter() =>
        Assert.False(ReportAccess.IsOwner(TestUser.Principal("reporter-2", Roles.Reporter), Make.Report()));

    [Fact]
    public void CanView_OwnerCanSeeTheirOwnReport() =>
        Assert.True(ReportAccess.CanView(TestUser.Principal("reporter-1", Roles.Reporter), Make.Report()));

    [Fact]
    public void CanView_ReporterCannotSeeSomeoneElsesReport() =>
        Assert.False(ReportAccess.CanView(TestUser.Principal("reporter-2", Roles.Reporter), Make.Report()));

    [Theory]
    [InlineData(Roles.Coordinator)]
    [InlineData(Roles.Administrator)]
    public void CanView_ManagersCanSeeEveryReport(string role) =>
        Assert.True(ReportAccess.CanView(TestUser.Principal("someone", role), Make.Report()));

    [Fact]
    public void CanView_TechnicianSeesOnlyAssignedReports()
    {
        var tech = TestUser.Principal("tech-1", Roles.Technician);
        Assert.True(ReportAccess.CanView(tech, Make.Report(assignedTo: "tech-1")));
        Assert.False(ReportAccess.CanView(tech, Make.Report(assignedTo: "tech-2")));
        Assert.False(ReportAccess.CanView(tech, Make.Report(assignedTo: null)));
    }

    [Fact]
    public void CanView_AnonymousUserSeesNothing() =>
        Assert.False(ReportAccess.CanView(TestUser.Anonymous(), Make.Report()));
}
