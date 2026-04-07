namespace BookLoggerApp.Services;

/// <summary>
/// Service to handle native barcode scanning from Blazor.
/// </summary>
public interface IScannerService
{
    /// <summary>
    /// Opens the native scanner page and returns the scanned barcode.
    /// </summary>
    /// <param name="timeout">Optional timeout for automatic cancellation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Scanned barcode string, or null if cancelled.</returns>
    Task<string?> ScanBarcodeAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
