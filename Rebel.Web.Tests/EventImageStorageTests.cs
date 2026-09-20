using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class EventImageStorageTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        $"rebel-event-images-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_StoresValidPngWithGeneratedName()
    {
        var content = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        };
        var storage = CreateStorage();
        var image = CreateFormFile(content, "poster.png", "image/png");

        var imageUrl = await storage.SaveAsync(image);

        Assert.StartsWith("/images/events/event-", imageUrl);
        Assert.EndsWith(".png", imageUrl);

        var physicalPath = Path.Combine(
            _testRoot,
            "wwwroot",
            imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(physicalPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(physicalPath));
    }

    [Fact]
    public async Task SaveAsync_RejectsFileWithInvalidSignature()
    {
        var storage = CreateStorage();
        var image = CreateFormFile(
            "not an image"u8.ToArray(),
            "poster.png",
            "image/png");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.SaveAsync(image));

        Assert.Equal("The selected file is not a valid image.", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_RejectsFileLargerThanFiveMegabytes()
    {
        var storage = CreateStorage();
        var image = CreateFormFile(
            new byte[(5 * 1024 * 1024) + 1],
            "poster.jpg",
            "image/jpeg");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.SaveAsync(image));

        Assert.Equal("The event poster must be 5 MB or smaller.", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGeneratedEventImage()
    {
        var storage = CreateStorage();
        var image = CreateFormFile(
            ValidPngHeader(),
            "poster.png",
            "image/png");
        var imageUrl = await storage.SaveAsync(image);
        var physicalPath = GetPhysicalPath(imageUrl);

        await storage.DeleteAsync(imageUrl);

        Assert.False(File.Exists(physicalPath));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotRemoveManuallyNamedImage()
    {
        var storage = CreateStorage();
        var storageDirectory = Path.Combine(
            _testRoot,
            "wwwroot",
            "images",
            "events");
        Directory.CreateDirectory(storageDirectory);
        var physicalPath = Path.Combine(storageDirectory, "featured.png");
        await File.WriteAllBytesAsync(physicalPath, ValidPngHeader());

        await storage.DeleteAsync("/images/events/featured.png");

        Assert.True(File.Exists(physicalPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private EventImageStorage CreateStorage()
    {
        var webRoot = Path.Combine(_testRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        return new EventImageStorage(new TestWebHostEnvironment
        {
            ContentRootPath = _testRoot,
            WebRootPath = webRoot
        });
    }

    private string GetPhysicalPath(string imageUrl) => Path.Combine(
        _testRoot,
        "wwwroot",
        imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private static byte[] ValidPngHeader() =>
    [
        0x89, 0x50, 0x4E, 0x47,
        0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x00
    ];

    private static FormFile CreateFormFile(
        byte[] content,
        string fileName,
        string contentType)
    {
        return new FormFile(
            new MemoryStream(content),
            0,
            content.Length,
            "imageFile",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Rebel.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
