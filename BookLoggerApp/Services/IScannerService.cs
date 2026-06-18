namespace BookLoggerApp.Services;

/// <summary>Native barcode scanner bridge for Blazor.</summary>
public interface IScannerService
{
    /// <summary>Opens the scanner page; returns the barcode or null if cancelled.</summary>
    Task<string?> ScanBarcodeAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
