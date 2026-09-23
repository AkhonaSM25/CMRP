using CMRP.Data;
using CMRP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMRP.Controllers;

/// <summary>Streams photo evidence only after an ownership/role check. Files are never exposed as public static URLs (Task 3 section 6.2).</summary>
[Authorize]
[Route("attachments")]
public class AttachmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AttachmentService _attachments;

    public AttachmentsController(ApplicationDbContext db, AttachmentService attachments)
    {
        _db = db;
        _attachments = attachments;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var attachment = await _db.Attachments
            .AsNoTracking()
            .Include(a => a.Report)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment is null) return NotFound();
        if (!ReportAccess.CanView(User, attachment.Report)) return Forbid();

        var stream = _attachments.OpenRead(attachment.StoredName);
        if (stream is null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return File(stream, attachment.ContentType);
    }
}
