using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing cover images and other image assets.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Saves a cover image for a book from a stream.
    /// </summary>
    /// <param name="imageStream">The image data stream.</param>
    /// <param name="bookId">The book ID to associate with the image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The relative path to the saved image.</returns>
    Task<string> SaveCoverImageAsync(Stream imageStream, Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Gets the full path to a book's cover image.
    /// </summary>
    /// <param name="bookId">The book ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full path to the cover image, or null if not found.</returns>
    Task<string?> GetCoverImagePathAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a book's cover image.
    /// </summary>
    /// <param name="bookId">The book ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteCoverImageAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Downloads an image from a URL and returns it as a stream.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The image stream, or null if download failed.</returns>
    Task<Stream?> DownloadImageFromUrlAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Saves a downloaded image from URL for a book.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="bookId">The book ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The relative path to the saved image, or null if download failed.</returns>
    Task<string?> SaveCoverImageFromUrlAsync(string imageUrl, Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Gets a resized cover image for efficient display. Returns cached thumbnail if available,
    /// otherwise resizes the original and caches the result.
    /// </summary>
    /// <param name="bookId">The book ID.</param>
    /// <param name="maxWidth">Maximum width in pixels (default 400).</param>
    /// <param name="maxHeight">Maximum height in pixels (default 600).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (image bytes, MIME type), or null if no cover image exists.</returns>
    Task<(byte[] Bytes, string MimeType)?> GetResizedCoverImageAsync(Guid bookId, int maxWidth = 400, int maxHeight = 600, CancellationToken ct = default);
}
