using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service for managing cover images and other image assets.
/// </summary>
public class ImageService : IImageService
{
    private readonly string _imagesDirectory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageService>? _logger;
    private readonly IFileSystem _fileSystem;

    // Fix: Allow HttpClient injection for testing
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

            // Fix: Use HttpCompletionOption.ResponseHeadersRead to check headers before downloading body
            // This allows us to validate Content-Type and Content-Length before consuming resources
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to download image from {Url}. Status: {StatusCode}",
                    url, response.StatusCode);
                return null;
            }

            // Validate Content-Type
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                 _logger?.LogWarning("Invalid content type for image download: {ContentType}", contentType);
                 return null;
            }

            // Validate Content-Length if present (e.g. max 10MB)
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > 10 * 1024 * 1024)
            {
                _logger?.LogWarning("Image too large: {Size} bytes", contentLength);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return stream;
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
