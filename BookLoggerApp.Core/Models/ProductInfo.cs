namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a product available for purchase in the store.
/// </summary>
public class ProductInfo
{
    public required string ProductId { get; init; }
    public required string Name { get; init; }
    public required string LocalizedPrice { get; init; }
    public decimal PriceAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
