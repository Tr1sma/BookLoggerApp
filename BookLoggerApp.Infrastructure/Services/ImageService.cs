using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service for managing cover images and other image assets.
/// </summary>
public class ImageService : IImageService
{
    private const long MaxImageSize = 10 * 1024 * 1024; // 10MB
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp"
    };

    private readonly string _imagesDirectory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageService>? _logger;
    private readonly IFileSystem _fileSystem;

    public ImageService(IFileSystem fileSystem, ILogger<ImageService>? logger = null, HttpClient? httpClient = null)
    {
        _fileSystem = fileSystem;
        _logger = logger;

        // Get the app's local data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _imagesDirectory = _fileSystem.CombinePath(appDataPath, "covers");

        // Ensure directory exists
        _fileSystem.CreateDirectory(_imagesDirectory);

        // Initialize HttpClient for downloading images
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<string> SaveCoverImageAsync(Stream imageStream, Guid bookId, CancellationToken ct = default)
    {
        if (imageStream == null || !imageStream.CanRead)
            throw new ArgumentException("Invalid image stream", nameof(imageStream));

        try
        {
            // Generate filename: {bookId}.jpg
            var fileName = $"{bookId}.jpg";
            var fullPath = _fileSystem.CombinePath(_imagesDirectory, fileName);

            // Save the stream to file
            using var fileStream = _fileSystem.OpenWrite(fullPath);
            await imageStream.CopyToAsync(fileStream, ct);

            _logger?.LogInformation("Cover image saved for book {BookId} at {Path}", bookId, fullPath);

            // Return relative path
            return _fileSystem.CombinePath("covers", fileName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save cover image for book {BookId}", bookId);
            throw;
        }
    }

    public Task<string?> GetCoverImagePathAsync(Guid bookId, CancellationToken ct = default)
    {
        try
        {
            var fileName = $"{bookId}.jpg";
            var fullPath = _fileSystem.CombinePath(_imagesDirectory, fileName);

            // Check if file exists
            if (_fileSystem.FileExists(fullPath))
            {
                return Task.FromResult<string?>(fullPath);
            }

            // Try alternative extensions
            var pngPath = _fileSystem.CombinePath(_imagesDirectory, $"{bookId}.png");
            if (_fileSystem.FileExists(pngPath))
            {
                return Task.FromResult<string?>(pngPath);
            }

            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get cover image path for book {BookId}", bookId);
            return Task.FromResult<string?>(null);
        }
    }

    public Task DeleteCoverImageAsync(Guid bookId, CancellationToken ct = default)
    {
        try
        {
            var fileName = $"{bookId}.jpg";
            var fullPath = _fileSystem.CombinePath(_imagesDirectory, fileName);

            if (_fileSystem.FileExists(fullPath))
            {
                _fileSystem.DeleteFile(fullPath);
                _logger?.LogInformation("Cover image deleted for book {BookId}", bookId);
            }

            // Also try to delete PNG version
            var pngPath = _fileSystem.CombinePath(_imagesDirectory, $"{bookId}.png");
            if (_fileSystem.FileExists(pngPath))
            {
                _fileSystem.DeleteFile(pngPath);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete cover image for book {BookId}", bookId);
            throw;
        }
    }

    public async Task<Stream?> DownloadImageFromUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            _logger?.LogInformation("Downloading image from URL: {Url}", url);

            // Sentinel: Use ResponseHeadersRead to validate headers before downloading body
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to download image from {Url}. Status: {StatusCode}",
                    url, response.StatusCode);
                return null;
            }

            // Sentinel: Validate Content-Type
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType) || !AllowedContentTypes.Contains(contentType))
            {
                _logger?.LogWarning("Rejected image download from {Url} due to invalid Content-Type: {ContentType}", url, contentType);
                return null;
            }

            // Sentinel: Validate Content-Length if present
            if (response.Content.Headers.ContentLength.HasValue)
            {
                if (response.Content.Headers.ContentLength.Value > MaxImageSize)
                {
                    _logger?.LogWarning("Rejected image download from {Url} due to size: {Size} bytes", url, response.Content.Headers.ContentLength.Value);
                    return null;
                }
            }

            // Sentinel: Read stream with size limit
            using var networkStream = await response.Content.ReadAsStreamAsync(ct);
            var memoryStream = new MemoryStream();

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                totalBytesRead += bytesRead;
                if (totalBytesRead > MaxImageSize)
                {
                    _logger?.LogWarning("Rejected image download from {Url} because it exceeded max size while reading", url);
                    return null;
                }
                await memoryStream.WriteAsync(buffer, 0, bytesRead, ct);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download image from URL: {Url}", url);
            return null;
        }
    }

    public async Task<string?> SaveCoverImageFromUrlAsync(string imageUrl, Guid bookId, CancellationToken ct = default)
    {
        try
        {
            var stream = await DownloadImageFromUrlAsync(imageUrl, ct);

            if (stream == null)
                return null;

            using (stream)
            {
                var path = await SaveCoverImageAsync(stream, bookId, ct);
                return path;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save cover image from URL for book {BookId}", bookId);
            return null;
        }
    }
}
