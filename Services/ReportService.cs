using CMRP.Data;
using CMRP.Models;
using CMRP.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Services;

/// <summary>Filters used by the report lists. Unset properties are ignored.</summary>
public class ReportQuery
{
    public string? Search { get; set; }
    public ReportStatus? Status { get; set; }
    public ReportPriority? Priority { get; set; }
    public int? CategoryId { get; set; }
    public string? ReporterId { get; set; }
    public string? AssignedToUserId { get; set; }
    public bool ActiveOnly { get; set; }
    public bool UnassignedOnly { get; set; }

    /// <summary>Staff queues put the most urgent work first; reporters see the newest first.</summary>
    public bool OrderByPriority { get; set; }

    public int Take { get; set; } = 200;
}

public class ReportService
{
    public const string ReferencePrefix = "CMR";

    private readonly ApplicationDbContext _db;
    private readonly AttachmentService _attachments;
    private readonly ILogger<ReportService> _logger;

    public ReportService(ApplicationDbContext db, AttachmentService attachments, ILogger<ReportService> logger)
    {
        _db = db;
        _attachments = attachments;
        _logger = logger;
    }

    /// <summary>
    /// Creates a report. Success is only reported after the data (and optional photo) has been persisted (Task 3 section 3.3).
    /// </summary>
    public async Task<CreateReportResult> CreateAsync(string reporterId, CreateReportViewModel input)
    {
        string? contentType = null;
        var hasPhoto = input.Photo is { Length: > 0 };

        if (hasPhoto)
        {
            var (isValid, error, type) = await _attachments.ValidateAsync(input.Photo!);
            if (!isValid) return CreateReportResult.Fail(error!);
            contentType = type;
        }

        var categoryId = input.CategoryId.GetValueOrDefault();
        var locationId = input.LocationId.GetValueOrDefault();
        var categoryOk = await _db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive);
        var locationOk = await _db.Locations.AnyAsync(l => l.Id == locationId && l.IsActive);
        if (!categoryOk || !locationOk)
        {
            return CreateReportResult.Fail("Please choose a valid incident type and location.");
        }

        string? storedName = null;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var report = new MaintenanceReport
            {
                // The real reference number needs the generated key, so start with a unique placeholder.
                ReferenceNumber = Guid.NewGuid().ToString("N"),
                Description = input.Description.Trim(),
                CategoryId = categoryId,
                LocationId = locationId,
                LocationDetail = string.IsNullOrWhiteSpace(input.LocationDetail) ? null : input.LocationDetail.Trim(),
                Status = ReportStatus.Submitted,
                Priority = ReportPriority.Normal,
                ReporterId = reporterId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Reports.Add(report);
            await _db.SaveChangesAsync();

            report.ReferenceNumber = $"{ReferencePrefix}-{now.Year}-{report.Id:0000}";

            _db.StatusHistory.Add(new ReportStatusHistory
            {
                ReportId = report.Id,
                ChangedById = reporterId,
                FromStatus = null,
                ToStatus = ReportStatus.Submitted,
                ChangedAt = now
            });

            if (hasPhoto)
            {
                storedName = await _attachments.SaveAsync(input.Photo!);
                var original = Path.GetFileName(input.Photo!.FileName);
                _db.Attachments.Add(new ReportAttachment
                {
                    ReportId = report.Id,
                    StoredName = storedName,
                    OriginalName = original.Length > 200 ? original[..200] : original,
                    ContentType = contentType!,
                    SizeBytes = input.Photo.Length,
                    UploadedAt = now
                });
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return CreateReportResult.Ok(report.Id, report.ReferenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create a maintenance report.");
            await transaction.RollbackAsync();
            if (storedName is not null) _attachments.Delete(storedName);
            return CreateReportResult.Fail("We could not save your report. Please try again.");
        }
    }

    public async Task<List<MaintenanceReport>> SearchAsync(ReportQuery q)
    {
        IQueryable<MaintenanceReport> query = _db.Reports
            .AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Location)
            .Include(r => r.Reporter)
            .Include(r => r.AssignedTo)
            .Include(r => r.Attachments);

        if (q.ReporterId is not null) query = query.Where(r => r.ReporterId == q.ReporterId);
        if (q.AssignedToUserId is not null) query = query.Where(r => r.AssignedToUserId == q.AssignedToUserId);
        if (q.Status.HasValue) query = query.Where(r => r.Status == q.Status.Value);
        if (q.Priority.HasValue) query = query.Where(r => r.Priority == q.Priority.Value);
        if (q.CategoryId.HasValue) query = query.Where(r => r.CategoryId == q.CategoryId.Value);
        if (q.UnassignedOnly) query = query.Where(r => r.AssignedToUserId == null);

        if (q.ActiveOnly)
        {
            query = query.Where(r =>
                r.Status != ReportStatus.Resolved &&
                r.Status != ReportStatus.Closed &&
                r.Status != ReportStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var like = $"%{q.Search.Trim()}%";
            query = query.Where(r =>
                EF.Functions.Like(r.ReferenceNumber, like) ||
                EF.Functions.Like(r.Description, like) ||
                EF.Functions.Like(r.Category.Name, like) ||
                EF.Functions.Like(r.Location.Building, like) ||
                EF.Functions.Like(r.Location.Campus, like));
        }

        query = q.OrderByPriority
            ? query.OrderByDescending(r => r.Priority).ThenByDescending(r => r.CreatedAt)
            : query.OrderByDescending(r => r.CreatedAt);

        return await query.Take(q.Take).ToListAsync();
    }

    /// <summary>Loads a report with everything the detail page needs.</summary>
    public Task<MaintenanceReport?> GetDetailsAsync(int id) =>
        _db.Reports
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Category)
            .Include(r => r.Location)
            .Include(r => r.Reporter)
            .Include(r => r.AssignedTo)
            .Include(r => r.Attachments)
            .Include(r => r.Notes).ThenInclude(n => n.Author)
            .Include(r => r.StatusHistory).ThenInclude(h => h.ChangedBy)
            .Include(r => r.AssignmentHistory).ThenInclude(a => a.ToUser)
            .FirstOrDefaultAsync(r => r.Id == id);
}
