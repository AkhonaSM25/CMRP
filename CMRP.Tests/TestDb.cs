using CMRP.Data;
using CMRP.Models;
using CMRP.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CMRP.Tests;

/// <summary>
/// A throw-away SQLite in-memory database with the real EF model, Identity managers and the real services.
/// Each test builds its own instance, so tests never affect each other.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly string _uploadDir;

    public ApplicationDbContext Db { get; }
    public UserManager<ApplicationUser> Users { get; }
    public RoleManager<IdentityRole> RolesManager { get; }
    public AttachmentService Attachments { get; }
    public ReportService Reports { get; }
    public WorkflowService Workflow { get; }
    public DashboardService Dashboard { get; }
    public string UploadDir => _uploadDir;

    public int CategoryId { get; private set; }
    public int LocationId { get; private set; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        Db = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Db.Database.EnsureCreated();
        Users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        RolesManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        _uploadDir = Path.Combine(Path.GetTempPath(), "cmrp-tests-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:Path"] = _uploadDir })
            .Build();

        Attachments = new AttachmentService(config, new FakeEnvironment());
        Reports = new ReportService(Db, Attachments, NullLogger<ReportService>.Instance);
        Workflow = new WorkflowService(Db, Users);
        Dashboard = new DashboardService(Db);

        foreach (var role in Roles.All)
        {
            RolesManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }

        var category = new MaintenanceCategory { Name = "Water leak" };
        var location = new CampusLocation { Campus = "Howard College", Building = "Library" };
        Db.Categories.Add(category);
        Db.Locations.Add(location);
        Db.SaveChanges();
        CategoryId = category.Id;
        LocationId = location.Id;
    }

    public async Task<ApplicationUser> AddUserAsync(string email, string role, string? fullName = null)
    {
        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName ?? email };
        var created = await Users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        await Users.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>Inserts a report directly, bypassing the service, so each test controls the starting state.</summary>
    public async Task<MaintenanceReport> AddReportAsync(
        string reporterId,
        ReportStatus status = ReportStatus.Submitted,
        ReportPriority priority = ReportPriority.Normal,
        string? assignedTo = null,
        DateTime? createdAt = null,
        DateTime? resolvedAt = null,
        string description = "Water is leaking from the ceiling.",
        int? categoryId = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        var report = new MaintenanceReport
        {
            ReferenceNumber = Guid.NewGuid().ToString("N"),
            Description = description,
            CategoryId = categoryId ?? CategoryId,
            LocationId = LocationId,
            Status = status,
            Priority = priority,
            ReporterId = reporterId,
            AssignedToUserId = assignedTo,
            CreatedAt = now,
            UpdatedAt = now,
            ResolvedAt = resolvedAt
        };
        Db.Reports.Add(report);
        await Db.SaveChangesAsync();
        report.ReferenceNumber = $"CMR-{now.Year}-{report.Id:0000}";
        await Db.SaveChangesAsync();
        return report;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        try
        {
            if (Directory.Exists(_uploadDir)) Directory.Delete(_uploadDir, true);
        }
        catch (IOException)
        {
            // Cleanup is best effort.
        }
    }
}
