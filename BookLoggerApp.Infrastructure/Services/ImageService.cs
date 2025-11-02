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

    public ImageService(ILogger<ImageService>? logger = null)
    {
        _logger = logger;

        // Get the app's local data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _imagesDirectory = Path.Combine(appDataPath, "covers");

        // Ensure directory exists
        Directory.CreateDirectory(_imagesDirectory);

        // Initialize HttpClient for downloading images
        _httpClient = new HttpClient
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
            var fullPath = Path.Combine(_imagesDirectory, fileName);

            // Save the stream to file
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await imageStream.CopyToAsync(fileStream, ct);

            _logger?.LogInformation("Cover image saved for book {BookId} at {Path}", bookId, fullPath);

            // Return relative path
            return Path.Combine("covers", fileName);
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
            var fullPath = Path.Combine(_imagesDirectory, fileName);

            // Check if file exists
            if (File.Exists(fullPath))
            {
                return Task.FromResult<string?>(fullPath);
            }

            // Try alternative extensions
            var pngPath = Path.Combine(_imagesDirectory, $"{bookId}.png");
            if (File.Exists(pngPath))
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
            var fullPath = Path.Combine(_imagesDirectory, fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger?.LogInformation("Cover image deleted for book {BookId}", bookId);
            }

            // Also try to delete PNG version
            var pngPath = Path.Combine(_imagesDirectory, $"{bookId}.png");
            if (File.Exists(pngPath))
            {
                File.Delete(pngPath);
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

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to download image from {Url}. Status: {StatusCode}",
                    url, response.StatusCode);
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
