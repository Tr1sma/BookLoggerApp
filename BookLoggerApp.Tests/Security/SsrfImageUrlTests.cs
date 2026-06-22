using System.Net;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Security;

public class SsrfImageUrlTests
{
    [Theory]
    [InlineData("https://books.google.com/books/content?id=x&img=1")]
    [InlineData("http://covers.example.com/x.png")]
    public void IsSafeRemoteImageUrl_allows_public_http_https(string url)
    {
        ImageService.IsSafeRemoteImageUrl(url, out var uri).Should().BeTrue();
        uri.Should().NotBeNull();
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/x")]
    [InlineData("http://127.0.0.1/x")]
    [InlineData("http://localhost/x")]
    [InlineData("http://[::1]/x")]
    [InlineData("http://169.254.169.254/latest/meta-data")] // cloud metadata endpoint
    [InlineData("http://10.0.0.5/x")]
    [InlineData("http://192.168.1.1/x")]
    [InlineData("http://172.16.0.1/x")]
    [InlineData("not a url")]
    [InlineData("")]
    public void IsSafeRemoteImageUrl_rejects_unsafe_or_internal_targets(string url)
    {
        ImageService.IsSafeRemoteImageUrl(url, out var uri).Should().BeFalse();
        uri.Should().BeNull();
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_skips_http_request_for_unsafe_url()
    {
        // The guard must short-circuit BEFORE any HTTP request is made — proven by the
        // handler recording zero calls. (Without the guard the loopback request would be
        // attempted and the handler would be invoked.)
        var handler = new CountingHandler();
        var service = new ImageService(new FileSystemAdapter(), null, new HttpClient(handler));

        var result = await service.DownloadImageFromUrlAsync("http://127.0.0.1/cover.jpg");

        handler.Calls.Should().Be(0);
        result.Should().BeNull();
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            });
        }
    }
}
