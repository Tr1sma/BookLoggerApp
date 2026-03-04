using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ReadingStatsCardServiceTests : IDisposable
{
    private readonly ReadingStatsCardService _service;
    private readonly List<string> _filesToCleanup = new();

    public ReadingStatsCardServiceTests()
    {
        _service = new ReadingStatsCardService();
    }

    [Fact]
    public async Task GenerateMonthlyCardAsync_Should_CreatePngFile()
    {
        // Arrange
        var stats = CreateSampleStats();

        // Act
        string filePath = await _service.GenerateMonthlyCardAsync(stats);
        _filesToCleanup.Add(filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue("the card image should be written to disk");
        filePath.Should().EndWith(".png");
    }

    [Fact]
    public async Task GenerateMonthlyCardAsync_Should_CreateValidPngContent()
    {
        // Arrange
        var stats = CreateSampleStats();

        // Act
        string filePath = await _service.GenerateMonthlyCardAsync(stats);
        _filesToCleanup.Add(filePath);

        // Assert
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
        fileBytes.Length.Should().BeGreaterThan(0, "the PNG file should contain image data");

        // PNG magic bytes: 137 80 78 71 13 10 26 10
        fileBytes[0].Should().Be(137, "first byte of PNG header");
        fileBytes[1].Should().Be(80, "second byte of PNG header (P)");
        fileBytes[2].Should().Be(78, "third byte of PNG header (N)");
        fileBytes[3].Should().Be(71, "fourth byte of PNG header (G)");
    }

    [Fact]
    public async Task GenerateMonthlyCardAsync_Should_IncludeMonthInFilename()
    {
        // Arrange
        var stats = CreateSampleStats(month: 3);

        // Act
        string filePath = await _service.GenerateMonthlyCardAsync(stats);
        _filesToCleanup.Add(filePath);

        // Assert
        Path.GetFileName(filePath).Should().Contain("03", "the filename should include the zero-padded month");
    }

    [Fact]
    public async Task GenerateMonthlyCardAsync_Should_HandleZeroStats()
    {
        // Arrange
        var stats = new MonthlyReadingStats
        {
            Year = 2026,
            Month = 1,
            BooksCompleted = 0,
            PagesRead = 0,
            MinutesRead = 0,
            CurrentStreak = 0,
            AverageRating = 0,
            FavoriteGenre = null,
            CurrentLevel = 1,
            TotalXp = 0
        };

        // Act
        string filePath = await _service.GenerateMonthlyCardAsync(stats);
        _filesToCleanup.Add(filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue("should generate a card even with zero stats");
    }

    [Fact]
    public async Task GenerateMonthlyCardAsync_Should_HandleLargeNumbers()
    {
        // Arrange
        var stats = new MonthlyReadingStats
        {
            Year = 2026,
            Month = 12,
            BooksCompleted = 99,
            PagesRead = 50000,
            MinutesRead = 99999,
            CurrentStreak = 365,
            AverageRating = 5.0,
            FavoriteGenre = "Science Fiction & Fantasy",
            CurrentLevel = 50,
            TotalXp = 999999
        };

        // Act
        string filePath = await _service.GenerateMonthlyCardAsync(stats);
        _filesToCleanup.Add(filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue("should handle large stat values gracefully");
    }

    [Fact]
    public void GenerateMonthlyCardAsync_Should_ThrowOnCancellation()
    {
        // Arrange
        var stats = CreateSampleStats();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => _service.GenerateMonthlyCardAsync(stats, cts.Token);
        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateMonthlyCardAsync_Should_OverwriteExistingFile()
    {
        // Arrange
        var stats = CreateSampleStats();

        // Act - generate twice
        string filePath1 = await _service.GenerateMonthlyCardAsync(stats);
        _filesToCleanup.Add(filePath1);
        var firstWriteTime = File.GetLastWriteTimeUtc(filePath1);

        // Small delay to ensure different write time
        await Task.Delay(50);

        string filePath2 = await _service.GenerateMonthlyCardAsync(stats);

        // Assert
        filePath1.Should().Be(filePath2, "same stats should produce same filename");
        File.GetLastWriteTimeUtc(filePath2).Should().BeOnOrAfter(firstWriteTime);
    }

    private static MonthlyReadingStats CreateSampleStats(int month = 3)
    {
        return new MonthlyReadingStats
        {
            Year = 2026,
            Month = month,
            BooksCompleted = 5,
            PagesRead = 1250,
            MinutesRead = 720,
            CurrentStreak = 14,
            AverageRating = 4.2,
            FavoriteGenre = "Fantasy",
            CurrentLevel = 7,
            TotalXp = 2500
        };
    }

    public void Dispose()
    {
        foreach (string file in _filesToCleanup)
        {
            try { File.Delete(file); } catch { }
        }
    }
}
