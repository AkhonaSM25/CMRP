using CMRP.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MaintenanceReport> Reports => Set<MaintenanceReport>();
    public DbSet<MaintenanceCategory> Categories => Set<MaintenanceCategory>();
    public DbSet<CampusLocation> Locations => Set<CampusLocation>();
    public DbSet<ReportAttachment> Attachments => Set<ReportAttachment>();
    public DbSet<ReportNote> Notes => Set<ReportNote>();
    public DbSet<ReportStatusHistory> StatusHistory => Set<ReportStatusHistory>();
    public DbSet<ReportAssignmentHistory> AssignmentHistory => Set<ReportAssignmentHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Accountability: history is never deleted by cascade (Task 3 section 4.4), so every relationship is Restrict.
        builder.Entity<MaintenanceReport>(e =>
        {
            e.HasIndex(r => r.ReferenceNumber).IsUnique();
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.Priority);
            e.HasIndex(r => r.CreatedAt);
            e.HasIndex(r => r.AssignedToUserId);

            e.HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Location).WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.AssignedTo).WithMany().HasForeignKey(r => r.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);

            e.HasMany(r => r.Attachments).WithOne(a => a.Report).HasForeignKey(a => a.ReportId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Notes).WithOne(n => n.Report).HasForeignKey(n => n.ReportId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.StatusHistory).WithOne(h => h.Report).HasForeignKey(h => h.ReportId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.AssignmentHistory).WithOne(h => h.Report).HasForeignKey(h => h.ReportId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReportNote>()
            .HasOne(n => n.Author).WithMany().HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReportStatusHistory>()
            .HasOne(h => h.ChangedBy).WithMany().HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReportAssignmentHistory>(e =>
        {
            e.HasOne(a => a.FromUser).WithMany().HasForeignKey(a => a.FromUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.ToUser).WithMany().HasForeignKey(a => a.ToUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.ChangedBy).WithMany().HasForeignKey(a => a.ChangedById).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MaintenanceCategory>().HasIndex(c => c.Name).IsUnique();
        builder.Entity<CampusLocation>().HasIndex(l => new { l.Campus, l.Building }).IsUnique();
    }
}
