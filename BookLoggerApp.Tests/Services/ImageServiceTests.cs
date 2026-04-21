using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ImageServiceTests : IDisposable
{
    private readonly ImageService _service;
    private readonly string _testImagePath;

    public ImageServiceTests()
    {
        IFileSystem fileSystem = new FileSystemAdapter();
        _service = new ImageService(fileSystem);

        // Create a test image file
        _testImagePath = Path.Combine(Path.GetTempPath(), "test_image.jpg");
        File.WriteAllBytes(_testImagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header
    }

    [Fact]
    public async Task SaveCoverImageAsync_ShouldSaveImageAndReturnPath()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        using var imageStream = File.OpenRead(_testImagePath);

        // Act
        var result = await _service.SaveCoverImageAsync(imageStream, bookId);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("covers");
        result.Should().Contain(bookId.ToString());

        // Verify the file was saved
        var savedPath = await _service.GetCoverImagePathAsync(bookId);
        savedPath.Should().NotBeNull();
        File.Exists(savedPath).Should().BeTrue();
    }

    [Fact]
    public async Task GetCoverImagePathAsync_WhenImageDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var nonExistentBookId = Guid.NewGuid();

        // Act
        var result = await _service.GetCoverImagePathAsync(nonExistentBookId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCoverImageAsync_ShouldDeleteImage()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        using var imageStream = File.OpenRead(_testImagePath);
        await _service.SaveCoverImageAsync(imageStream, bookId);

        // Act
        await _service.DeleteCoverImageAsync(bookId);

        // Assert
        var savedPath = await _service.GetCoverImagePathAsync(bookId);
        savedPath.Should().BeNull();
    }

    [Fact]
    public async Task SaveCoverImageAsync_WithNullStream_ShouldThrowException()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _service.SaveCoverImageAsync(null!, bookId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Coverage-ergänzende Tests (URL download, resize, PNG path)
    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] CreatePngBytes(int width = 10, int height = 10)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_handler(request));
    }

    private ImageService CreateServiceWithHttp(HttpClient http)
    {
        return new ImageService(new FileSystemAdapter(), null, http);
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_WhitespaceUrl_ReturnsNull()
    {
        var result = await _service.DownloadImageFromUrlAsync("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_NotFound_ReturnsNull()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var service = CreateServiceWithHttp(http);

        var result = await service.DownloadImageFromUrlAsync("https://example.com/missing.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_NonImageContentType_ReturnsNull()
    {
        var handler = new StubHttpHandler(_ =>
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            };
            resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return resp;
        });
        using var http = new HttpClient(handler);
        var service = CreateServiceWithHttp(http);

        var result = await service.DownloadImageFromUrlAsync("https://example.com/page.html");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_TooLarge_ReturnsNull()
    {
        var handler = new StubHttpHandler(_ =>
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1 })
            };
            resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            resp.Content.Headers.ContentLength = 20 * 1024 * 1024;
            return resp;
        });
        using var http = new HttpClient(handler);
        var service = CreateServiceWithHttp(http);

        var result = await service.DownloadImageFromUrlAsync("https://example.com/big.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ValidImage_ReturnsStream()
    {
        var bytes = CreatePngBytes();
        var handler = new StubHttpHandler(_ =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
        });
        using var http = new HttpClient(handler);
        var service = CreateServiceWithHttp(http);

        var result = await service.DownloadImageFromUrlAsync("https://example.com/ok.png");

        result.Should().NotBeNull();
        using var ms = new MemoryStream();
        await result!.CopyToAsync(ms);
        ms.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveCoverImageFromUrlAsync_InvalidUrl_ReturnsNull()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var service = CreateServiceWithHttp(http);

        var result = await service.SaveCoverImageFromUrlAsync("https://example.com/missing.jpg", Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveCoverImageFromUrlAsync_ValidImage_SavesAndReturnsPath()
    {
        var bytes = CreatePngBytes();
        var handler = new StubHttpHandler(_ =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
        });
        using var http = new HttpClient(handler);
        var service = CreateServiceWithHttp(http);

        var result = await service.SaveCoverImageFromUrlAsync("https://example.com/ok.png", Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Should().Contain("covers");
    }

    [Fact]
    public async Task GetResizedCoverImageAsync_NoImage_ReturnsNull()
    {
        var result = await _service.GetResizedCoverImageAsync(Guid.NewGuid(), 400, 600);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetResizedCoverImageAsync_ExistingImage_ReturnsResizedBytes()
    {
        var bookId = Guid.NewGuid();
        var largePng = CreatePngBytes(width: 2000, height: 3000);
        using var ms = new MemoryStream(largePng);
        await _service.SaveCoverImageAsync(ms, bookId);

        var result = await _service.GetResizedCoverImageAsync(bookId, maxWidth: 200, maxHeight: 300);

        result.Should().NotBeNull();
        result!.Value.Bytes.Length.Should().BeGreaterThan(0);
        result.Value.MimeType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task GetResizedCoverImageAsync_SmallImage_ReturnsOriginal()
    {
        var bookId = Guid.NewGuid();
        var smallPng = CreatePngBytes(width: 50, height: 50);
        using var ms = new MemoryStream(smallPng);
        await _service.SaveCoverImageAsync(ms, bookId);

        var result = await _service.GetResizedCoverImageAsync(bookId, maxWidth: 400, maxHeight: 600);

        result.Should().NotBeNull();
        result!.Value.Bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetResizedCoverImageAsync_SecondCall_UsesCachedThumbnail()
    {
        var bookId = Guid.NewGuid();
        var png = CreatePngBytes(width: 100, height: 100);
        using var ms = new MemoryStream(png);
        await _service.SaveCoverImageAsync(ms, bookId);

        var firstCall = await _service.GetResizedCoverImageAsync(bookId);
        var secondCall = await _service.GetResizedCoverImageAsync(bookId);

        firstCall.Should().NotBeNull();
        secondCall.Should().NotBeNull();
        secondCall!.Value.Bytes.Length.Should().Be(firstCall!.Value.Bytes.Length);
    }

    public void Dispose()
    {
        // Clean up test file
        if (File.Exists(_testImagePath))
        {
            File.Delete(_testImagePath);
        }
    }
}
