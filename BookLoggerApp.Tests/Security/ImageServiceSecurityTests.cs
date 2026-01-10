using System.Net;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Security;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}

public class ImageServiceSecurityTests
{
    private readonly IFileSystem _fileSystem;
    private readonly string _testImagesDir;

    public ImageServiceSecurityTests()
    {
        _fileSystem = Substitute.For<IFileSystem>();
        _testImagesDir = Path.Combine(Path.GetTempPath(), "test_covers");
        _fileSystem.CombinePath(Arg.Any<string>(), "covers").Returns(_testImagesDir);
        _fileSystem.CombinePath(Arg.Any<string>(), Arg.Any<string>()).Returns(x => Path.Combine((string)x[0], (string)x[1]));
        _fileSystem.CreateDirectory(Arg.Any<string>());
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectLargeFile()
    {
        // Arrange
        var hugeContent = new byte[15 * 1024 * 1024]; // 15MB (limit should be 10MB)
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(hugeContent)
            };
            response.Content.Headers.ContentLength = hugeContent.Length;
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler);
        var service = new ImageService(_fileSystem, null, httpClient);

        // Act
        var result = await service.DownloadImageFromUrlAsync("http://example.com/huge.jpg");

        // Assert
        result.Should().BeNull("because the file exceeds the maximum allowed size");
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectNonImageContentType()
    {
        // Arrange
        var content = new byte[100];
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler);
        var service = new ImageService(_fileSystem, null, httpClient);

        // Act
        var result = await service.DownloadImageFromUrlAsync("http://example.com/malicious.html");

        // Assert
        result.Should().BeNull("because the content type is not an allowed image type");
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldAcceptValidImage()
    {
        // Arrange
        var content = new byte[1024];
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler);
        var service = new ImageService(_fileSystem, null, httpClient);

        // Act
        var result = await service.DownloadImageFromUrlAsync("http://example.com/valid.jpg");

        // Assert
        result.Should().NotBeNull();
        result!.Length.Should().Be(1024);
    }
}
