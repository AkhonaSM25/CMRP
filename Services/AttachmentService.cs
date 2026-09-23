namespace CMRP.Services;

/// <summary>
/// Validates, stores and reads photo evidence (FR-03, R-09). Files are stored outside wwwroot under generated names and are only
/// served through AttachmentsController after an access check. Client-side checks are a convenience; this is the real validation.
/// </summary>
public class AttachmentService
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    private readonly string _root;

    public AttachmentService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Uploads:Path"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Path.Combine("App_Data", "uploads");
        }

        _root = Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
        Directory.CreateDirectory(_root);
    }

    /// <summary>Checks size, extension and file signature. Returns the content type to store when valid.</summary>
    public async Task<(bool IsValid, string? Error, string? ContentType)> ValidateAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            return (false, "The selected file is empty.", null);
        }

        if (file.Length > MaxBytes)
        {
            return (false, "The photo is larger than 5 MB. Please choose a smaller image.", null);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
        {
            return (false, "Only PNG or JPG photos can be uploaded.", null);
        }

        var header = new byte[8];
        int read;
        await using (var stream = file.OpenReadStream())
        {
            read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        }

        var isPng = read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var extensionIsPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

        if ((extensionIsPng && !isPng) || (!extensionIsPng && !isJpeg))
        {
            return (false, "The file is not a valid PNG or JPG image.", null);
        }

        return (true, null, contentType);
    }

    /// <summary>Saves the file under a generated name and returns that name.</summary>
    public async Task<string> SaveAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_root, storedName);

        await using var target = new FileStream(path, FileMode.CreateNew);
        await file.CopyToAsync(target);
        return storedName;
    }

    public void Delete(string storedName)
    {
        var path = Path.Combine(_root, Path.GetFileName(storedName));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Opens a stored file for streaming, or returns null if it is missing.</summary>
    public Stream? OpenRead(string storedName)
    {
        var path = Path.Combine(_root, Path.GetFileName(storedName));
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }
}
