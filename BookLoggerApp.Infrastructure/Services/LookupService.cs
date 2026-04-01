using System.Net;
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
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [1000, 3000, 6000];
    private const string QuotaKeyword = "quota";
    private const string RateLimitKeyword = "rateLimitExceeded";
    private const string DailyLimitKeyword = "dailyLimitExceeded";
    private const string ResourceExhaustedKeyword = "RESOURCE_EXHAUSTED";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _httpClient;
    private readonly ILogger<LookupService>? _logger;
    private readonly string? _googleBooksApiKey;
    private bool _useApiKey = true;

    public LookupService(HttpClient httpClient, ILogger<LookupService>? logger = null, string? googleBooksApiKey = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _googleBooksApiKey = string.IsNullOrWhiteSpace(googleBooksApiKey)
            ? ApiKeys.GoogleBooks
            : googleBooksApiKey;
    }

    public async Task<BookMetadata?> LookupByISBNAsync(string isbn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return null;

        // Clean ISBN (remove dashes and spaces)
        isbn = isbn.Replace("-", "").Replace(" ", "");

        _logger?.LogInformation("Looking up book by ISBN: {ISBN}", isbn);

        var queryParams = $"q=isbn:{Uri.EscapeDataString(isbn)}";
        var searchResult = await GetWithRetryAsync<GoogleBooksSearchResult>(queryParams, ct);

        if (searchResult?.Items == null || searchResult.Items.Count == 0)
        {
            _logger?.LogInformation("No results found for ISBN: {ISBN}", isbn);
            return null;
        }

        var volumeInfo = searchResult.Items[0].VolumeInfo;
        var metadata = MapToBookMetadata(volumeInfo, isbn);

        _logger?.LogInformation("Found book: {Title} by {Author}", metadata.Title, metadata.Author);

        return metadata;
    }

    public async Task<IReadOnlyList<BookMetadata>> SearchBooksAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<BookMetadata>();

        _logger?.LogInformation("Searching books with query: {Query}", query);

        var queryParams = $"q={Uri.EscapeDataString(query)}&maxResults=10";
        var searchResult = await GetWithRetryAsync<GoogleBooksSearchResult>(queryParams, ct);

        if (searchResult?.Items == null || searchResult.Items.Count == 0)
        {
            _logger?.LogInformation("No results found for query: {Query}", query);
            return Array.Empty<BookMetadata>();
        }

        var results = searchResult.Items
            .Select(item => MapToBookMetadata(item.VolumeInfo, null))
            .ToList();

        _logger?.LogInformation("Found {Count} books for query: {Query}", results.Count, query);

        return results;
    }

    private string BuildUrl(string queryParams, bool includeApiKey)
    {
        var url = $"{GoogleBooksApiBaseUrl}?{queryParams}";
        if (includeApiKey && !string.IsNullOrWhiteSpace(_googleBooksApiKey))
            url += $"&key={Uri.EscapeDataString(_googleBooksApiKey)}";
        return url;
    }

    private async Task<T?> GetWithRetryAsync<T>(string queryParams, CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        try
        {
            var shouldUseApiKey = _useApiKey && !string.IsNullOrWhiteSpace(_googleBooksApiKey);
            response = await SendWithRetryAsync(BuildUrl(queryParams, includeApiKey: shouldUseApiKey), ct);

            if (shouldUseApiKey && !response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (IsQuotaOrRateLimitError(response.StatusCode, body))
                {
                    _logger?.LogWarning(
                        "Google Books API key hit quota/rate limits ({StatusCode}). Retrying lookup without API key.",
                        (int)response.StatusCode);

                    _useApiKey = false;
                    response.Dispose();
                    response = await SendWithRetryAsync(BuildUrl(queryParams, includeApiKey: false), ct);
                }
                else
                {
                    ThrowHttpRequestException(response.StatusCode, body);
                }
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                ThrowHttpRequestException(response.StatusCode, json);
            }

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);

        for (int i = 0; i < MaxRetries && response.StatusCode == HttpStatusCode.TooManyRequests; i++)
        {
            _logger?.LogWarning("Retry {RetryCount}/{MaxRetries} after 429 Too Many Requests", i + 1, MaxRetries);
            response.Dispose();
            await Task.Delay(RetryDelaysMs[i], ct);
            response = await _httpClient.GetAsync(url, ct);
        }

        return response;
    }

    private static bool IsQuotaOrRateLimitError(HttpStatusCode statusCode, string body)
    {
        if (statusCode == HttpStatusCode.TooManyRequests)
            return true;

        if (statusCode != HttpStatusCode.Forbidden)
            return false;

        return body.Contains(QuotaKeyword, StringComparison.OrdinalIgnoreCase) ||
               body.Contains(RateLimitKeyword, StringComparison.OrdinalIgnoreCase) ||
               body.Contains(DailyLimitKeyword, StringComparison.OrdinalIgnoreCase) ||
               body.Contains(ResourceExhaustedKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowHttpRequestException(HttpStatusCode statusCode, string body)
    {
        _logger?.LogError("Google Books API error {StatusCode}: {Body}", (int)statusCode, body);
        throw new HttpRequestException(
            $"HTTP {(int)statusCode}: {body}",
            null,
            statusCode);
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
            PageCount = volumeInfo.PageCount ?? volumeInfo.PrintedPageCount,
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
        public int? PrintedPageCount { get; set; }
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
