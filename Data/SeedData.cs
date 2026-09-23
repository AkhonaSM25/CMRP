using CMRP.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Data;

/// <summary>
/// Demo/development seed routine (Task 3 section 4.4). Idempotent: safe to run on every start-up.
/// Controlled by configuration: Seed:DemoAccounts and Seed:DemoReports.
/// </summary>
public static class SeedData
{
    /// <summary>Demo-only password. Documented in the README for assessment; never use in production.</summary>
    public const string DemoPassword = "Demo@12345";

    public const string ReporterEmail = "reporter@cmrp.test";
    public const string Reporter2Email = "reporter2@cmrp.test";
    public const string TechnicianEmail = "technician@cmrp.test";
    public const string Technician2Email = "technician2@cmrp.test";
    public const string CoordinatorEmail = "coordinator@cmrp.test";
    public const string AdminEmail = "admin@cmrp.test";

    private static readonly (string Email, string Name, string Role)[] DemoUsers =
    {
        (ReporterEmail, "Lerato Mokoena", Roles.Reporter),
        (Reporter2Email, "Mr Dlamini", Roles.Reporter),
        (TechnicianEmail, "Sipho Khumalo", Roles.Technician),
        (Technician2Email, "Thandi Zulu", Roles.Technician),
        (CoordinatorEmail, "Naledi Sithole", Roles.Coordinator),
        (AdminEmail, "System Administrator", Roles.Administrator)
    };

    private static readonly string[] CategoryNames =
    {
        "Electrical fault",
        "Water leak",
        "Broken window",
        "Vandalism",
        "Sanitation issue",
        "Wi-Fi / network",
        "Projector / AV equipment",
        "Computer lab equipment",
        "Furniture damage",
        "Building fabric (doors, walls, roof)",
        "Lighting",
        "Other"
    };

    // Sample reference data for demonstration. Replace with the real campus/building list.
    private static readonly (string Campus, string[] Buildings)[] LocationData =
 {
    ("Howard College", new[] { "Library", "Engineering Block", "Student Centre", "Lecture Venues", "Residences" }),
    ("Westville", new[] { "Library", "Student Centre", "Science Block", "Residences" }),
    ("Edgewood", new[] { "Main Block", "Library" }),
    ("Pietermaritzburg", new[] { "Library", "Science Block", "Student Centre", "Residences" }),
    ("Medical School", new[] { "Main Building", "Library", "Lecture Venues", "Residences" }),   // <-- new
    ("Other", new[] { "Off-campus / other" })
};

    public static async Task InitializeAsync(IServiceProvider services, bool demoAccounts, bool demoReports)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedReferenceDataAsync(db);

        if (demoAccounts)
        {
            foreach (var (email, name, role) in DemoUsers)
            {
                await EnsureUserAsync(userManager, email, name, role);
            }

            if (demoReports)
            {
                await SeedDemoReportsAsync(db, userManager);
            }
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(CategoryNames.Select(n => new MaintenanceCategory { Name = n }));
        }

        if (!await db.Locations.AnyAsync())
        {
            foreach (var (campus, buildings) in LocationData)
            {
                foreach (var building in buildings)
                {
                    db.Locations.Add(new CampusLocation { Campus = campus, Building = building });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private record DemoReport(
        double DaysAgo, string Category, string Campus, string Building, string? Detail, string Description,
        string ReporterEmail, ReportStatus Status, ReportPriority Priority, string? TechnicianEmail, double FixDays);

    private static readonly DemoReport[] DemoReports =
    {
        new(45, "Sanitation issue", "Westville", "Residences", null, "Overflowing bins behind the dining hall are attracting pests.", ReporterEmail, ReportStatus.Closed, ReportPriority.Normal, TechnicianEmail, 1.2),
        new(40, "Vandalism", "Howard College", "Library", null, "Damaged noticeboard outside the entrance.", Reporter2Email, ReportStatus.Closed, ReportPriority.Low, Technician2Email, 2),
        new(33, "Furniture damage", "Pietermaritzburg", "Science Block", "Room 105", "Several broken chairs and a wobbly lecturer's desk.", ReporterEmail, ReportStatus.Closed, ReportPriority.Low, TechnicianEmail, 6),
        new(28, "Broken window", "Edgewood", "Main Block", "Room 204", "Window will not close and lets rain in.", Reporter2Email, ReportStatus.Closed, ReportPriority.Low, Technician2Email, 9),
        new(20, "Water leak", "Howard College", "Residences", null, "Burst pipe outside block C is flooding the walkway.", ReporterEmail, ReportStatus.Closed, ReportPriority.High, TechnicianEmail, 0.4),
        new(15, "Electrical fault", "Howard College", "Engineering Block", "Lab 2", "Half of the lab lights are not working.", Reporter2Email, ReportStatus.Resolved, ReportPriority.Normal, TechnicianEmail, 4.5),
        new(12, "Wi-Fi / network", "Westville", "Library", null, "Wi-Fi drops every few minutes on the second floor.", ReporterEmail, ReportStatus.Closed, ReportPriority.Normal, Technician2Email, 3.2),
        new(9, "Projector / AV equipment", "Howard College", "Lecture Venues", "Venue 3", "Projector will not switch on before lectures.", Reporter2Email, ReportStatus.Resolved, ReportPriority.Normal, TechnicianEmail, 0.6),
        new(6, "Sanitation issue", "Pietermaritzburg", "Student Centre", "Ground floor toilets", "Blocked toilets and no running water.", ReporterEmail, ReportStatus.UnderReview, ReportPriority.High, null, 0),
        new(5, "Vandalism", "Westville", "Student Centre", null, "Graffiti on the outer wall next to the main entrance.", ReporterEmail, ReportStatus.Resolved, ReportPriority.Low, TechnicianEmail, 1.5),
        new(4, "Computer lab equipment", "Howard College", "Engineering Block", "Lab 1", "Three computers do not start up.", ReporterEmail, ReportStatus.InProgress, ReportPriority.Normal, Technician2Email, 0),
        new(3, "Water leak", "Edgewood", "Main Block", null, "Water dripping from the ceiling in the corridor outside the lecture venue.", Reporter2Email, ReportStatus.Assigned, ReportPriority.Normal, Technician2Email, 0),
        new(2, "Broken window", "Howard College", "Library", "Level 1", "Window pane cracked near the study carrels and the glass is loose.", ReporterEmail, ReportStatus.InProgress, ReportPriority.High, TechnicianEmail, 0),
        new(1, "Electrical fault", "Howard College", "Residences", "Block B, room 12", "Sparking plug point in the study room. Please treat as urgent.", Reporter2Email, ReportStatus.Submitted, ReportPriority.Critical, null, 0),
        new(0.5, "Building fabric (doors, walls, roof)", "Westville", "Science Block", null, "Door handle is broken and the door will not lock.", Reporter2Email, ReportStatus.Submitted, ReportPriority.Normal, null, 0)
    };

    private static async Task SeedDemoReportsAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        if (await db.Reports.AnyAsync())
        {
            return;
        }

        var users = new Dictionary<string, ApplicationUser>();
        foreach (var (email, _, _) in DemoUsers)
        {
            var u = await userManager.FindByEmailAsync(email);
            if (u is not null) users[email] = u;
        }

        var categories = await db.Categories.ToDictionaryAsync(c => c.Name);
        var locations = await db.Locations.ToListAsync();
        var coordinator = users[CoordinatorEmail];
        var now = DateTime.UtcNow;
        var created = new List<MaintenanceReport>();

        foreach (var d in DemoReports.OrderByDescending(x => x.DaysAgo))
        {
            var reporter = users[d.ReporterEmail];
            var technician = d.TechnicianEmail is null ? null : users[d.TechnicianEmail];
            var location = locations.First(l => l.Campus == d.Campus && l.Building == d.Building);
            var start = now.AddDays(-d.DaysAgo);

            var report = new MaintenanceReport
            {
                ReferenceNumber = Guid.NewGuid().ToString("N"),
                Description = d.Description,
                CategoryId = categories[d.Category].Id,
                LocationId = location.Id,
                LocationDetail = d.Detail,
                Status = d.Status,
                Priority = d.Priority,
                ReporterId = reporter.Id,
                CreatedAt = start,
                UpdatedAt = start
            };

            var current = ReportStatus.Submitted;
            var last = start;

            report.StatusHistory.Add(new ReportStatusHistory
            {
                ChangedById = reporter.Id,
                FromStatus = null,
                ToStatus = ReportStatus.Submitted,
                ChangedAt = start
            });

            void MoveTo(ReportStatus next, DateTime at, string changedById)
            {
                report.StatusHistory.Add(new ReportStatusHistory
                {
                    ChangedById = changedById,
                    FromStatus = current,
                    ToStatus = next,
                    ChangedAt = at
                });
                current = next;
                last = at;
            }

            if (d.Priority >= ReportPriority.High)
            {
                report.Notes.Add(new ReportNote
                {
                    AuthorId = coordinator.Id,
                    Text = "Prioritised because of the safety risk or disruption to teaching.",
                    IsInternal = true,
                    CreatedAt = start.AddMinutes(30)
                });
            }

            if (d.Status == ReportStatus.UnderReview)
            {
                MoveTo(ReportStatus.UnderReview, start.AddHours(2), coordinator.Id);
            }

            if (technician is not null)
            {
                var assignedAt = d.FixDays > 0 ? start.AddDays(d.FixDays * 0.15) : start.AddHours(3);
                report.AssignedToUserId = technician.Id;
                report.AssignmentHistory.Add(new ReportAssignmentHistory
                {
                    FromUserId = null,
                    ToUserId = technician.Id,
                    ChangedById = coordinator.Id,
                    ChangedAt = assignedAt
                });
                MoveTo(ReportStatus.Assigned, assignedAt, coordinator.Id);

                if (d.Status is ReportStatus.InProgress or ReportStatus.Resolved or ReportStatus.Closed)
                {
                    var startedAt = d.FixDays > 0 ? start.AddDays(d.FixDays * 0.4) : start.AddDays(1);
                    MoveTo(ReportStatus.InProgress, startedAt, technician.Id);
                    report.Notes.Add(new ReportNote
                    {
                        AuthorId = technician.Id,
                        Text = "Technician on site and assessing the fault.",
                        IsInternal = false,
                        CreatedAt = startedAt.AddMinutes(10)
                    });
                }

                if (d.Status is ReportStatus.Resolved or ReportStatus.Closed)
                {
                    var resolvedAt = start.AddDays(d.FixDays);
                    MoveTo(ReportStatus.Resolved, resolvedAt, technician.Id);
                    report.ResolvedAt = resolvedAt;
                    report.Notes.Add(new ReportNote
                    {
                        AuthorId = technician.Id,
                        Text = "Repair completed and checked.",
                        IsInternal = false,
                        CreatedAt = resolvedAt
                    });
                }

                if (d.Status == ReportStatus.Closed)
                {
                    MoveTo(ReportStatus.Closed, last.AddDays(2), coordinator.Id);
                }
            }

            report.UpdatedAt = last;
            created.Add(report);
            db.Reports.Add(report);
        }

        await db.SaveChangesAsync();

        // Reference numbers use the generated key, so they can only be set after the first save.
        foreach (var r in created)
        {
            r.ReferenceNumber = $"CMR-{r.CreatedAt.Year}-{r.Id:0000}";
        }

        await db.SaveChangesAsync();
    }
}
