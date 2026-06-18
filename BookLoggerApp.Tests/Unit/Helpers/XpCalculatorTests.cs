using BookLoggerApp.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class XpCalculatorTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 400)]
    [InlineData(5, 2500)]
    [InlineData(10, 10000)]
    [InlineData(14, 19600)]
    [InlineData(20, 40000)]
    public void GetXpForLevel_ReturnsCorrectQuadraticValues(int level, int expectedXp)
    {
        XpCalculator.GetXpForLevel(level).Should().Be(expectedXp);
    }

    [Theory]
    [InlineData(0, 1)]   // 0 < 100 → stays L1
    [InlineData(99, 1)]  // 99 < 100 → stays L1
    [InlineData(100, 2)] // 100 - 100 = 0 < 400 → L2
    [InlineData(499, 2)] // 499 - 100 = 399 < 400 → L2
    [InlineData(500, 3)] // 500 - 100 - 400 = 0 < 900 → L3
    public void CalculateLevelFromXp_ReturnsCorrectLevel(int totalXp, int expectedLevel)
    {
        XpCalculator.CalculateLevelFromXp(totalXp).Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 200)]
    [InlineData(3, 400)]
    [InlineData(7, 1050)]
    [InlineData(14, 2250)]
    [InlineData(30, 4850)]
    public void CalculateStreakBonusXp_ReturnsScaledBonus(int streakDays, int expectedXp)
    {
        XpCalculator.CalculateStreakBonusXp(streakDays).Should().Be(expectedXp);
    }

    [Fact]
    public void CalculateSessionXpBreakdown_IncludesScaledStreakBonus()
    {
        var result = XpCalculator.CalculateSessionXpBreakdown(10, 2, 3);

        result.MinutesXp.Should().Be(50);
        result.PagesXp.Should().Be(40);
        result.LongSessionXp.Should().Be(0);
        result.StreakXp.Should().Be(400);
    }

    [Theory]
    [InlineData(1, 53)]
    [InlineData(2, 112)]
    [InlineData(5, 325)]
    [InlineData(10, 800)]
    [InlineData(20, 2200)]
    [InlineData(30, 4200)]
    [InlineData(33, 4917)]
    public void CalculateCoinsForLevel_ReturnsProgressiveValues(int level, int expectedCoins)
    {
        XpCalculator.CalculateCoinsForLevel(level).Should().Be(expectedCoins);
    }

    [Theory]
    [InlineData(100, 0.0, 100)]
    [InlineData(100, 0.25, 125)]
    [InlineData(100, 0.5, 150)]
    [InlineData(1325, 0.5, 1988)]   // 1987.5 → ceiling
    [InlineData(1000, 0.255, 1255)]
    [InlineData(33, 0.5, 50)]       // 49.5 → ceiling
    public void ApplyPlantBoost_RoundsCorrectly(int baseXp, double boostPercentage, int expectedXp)
    {
        XpCalculator.ApplyPlantBoost(baseXp, (decimal)boostPercentage).Should().Be(expectedXp);
    }
}
