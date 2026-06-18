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
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>Not an image</body></html>")
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        });

        var result = await new ImageService(_fileSystem, _logger, new HttpClient(mockHandler))
            .DownloadImageFromUrlAsync("http://example.com/malicious.html");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectLargeFiles()
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[0])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = 11 * 1024 * 1024; // 11MB
            return Task.FromResult(response);
        });

        var result = await new ImageService(_fileSystem, _logger, new HttpClient(mockHandler))
            .DownloadImageFromUrlAsync("http://example.com/huge_image.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldAcceptValidImage()
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }) // JPEG magic
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = 1024;
            return Task.FromResult(response);
        });

        var result = await new ImageService(_fileSystem, _logger, new HttpClient(mockHandler))
            .DownloadImageFromUrlAsync("http://example.com/valid.jpg");

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
