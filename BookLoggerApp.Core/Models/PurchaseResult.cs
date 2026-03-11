namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents the result of an in-app purchase attempt.
/// </summary>
public class PurchaseResult
{
    public bool Success { get; init; }
    public string? ProductId { get; init; }
    public string? PurchaseToken { get; init; }
    public string? ErrorMessage { get; init; }
    public bool WasCancelled { get; init; }

    public static PurchaseResult Succeeded(string productId, string? purchaseToken) => new()
    {
        Success = true,
        ProductId = productId,
        PurchaseToken = purchaseToken
    };

    public static PurchaseResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static PurchaseResult Cancelled() => new()
    {
        Success = false,
        WasCancelled = true
    };
}
