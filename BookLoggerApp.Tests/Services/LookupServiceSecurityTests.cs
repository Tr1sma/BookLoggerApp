using System.Net;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class LookupServiceSecurityTests
{
    [Fact]
    public async Task LookupByISBNAsync_ShouldEscapeISBN_ToPreventParameterInjection()
    {
        // Arrange
        // Mock HttpMessageHandler to capture the request URL
        var handlerMock = new Mock<HttpMessageHandler>();
        var capturedUrl = "";

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedUrl = req.RequestUri?.ToString() ?? "";
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}") // Return empty valid JSON
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new LookupService(httpClient);

        // This input attempts to inject a new parameter "&maxResults=100"
        // If not escaped, the URL would be "...?q=isbn:123&maxResults=100"
        var injectionAttempt = "123&maxResults=100";

        // Act
        await service.LookupByISBNAsync(injectionAttempt);

        // Assert
        // Verify that the special characters were escaped
        // Expected: ...?q=isbn:123%26maxResults%3D100
        // We verify that it does NOT contain the raw "&maxResults"
        capturedUrl.Should().Contain("123%26maxResults%3D100");
        capturedUrl.Should().NotContain("&maxResults=100");
    }
}
