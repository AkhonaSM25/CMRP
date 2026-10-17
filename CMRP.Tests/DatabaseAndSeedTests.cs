using CMRP.Data;
using CMRP.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CMRP.Tests;

/// <summary>Database rules (unique references, no cascade deletes) and the seed routine (NFR-10).</summary>
public sealed class DatabaseAndSeedTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task ReferenceNumbersAreUnique()
    {
        var user = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var first = await _t.AddReportAsync(user.Id);
        var duplicate = await _t.AddReportAsync(user.Id);
        duplicate.ReferenceNumber = first.ReferenceNumber;

        await Assert.ThrowsAsync<DbUpdateException>(() => _t.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task CategoryNamesAreUnique()
    {
        _t.Db.Categories.Add(new MaintenanceCategory { Name = "Water leak" }); // already seeded by the fixture

        await Assert.ThrowsAsync<DbUpdateException>(() => _t.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task LocationsAreUniquePerCampusAndBuilding()
    {
        _t.Db.Locations.Add(new CampusLocation { Campus = "Howard College", Building = "Library" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _t.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task AReporterWhoOwnsReportsCannotBeDeleted()
    {
        // History is never lost by cascade (Task 3 section 4.4): every relationship is Restrict.
        var user = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        await _t.AddReportAsync(user.Id);

        await Record.ExceptionAsync(() => _t.Users.DeleteAsync(user));

        // Whether the database or EF refuses, the account and its report must both still be there.
        Assert.NotNull(await _t.Db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == user.Id));
        Assert.Equal(1, await _t.Db.Reports.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ACategoryInUseCannotBeDeleted()
    {
        var user = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        await _t.AddReportAsync(user.Id);
        var category = await _t.Db.Categories.SingleAsync();

        // EF may refuse at Remove() (the report is tracked) or at SaveChanges (the database FK is Restrict).
        var error = await Record.ExceptionAsync(async () =>
        {
            _t.Db.Categories.Remove(category);
            await _t.Db.SaveChangesAsync();
        });

        Assert.NotNull(error);
        Assert.Equal(1, await _t.Db.Categories.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task NewReportsDefaultToSubmittedAndNormal()
    {
        var user = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);

        var report = await _t.AddReportAsync(user.Id);

        Assert.Equal(ReportStatus.Submitted, report.Status);
        Assert.Equal(ReportPriority.Normal, report.Priority);
    }

    // ---- Seed routine ----

    [Fact]
    public async Task Seed_CreatesTheFourRolesAndReferenceData()
    {
        using var seeded = new SeededDb();

        await SeedData.InitializeAsync(seeded.Provider, demoAccounts: false, demoReports: false);

        var roleManager = seeded.Provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            Assert.True(await roleManager.RoleExistsAsync(role), role);
        }

        var db = seeded.Provider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(12, await db.Categories.CountAsync());
        Assert.True(await db.Locations.CountAsync() > 10);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Reports.CountAsync());
    }

    [Fact]
    public async Task Seed_DemoAccountsCanBeSwitchedOnWithTheRightRoles()
    {
        using var seeded = new SeededDb();

        await SeedData.InitializeAsync(seeded.Provider, demoAccounts: true, demoReports: false);

        var users = seeded.Provider.GetRequiredService<UserManager<ApplicationUser>>();
        var tech = await users.FindByEmailAsync(SeedData.TechnicianEmail);
        var admin = await users.FindByEmailAsync(SeedData.AdminEmail);
        Assert.NotNull(tech);
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(tech!, Roles.Technician));
        Assert.True(await users.IsInRoleAsync(admin!, Roles.Administrator));
        Assert.True(await users.CheckPasswordAsync(tech!, SeedData.DemoPassword));
    }

    [Fact]
    public async Task Seed_CanRunTwiceWithoutDuplicates()
    {
        using var seeded = new SeededDb();

        await SeedData.InitializeAsync(seeded.Provider, demoAccounts: true, demoReports: true);
        var db = seeded.Provider.GetRequiredService<ApplicationDbContext>();
        var users = await db.Users.CountAsync();
        var reports = await db.Reports.CountAsync();
        var categories = await db.Categories.CountAsync();

        await SeedData.InitializeAsync(seeded.Provider, demoAccounts: true, demoReports: true);

        Assert.Equal(6, users);
        Assert.Equal(users, await db.Users.CountAsync());
        Assert.Equal(reports, await db.Reports.CountAsync());
        Assert.Equal(categories, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task Seed_DemoReportsHaveUniqueReferences()
    {
        using var seeded = new SeededDb();

        await SeedData.InitializeAsync(seeded.Provider, demoAccounts: true, demoReports: true);

        var db = seeded.Provider.GetRequiredService<ApplicationDbContext>();
        var references = await db.Reports.Select(r => r.ReferenceNumber).ToListAsync();
        Assert.NotEmpty(references);
        Assert.Equal(references.Count, references.Distinct().Count());
    }

    /// <summary>Service provider with the same Identity set-up as Program.cs, over a fresh SQLite in-memory database.</summary>
    private sealed class SeededDb : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = new("DataSource=:memory:");
        private readonly ServiceProvider _root;
        private readonly IServiceScope _scope;

        public IServiceProvider Provider => _scope.ServiceProvider;

        public SeededDb()
        {
            _connection.Open();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            _root = services.BuildServiceProvider();
            _scope = _root.CreateScope();
            _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _root.Dispose();
            _connection.Dispose();
        }
    }
}

/// <summary>The dashboard service reading real rows (as opposed to the pure maths tested elsewhere).</summary>
public sealed class DashboardServiceDatabaseTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Get_SummarisesTheStoredReports()
    {
        var reporter = await _t.AddUserAsync("r@cmrp.test", Roles.Reporter);
        var tech = await _t.AddUserAsync("t@cmrp.test", Roles.Technician);
        var now = DateTime.UtcNow;

        await _t.AddReportAsync(reporter.Id, ReportStatus.Submitted);
        await _t.AddReportAsync(reporter.Id, ReportStatus.InProgress, assignedTo: tech.Id);
        await _t.AddReportAsync(reporter.Id, ReportStatus.Resolved, assignedTo: tech.Id,
            createdAt: now.AddDays(-3), resolvedAt: now.AddDays(-1));

        var vm = await _t.Dashboard.GetAsync();

        Assert.Equal(3, vm.TotalIncidents);
        Assert.Equal(1, vm.InProgress);
        Assert.Equal(1, vm.Resolved);
        Assert.Equal(1, vm.AwaitingAssignment);
        Assert.Equal(2.0, vm.AverageFixDays);
        Assert.Equal("Water leak", vm.TopCategories[0].Label);
        Assert.Equal("Howard College", vm.ByCampus[0].Label);
    }

    [Fact]
    public async Task Get_WorksOnAnEmptyDatabase()
    {
        var vm = await _t.Dashboard.GetAsync();

        Assert.Equal(0, vm.TotalIncidents);
        Assert.Null(vm.AverageFixDays);
    }
}
