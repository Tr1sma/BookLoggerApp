using System.Net.Http;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service for managing cover images and other image assets.
/// </summary>
public class ImageService : IImageService
{
    private readonly string _imagesDirectory;
    private readonly string _thumbnailsDirectory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageService>? _logger;
    private readonly IFileSystem _fileSystem;

    public ImageService(IFileSystem fileSystem, ILogger<ImageService>? logger = null)
        : this(fileSystem, logger, null)
    {
    }
    // Public constructor for testing or custom HttpClient usage
    public ImageService(IFileSystem fileSystem, ILogger<ImageService>? logger, HttpClient? httpClient)
    {
        _fileSystem = fileSystem;
        _logger = logger;

        // Get the app's local data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _imagesDirectory = _fileSystem.CombinePath(appDataPath, "covers");
        _thumbnailsDirectory = _fileSystem.CombinePath(_imagesDirectory, "thumbs");

        // Ensure directories exist
        _fileSystem.CreateDirectory(_imagesDirectory);
        _fileSystem.CreateDirectory(_thumbnailsDirectory);

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

            // Invalidate cached thumbnail
            DeleteThumbnail(bookId);

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

            // Delete cached thumbnail
            DeleteThumbnail(bookId);

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

            // Sentinel: Security enhancement - Use ResponseHeadersRead to inspect headers before downloading body
            // This prevents downloading massive files (DoS risk) or wrong content types
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            try
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Failed to download image from {Url}. Status: {StatusCode}",
                        url, response.StatusCode);
                    response.Dispose();
                    return null;
                }

                // Sentinel: Check Content-Length (Max 10MB)
                // If Content-Length is missing, we must be careful.
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    if (response.Content.Headers.ContentLength > 10 * 1024 * 1024)
                    {
                        _logger?.LogWarning("Image too large ({Size} bytes) from {Url}",
                            response.Content.Headers.ContentLength, url);
                        response.Dispose();
                        return null;
                    }
                }
                else
                {
                    // Warn about missing content length
                    _logger?.LogWarning("Missing Content-Length header from {Url}. Proceeding with caution.", url);
                }

                // Sentinel: Check Content-Type
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("Invalid content type ({Type}) from {Url}", contentType, url);
                    response.Dispose();
                    return null;
                }

                // Copy the network stream to a MemoryStream so we can safely dispose the response.
                // Content-Length is already validated above (max 10MB), so buffering is safe.
                var networkStream = await response.Content.ReadAsStreamAsync(ct);
                var memoryStream = new MemoryStream();
                await networkStream.CopyToAsync(memoryStream, ct);
                memoryStream.Position = 0;
                response.Dispose();
                return memoryStream;
            }
            catch
            {
                response.Dispose();
                throw;
            }
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

    public async Task<(byte[] Bytes, string MimeType)?> GetResizedCoverImageAsync(
        Guid bookId, int maxWidth = 400, int maxHeight = 600, CancellationToken ct = default)
    {
        try
        {
            // Check for cached thumbnail first
            var thumbPath = _fileSystem.CombinePath(_thumbnailsDirectory, $"{bookId}.jpg");
            if (_fileSystem.FileExists(thumbPath))
            {
                var cachedBytes = await _fileSystem.ReadAllBytesAsync(thumbPath, ct);
                return (cachedBytes, "image/jpeg");
            }

            // Get original image path
            var originalPath = await GetCoverImagePathAsync(bookId, ct);
            if (originalPath == null)
                return null;

            var originalBytes = await _fileSystem.ReadAllBytesAsync(originalPath, ct);
            if (originalBytes.Length == 0)
                return null;

            // Decode with SkiaSharp
            using var original = SKBitmap.Decode(originalBytes);
            if (original == null)
            {
                _logger?.LogWarning("Failed to decode cover image for book {BookId}", bookId);
                return null;
            }

            // If already small enough, cache as-is and return
            if (original.Width <= maxWidth && original.Height <= maxHeight)
            {
                await _fileSystem.WriteAllBytesAsync(thumbPath, originalBytes, ct);
                var mimeType = originalPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? "image/png" : "image/jpeg";
                return (originalBytes, mimeType);
            }

            // Calculate new dimensions maintaining aspect ratio
            var ratioX = (float)maxWidth / original.Width;
            var ratioY = (float)maxHeight / original.Height;
            var ratio = Math.Min(ratioX, ratioY);
            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);

            // Resize
            using var resized = original.Resize(
                new SKImageInfo(newWidth, newHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

            if (resized == null)
            {
                _logger?.LogWarning("Failed to resize cover image for book {BookId}", bookId);
                return null;
            }

            // Encode to JPEG (quality 85 is a good balance of size vs quality)
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            var resizedBytes = data.ToArray();

            // Cache to disk
            await _fileSystem.WriteAllBytesAsync(thumbPath, resizedBytes, ct);

            _logger?.LogInformation(
                "Resized cover image for book {BookId}: {OrigW}x{OrigH} -> {NewW}x{NewH} ({OrigSize} -> {NewSize} bytes)",
                bookId, original.Width, original.Height, newWidth, newHeight,
                originalBytes.Length, resizedBytes.Length);

            return (resizedBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get resized cover image for book {BookId}", bookId);
            return null;
        }
    }

    private void DeleteThumbnail(Guid bookId)
    {
        try
        {
            var thumbPath = _fileSystem.CombinePath(_thumbnailsDirectory, $"{bookId}.jpg");
            if (_fileSystem.FileExists(thumbPath))
            {
                _fileSystem.DeleteFile(thumbPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete thumbnail for book {BookId}", bookId);
        }
    }
}
