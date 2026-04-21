using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Smoke-level tests for ShareCardService. Private drawing helpers are
/// marked [ExcludeFromCodeCoverage] as pixel-perfect tests would be brittle.
/// </summary>
public class ShareCardServiceTests
{
    private readonly ShareCardService _service = new();

    private static readonly byte[] PngMagic = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 4 && bytes.Take(4).SequenceEqual(PngMagic);

    [Fact]
    public async Task GenerateBookCardAsync_MinimalData_ReturnsPng()
    {
        var data = new BookShareData
        {
            Title = "Minimal",
            Author = "Tester"
        };

        var result = await _service.GenerateBookCardAsync(data);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBookCardAsync_FullData_ReturnsPng()
    {
        var data = new BookShareData
        {
            Title = "Full Data Book",
            Author = "Author Name",
            PageCount = 320,
            TotalMinutesRead = 125,
            AverageRating = 4.5,
            CategoryRatings = new Dictionary<RatingCategory, int?>
            {
                { RatingCategory.Characters, 5 },
                { RatingCategory.Plot, 4 },
                { RatingCategory.WritingStyle, 5 },
                { RatingCategory.Pacing, 3 }
            }
        };

        var result = await _service.GenerateBookCardAsync(data);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBookCardAsync_WithCoverImage_ReturnsPng()
    {
        var data = new BookShareData
        {
            Title = "With Cover",
            Author = "Author",
            AverageRating = 4.2,
            CoverImageBytes = CreateDummyPng()
        };

        var result = await _service.GenerateBookCardAsync(data);

        result.Should().NotBeNull();
        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBookCardAsync_LowRating_ReturnsPng()
    {
        var data = new BookShareData
        {
            Title = "Low Rating",
            Author = "Author",
            AverageRating = 2.5
        };

        var result = await _service.GenerateBookCardAsync(data);

        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBookCardAsync_AllRatingCategories_ReturnsPng()
    {
        var allCategories = new Dictionary<RatingCategory, int?>();
        foreach (RatingCategory cat in Enum.GetValues(typeof(RatingCategory)))
        {
            allCategories[cat] = 3;
        }

        var data = new BookShareData
        {
            Title = "All Cats",
            Author = "Author",
            AverageRating = 4.0,
            CategoryRatings = allCategories
        };

        var result = await _service.GenerateBookCardAsync(data);

        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateStatsCardAsync_MinimalData_ReturnsPng()
    {
        var data = new StatsShareData
        {
            PeriodLabel = "April 2026"
        };

        var result = await _service.GenerateStatsCardAsync(data);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateStatsCardAsync_FullData_ReturnsPng()
    {
        var data = new StatsShareData
        {
            PeriodLabel = "2026",
            BooksCompleted = 24,
            PagesRead = 8500,
            MinutesRead = 7200,
            FavoriteGenre = "Fantasy",
            UserLevel = 12,
            AverageRating = 4.3,
            CurrentStreak = 25,
            TotalBooks = 48,
            TopBooks = new List<(string, string, double?)>
            {
                ("Top Book 1", "Author 1", 5.0),
                ("Top Book 2", "Author 2", 4.5),
                ("Top Book 3", "Author 3", 4.0)
            }
        };

        var result = await _service.GenerateStatsCardAsync(data);

        result.Should().NotBeNull();
        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateStatsCardAsync_NoTopBooks_ReturnsPng()
    {
        var data = new StatsShareData
        {
            PeriodLabel = "Monat",
            BooksCompleted = 0,
            PagesRead = 0,
            MinutesRead = 0,
            TopBooks = new List<(string, string, double?)>()
        };

        var result = await _service.GenerateStatsCardAsync(data);

        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateStatsCardAsync_HighMinutesRead_FormatsHoursCorrectly()
    {
        var data = new StatsShareData
        {
            PeriodLabel = "Jahr",
            BooksCompleted = 10,
            MinutesRead = 12000 // 200 hours
        };

        var result = await _service.GenerateStatsCardAsync(data);

        IsPng(result).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBookCardAsync_LongTitle_ReturnsPng()
    {
        var data = new BookShareData
        {
            Title = new string('A', 200),
            Author = new string('B', 100)
        };

        var result = await _service.GenerateBookCardAsync(data);

        IsPng(result).Should().BeTrue();
    }

    private static byte[] CreateDummyPng()
    {
        // Minimal 1x1 PNG
        using var bitmap = new SkiaSharp.SKBitmap(1, 1);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
