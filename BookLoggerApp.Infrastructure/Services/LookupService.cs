using System.Text.Json;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service for looking up book metadata from Google Books API.
/// </summary>
public class LookupService : ILookupService
{
    private const string GoogleBooksApiBaseUrl = "https://www.googleapis.com/books/v1/volumes";
    private readonly HttpClient _httpClient;
    private readonly ILogger<LookupService>? _logger;

    public LookupService(ILogger<LookupService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<BookMetadata?> LookupByISBNAsync(string isbn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return null;

        try
        {
            // Clean ISBN (remove dashes and spaces)
            isbn = isbn.Replace("-", "").Replace(" ", "");

            _logger?.LogInformation("Looking up book by ISBN: {ISBN}", isbn);

            // Query Google Books API by ISBN
            var url = $"{GoogleBooksApiBaseUrl}?q=isbn:{isbn}";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Google Books API returned status {StatusCode} for ISBN {ISBN}",
                    response.StatusCode, isbn);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var searchResult = JsonSerializer.Deserialize<GoogleBooksSearchResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (searchResult?.Items == null || searchResult.Items.Count == 0)
            {
                _logger?.LogInformation("No results found for ISBN: {ISBN}", isbn);
                return null;
            }

            // Take the first result
            var volumeInfo = searchResult.Items[0].VolumeInfo;
            var metadata = MapToBookMetadata(volumeInfo, isbn);

            _logger?.LogInformation("Found book: {Title} by {Author}", metadata.Title, metadata.Author);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to lookup ISBN: {ISBN}", isbn);
            return null;
        }
    }

    public async Task<IReadOnlyList<BookMetadata>> SearchBooksAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<BookMetadata>();

        try
        {
            _logger?.LogInformation("Searching books with query: {Query}", query);

            // Query Google Books API
            var url = $"{GoogleBooksApiBaseUrl}?q={Uri.EscapeDataString(query)}&maxResults=10";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Google Books API returned status {StatusCode} for query {Query}",
                    response.StatusCode, query);
                return Array.Empty<BookMetadata>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var searchResult = JsonSerializer.Deserialize<GoogleBooksSearchResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (searchResult?.Items == null || searchResult.Items.Count == 0)
            {
                _logger?.LogInformation("No results found for query: {Query}", query);
                return Array.Empty<BookMetadata>();
            }

            // Map all results
            var results = searchResult.Items
                .Select(item => MapToBookMetadata(item.VolumeInfo, null))
                .ToList();

            _logger?.LogInformation("Found {Count} books for query: {Query}", results.Count, query);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search books with query: {Query}", query);
            return Array.Empty<BookMetadata>();
        }
    }

    private BookMetadata MapToBookMetadata(GoogleBooksVolumeInfo volumeInfo, string? isbn)
    {
        // Extract ISBN if not provided
        if (string.IsNullOrWhiteSpace(isbn))
        {
            isbn = volumeInfo.IndustryIdentifiers?
                .FirstOrDefault(id => id.Type == "ISBN_13" || id.Type == "ISBN_10")?.Identifier;
        }

        // Extract publication year
        int? publicationYear = null;
        if (!string.IsNullOrWhiteSpace(volumeInfo.PublishedDate) &&
            volumeInfo.PublishedDate.Length >= 4 &&
            int.TryParse(volumeInfo.PublishedDate.Substring(0, 4), out var year))
        {
            publicationYear = year;
        }

        return new BookMetadata
        {
            Title = volumeInfo.Title ?? string.Empty,
            Author = volumeInfo.Authors != null && volumeInfo.Authors.Count > 0
                ? string.Join(", ", volumeInfo.Authors)
                : string.Empty,
            ISBN = isbn ?? string.Empty,
            PageCount = volumeInfo.PageCount,
            Publisher = volumeInfo.Publisher,
            PublicationYear = publicationYear,
            Description = volumeInfo.Description,
            Language = volumeInfo.Language,
            CoverImageUrl = volumeInfo.ImageLinks?.Thumbnail?.Replace("http://", "https://"),
            Categories = volumeInfo.Categories ?? new List<string>()
        };
    }

    // Google Books API response models
    private class GoogleBooksSearchResult
    {
        public List<GoogleBooksItem> Items { get; set; } = new();
    }

    private class GoogleBooksItem
    {
        public GoogleBooksVolumeInfo VolumeInfo { get; set; } = new();
    }

    private class GoogleBooksVolumeInfo
    {
        public string? Title { get; set; }
        public List<string>? Authors { get; set; }
        public string? Publisher { get; set; }
        public string? PublishedDate { get; set; }
        public string? Description { get; set; }
        public List<GoogleBooksIdentifier>? IndustryIdentifiers { get; set; }
        public int? PageCount { get; set; }
        public List<string>? Categories { get; set; }
        public GoogleBooksImageLinks? ImageLinks { get; set; }
        public string? Language { get; set; }
    }

    private class GoogleBooksIdentifier
    {
        public string Type { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
    }

    private class GoogleBooksImageLinks
    {
        public string? Thumbnail { get; set; }
        public string? SmallThumbnail { get; set; }
    }
}
