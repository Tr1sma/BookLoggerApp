using BookLoggerApp.Core.Services.Analytics;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Analytics;

public class AnalyticsBucketsTests
{
    [Theory]
    [InlineData(-5, "0")]
    [InlineData(0, "0")]
    [InlineData(1, "1-5")]
    [InlineData(5, "1-5")]
    [InlineData(6, "6-20")]
    [InlineData(20, "6-20")]
    [InlineData(21, "21-50")]
    [InlineData(50, "21-50")]
    [InlineData(51, "51+")]
    [InlineData(9999, "51+")]
    public void BookCount_buckets_correctly(int n, string expected)
        => AnalyticsBuckets.BookCount(n).Should().Be(expected);

    [Theory]
    [InlineData(1, "1-5")]
    [InlineData(5, "1-5")]
    [InlineData(6, "6-10")]
    [InlineData(10, "6-10")]
    [InlineData(11, "11-20")]
    [InlineData(20, "11-20")]
    [InlineData(21, "21-35")]
    [InlineData(35, "21-35")]
    [InlineData(36, "36-50")]
    [InlineData(50, "36-50")]
    [InlineData(51, "51+")]
    public void Level_buckets_correctly(int n, string expected)
        => AnalyticsBuckets.Level(n).Should().Be(expected);

    [Theory]
    [InlineData(-1, "0")]
    [InlineData(0, "0")]
    [InlineData(1, "1-10")]
    [InlineData(10, "1-10")]
    [InlineData(11, "11-50")]
    [InlineData(50, "11-50")]
    [InlineData(51, "51-200")]
    [InlineData(200, "51-200")]
    [InlineData(201, "201-500")]
    [InlineData(500, "201-500")]
    [InlineData(501, "501+")]
    public void Pages_buckets_correctly(int n, string expected)
        => AnalyticsBuckets.Pages(n).Should().Be(expected);

    [Theory]
    [InlineData(0, "0-5")]
    [InlineData(5, "0-5")]
    [InlineData(6, "6-15")]
    [InlineData(15, "6-15")]
    [InlineData(16, "16-30")]
    [InlineData(30, "16-30")]
    [InlineData(31, "31-60")]
    [InlineData(60, "31-60")]
    [InlineData(61, "61-120")]
    [InlineData(120, "61-120")]
    [InlineData(121, "120+")]
    public void Minutes_buckets_correctly(int n, string expected)
        => AnalyticsBuckets.Minutes(n).Should().Be(expected);

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1-10")]
    [InlineData(10, "1-10")]
    [InlineData(11, "11-50")]
    [InlineData(1001, "1000+")]
    public void XpDelta_buckets_correctly(int n, string expected)
        => AnalyticsBuckets.XpDelta(n).Should().Be(expected);

    [Theory]
    [InlineData(0, "0-50")]
    [InlineData(50, "0-50")]
    [InlineData(51, "51-200")]
    [InlineData(5001, "5000+")]
    public void Coins_buckets_correctly(int n, string expected)
        => AnalyticsBuckets.Coins(n).Should().Be(expected);

    [Theory]
    [InlineData(null, "none")]
    [InlineData(0, "0")]
    [InlineData(3, "3")]
    [InlineData(5, "5")]
    public void RatingInt_handles_nullable(int? n, string expected)
        => AnalyticsBuckets.RatingInt(n).Should().Be(expected);

    [Theory]
    [InlineData(999_999L, "<1MB")]
    [InlineData(1_000_000L, "1-5MB")]
    [InlineData(5_000_000L, "5-25MB")]
    [InlineData(24_999_999L, "5-25MB")]
    [InlineData(25_000_000L, "25MB+")]
    public void SizeBytes_buckets_correctly(long n, string expected)
        => AnalyticsBuckets.SizeBytes(n).Should().Be(expected);

    [Theory]
    [InlineData(0d, "0")]
    [InlineData(24d, "1-24")]
    [InlineData(25d, "25-49")]
    [InlineData(49d, "25-49")]
    [InlineData(50d, "50-74")]
    [InlineData(99d, "75-99")]
    [InlineData(100d, "100")]
    [InlineData(150d, "100")]
    public void Progress_buckets_correctly(double percent, string expected)
        => AnalyticsBuckets.Progress(percent).Should().Be(expected);

    [Fact]
    public void InstallAge_handles_fixed_clock()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        AnalyticsBuckets.InstallAge(created, created.AddHours(5)).Should().Be("<1d");
        AnalyticsBuckets.InstallAge(created, created.AddDays(3)).Should().Be("1-7d");
        AnalyticsBuckets.InstallAge(created, created.AddDays(15)).Should().Be("8-30d");
        AnalyticsBuckets.InstallAge(created, created.AddDays(60)).Should().Be("31-90d");
        AnalyticsBuckets.InstallAge(created, created.AddDays(200)).Should().Be("90d+");
    }

    [Fact]
    public void DaysSince_handles_fixed_clock()
    {
        var t = new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);
        AnalyticsBuckets.DaysSince(t, t.AddHours(2)).Should().Be("<1d");
        AnalyticsBuckets.DaysSince(t, t.AddDays(2)).Should().Be("1-3d");
        AnalyticsBuckets.DaysSince(t, t.AddDays(5)).Should().Be("4-7d");
        AnalyticsBuckets.DaysSince(t, t.AddDays(10)).Should().Be("8-14d");
        AnalyticsBuckets.DaysSince(t, t.AddDays(20)).Should().Be("15-30d");
        AnalyticsBuckets.DaysSince(t, t.AddDays(60)).Should().Be("30d+");
    }
}
