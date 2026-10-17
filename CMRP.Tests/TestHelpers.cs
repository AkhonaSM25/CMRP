using System.Security.Claims;
using CMRP.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IWebHostEnvironment;

namespace CMRP.Tests;

/// <summary>Builds a signed-in principal without needing a web server.</summary>
public static class TestUser
{
    public static ClaimsPrincipal Principal(string? userId, params string[] roles)
    {
        var claims = new List<Claim>();
        if (userId is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}

/// <summary>In-memory objects for the pure (no database) tests.</summary>
public static class Make
{
    public static MaintenanceReport Report(
        string reporterId = "reporter-1",
        string? assignedTo = null,
        ReportStatus status = ReportStatus.Submitted,
        ReportPriority priority = ReportPriority.Normal,
        string category = "Water leak",
        string building = "Library",
        string? detail = null) => new()
    {
        Id = 1,
        ReferenceNumber = "CMR-2026-0001",
        Description = "Test",
        ReporterId = reporterId,
        AssignedToUserId = assignedTo,
        Status = status,
        Priority = priority,
        Category = new MaintenanceCategory { Id = 1, Name = category },
        Location = new CampusLocation { Id = 1, Campus = "Howard College", Building = building },
        LocationDetail = detail,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static FormFile File(byte[] content, string fileName, long? declaredLength = null) =>
        new(new MemoryStream(content), 0, declaredLength ?? content.Length, "Photo", fileName);

    public static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

    public static readonly byte[] JpegBytes =
        { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

    public static readonly byte[] ExeBytes =
        { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00 };
}

/// <summary>Minimal host environment so AttachmentService can be constructed in tests.</summary>
public sealed class FakeEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "CMRP.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
