using System.Net;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Security;

public class ImageServiceSecurityTests
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ImageService> _logger;

    public ImageServiceSecurityTests()
    {
        _fileSystem = Substitute.For<IFileSystem>();
        _logger = Substitute.For<ILogger<ImageService>>();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectNonImageContentType()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>Not an image</body></html>")
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new ImageService(_fileSystem, _logger, httpClient);

        // Act
        var result = await service.DownloadImageFromUrlAsync("http://example.com/malicious.html");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectLargeFiles()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[0]) // Empty content, but header says it's big
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = 11 * 1024 * 1024; // 11MB
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new ImageService(_fileSystem, _logger, httpClient);

        // Act
        var result = await service.DownloadImageFromUrlAsync("http://example.com/huge_image.jpg");

        // Assert
        result.Should().BeNull();
    }

     [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldAcceptValidImage()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }) // Fake JPEG header
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = 1024;
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new ImageService(_fileSystem, _logger, httpClient);

        // Act
        var result = await service.DownloadImageFromUrlAsync("http://example.com/valid.jpg");

        // Assert
        result.Should().NotBeNull();
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
    {
        _handlerFunc = handlerFunc;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handlerFunc(request, cancellationToken);
    }
}
