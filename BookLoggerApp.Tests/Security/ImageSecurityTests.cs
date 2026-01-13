using System.Net;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace BookLoggerApp.Tests.Security;

public class ImageSecurityTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<ILogger<ImageService>> _mockLogger;
    private ImageService _imageService;
    private readonly string _testImagesDir;

    public ImageSecurityTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockLogger = new Mock<ILogger<ImageService>>();

        _testImagesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "covers");

        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string p1, string p2) => Path.Combine(p1, p2));

        _mockFileSystem.Setup(fs => fs.CreateDirectory(It.IsAny<string>()));
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectLargeFiles()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[0])
                {
                    Headers = { ContentLength = 11 * 1024 * 1024 } // 11 MB
                }
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        // Inject HttpClient directly using the internal constructor
        _imageService = new ImageService(_mockFileSystem.Object, _mockLogger.Object, httpClient);

        // Act
        var result = await _imageService.DownloadImageFromUrlAsync("http://example.com/large.jpg");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldRejectInvalidContentType()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<html><body>Not an image</body></html>", System.Text.Encoding.UTF8, "text/html")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        _imageService = new ImageService(_mockFileSystem.Object, _mockLogger.Object, httpClient);

        // Act
        var result = await _imageService.DownloadImageFromUrlAsync("http://example.com/notimage.html");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadImageFromUrlAsync_ShouldAcceptValidImage()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }) // Pseudo JPG header
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
                }
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        _imageService = new ImageService(_mockFileSystem.Object, _mockLogger.Object, httpClient);

        // Act
        var result = await _imageService.DownloadImageFromUrlAsync("http://example.com/image.jpg");

        // Assert
        Assert.NotNull(result);
    }
}
