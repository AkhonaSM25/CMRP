using CMRP.Services;
using Microsoft.Extensions.Configuration;

namespace CMRP.Tests;

/// <summary>Photo evidence rules (FR-03, R-09): size, type, real file signature, safe storage.</summary>
public sealed class AttachmentServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cmrp-att-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentService _service;

    public AttachmentServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:Path"] = _dir })
            .Build();
        _service = new AttachmentService(config, new FakeEnvironment());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Constructor_CreatesTheUploadFolder() => Assert.True(Directory.Exists(_dir));

    [Fact]
    public async Task Validate_AcceptsAGenuinePng()
    {
        var (ok, error, type) = await _service.ValidateAsync(Make.File(Make.PngBytes, "leak.png"));
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("image/png", type);
    }

    [Theory]
    [InlineData("leak.jpg")]
    [InlineData("leak.jpeg")]
    [InlineData("LEAK.JPG")]
    public async Task Validate_AcceptsAGenuineJpeg(string fileName)
    {
        var (ok, _, type) = await _service.ValidateAsync(Make.File(Make.JpegBytes, fileName));
        Assert.True(ok);
        Assert.Equal("image/jpeg", type);
    }

    [Fact]
    public async Task Validate_ExtensionIsCaseInsensitive()
    {
        var (ok, _, type) = await _service.ValidateAsync(Make.File(Make.PngBytes, "LEAK.PNG"));
        Assert.True(ok);
        Assert.Equal("image/png", type);
    }

    [Fact]
    public async Task Validate_RejectsAnEmptyFile()
    {
        var (ok, error, _) = await _service.ValidateAsync(Make.File(Array.Empty<byte>(), "empty.png"));
        Assert.False(ok);
        Assert.Equal("The selected file is empty.", error);
    }

    [Fact]
    public async Task Validate_RejectsAFileOverFiveMegabytes()
    {
        var file = Make.File(Make.PngBytes, "big.png", declaredLength: AttachmentService.MaxBytes + 1);
        var (ok, error, _) = await _service.ValidateAsync(file);
        Assert.False(ok);
        Assert.Contains("5 MB", error);
    }

    [Fact]
    public async Task Validate_AcceptsAFileExactlyAtTheLimit()
    {
        var file = Make.File(Make.PngBytes, "limit.png", declaredLength: AttachmentService.MaxBytes);
        var (ok, _, _) = await _service.ValidateAsync(file);
        Assert.True(ok);
    }

    [Theory]
    [InlineData("photo.gif")]
    [InlineData("document.pdf")]
    [InlineData("script.exe")]
    [InlineData("noextension")]
    public async Task Validate_RejectsUnsupportedExtensions(string fileName)
    {
        var (ok, error, _) = await _service.ValidateAsync(Make.File(Make.PngBytes, fileName));
        Assert.False(ok);
        Assert.Equal("Only PNG or JPG photos can be uploaded.", error);
    }

    [Fact]
    public async Task Validate_RejectsAnExecutableRenamedToPng()
    {
        var (ok, error, _) = await _service.ValidateAsync(Make.File(Make.ExeBytes, "virus.png"));
        Assert.False(ok);
        Assert.Equal("The file is not a valid PNG or JPG image.", error);
    }

    [Fact]
    public async Task Validate_RejectsAnExecutableRenamedToJpg()
    {
        var (ok, _, _) = await _service.ValidateAsync(Make.File(Make.ExeBytes, "virus.jpg"));
        Assert.False(ok);
    }

    [Fact]
    public async Task Validate_RejectsJpegContentWithAPngName()
    {
        var (ok, _, _) = await _service.ValidateAsync(Make.File(Make.JpegBytes, "mismatch.png"));
        Assert.False(ok);
    }

    [Fact]
    public async Task Validate_RejectsPngContentWithAJpgName()
    {
        var (ok, _, _) = await _service.ValidateAsync(Make.File(Make.PngBytes, "mismatch.jpg"));
        Assert.False(ok);
    }

    [Fact]
    public async Task Validate_RejectsATruncatedPngHeader()
    {
        var truncated = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var (ok, _, _) = await _service.ValidateAsync(Make.File(truncated, "short.png"));
        Assert.False(ok);
    }

    [Fact]
    public async Task Save_StoresTheFileUnderAGeneratedName()
    {
        var storedName = await _service.SaveAsync(Make.File(Make.PngBytes, "my holiday photo.png"));

        Assert.NotEqual("my holiday photo.png", storedName);
        Assert.EndsWith(".png", storedName);
        Assert.Equal(32 + 4, storedName.Length); // 32-char GUID + ".png"
        Assert.True(File.Exists(Path.Combine(_dir, storedName)));
    }

    [Fact]
    public async Task Save_UsesADifferentNameEveryTime()
    {
        var first = await _service.SaveAsync(Make.File(Make.PngBytes, "a.png"));
        var second = await _service.SaveAsync(Make.File(Make.PngBytes, "a.png"));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Save_IgnoresPathPartsInTheClientFileName()
    {
        var storedName = await _service.SaveAsync(Make.File(Make.PngBytes, "../../evil.png"));
        Assert.DoesNotContain("..", storedName);
        Assert.True(File.Exists(Path.Combine(_dir, storedName)));
    }

    [Fact]
    public async Task OpenRead_ReturnsTheStoredBytes()
    {
        var storedName = await _service.SaveAsync(Make.File(Make.PngBytes, "a.png"));

        await using var stream = _service.OpenRead(storedName);
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal(Make.PngBytes, copy.ToArray());
    }

    [Fact]
    public void OpenRead_ReturnsNullForAMissingFile() =>
        Assert.Null(_service.OpenRead("does-not-exist.png"));

    [Fact]
    public void OpenRead_CannotEscapeTheUploadFolder()
    {
        var outside = Path.Combine(Path.GetTempPath(), "cmrp-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(outside, "secret");
        try
        {
            Assert.Null(_service.OpenRead("../" + Path.GetFileName(outside)));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Delete_RemovesTheFile()
    {
        var storedName = await _service.SaveAsync(Make.File(Make.PngBytes, "a.png"));
        _service.Delete(storedName);
        Assert.False(File.Exists(Path.Combine(_dir, storedName)));
    }

    [Fact]
    public void Delete_DoesNothingForAMissingFile()
    {
        var ex = Record.Exception(() => _service.Delete("nope.png"));
        Assert.Null(ex);
    }
}
