namespace BookLoggerApp.Services;

/// <summary>
/// Service to handle native barcode scanning from Blazor.
/// </summary>
public interface IScannerService
{
    /// <summary>
    /// Opens the native scanner page and returns the scanned barcode.
    /// </summary>
    /// <returns>Scanned barcode string, or null if cancelled.</returns>
    Task<string?> ScanBarcodeAsync();
}
