using BookLoggerApp.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class XpCalculatorTests
{
    [Theory]
    [InlineData(1, 100)] // 100 * 1^2
    [InlineData(2, 400)] // 100 * 2^2
    [InlineData(5, 2500)] // 100 * 5^2
    [InlineData(10, 10000)] // 100 * 10^2
    [InlineData(14, 19600)] // 100 * 14^2
    [InlineData(20, 40000)] // 100 * 20^2
    public void GetXpForLevel_ReturnsCorrectQuadraticValues(int level, int expectedXp)
    {
        // Act
        var result = XpCalculator.GetXpForLevel(level);

        // Assert
        result.Should().Be(expectedXp);
    }

    [Theory]
    // Loop subtracts each level's requirement; cumulative thresholds: L2 at 100, L3 at 500.
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(499, 2)]
    [InlineData(500, 3)]
    public void CalculateLevelFromXp_ReturnsCorrectLevel(int totalXp, int expectedLevel)
    {
        // Act
        var result = XpCalculator.CalculateLevelFromXp(totalXp);
        
        // Assert
        result.Should().Be(expectedLevel);
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
        // Act
        var result = XpCalculator.CalculateStreakBonusXp(streakDays);

        // Assert
        result.Should().Be(expectedXp);
    }

    [Fact]
    public void CalculateSessionXpBreakdown_IncludesScaledStreakBonus()
    {
        // Act
        var result = XpCalculator.CalculateSessionXpBreakdown(10, 2, 3);

        // Assert
        result.MinutesXp.Should().Be(50);
        result.PagesXp.Should().Be(40);
        result.LongSessionXp.Should().Be(0);
        result.StreakXp.Should().Be(400);
    }

    [Theory]
    [InlineData(1, 53)]      // 50 + 3
    [InlineData(2, 112)]     // 100 + 12
    [InlineData(5, 325)]     // 250 + 75
    [InlineData(10, 800)]    // 500 + 300
    [InlineData(20, 2200)]   // 1000 + 1200
    [InlineData(30, 4200)]   // 1500 + 2700
    [InlineData(33, 4917)]   // 1650 + 3267
    public void CalculateCoinsForLevel_ReturnsProgressiveValues(int level, int expectedCoins)
    {
        // Act
        var result = XpCalculator.CalculateCoinsForLevel(level);

        // Assert
        result.Should().Be(expectedCoins);
    }

    [Theory]
    [InlineData(100, 0.0, 100)]        // No boost
    [InlineData(100, 0.25, 125)]       // 25% boost, exact
    [InlineData(100, 0.5, 150)]        // 50% boost, exact
    [InlineData(1325, 0.5, 1988)]      // 1325 × 1.5 = 1987.5 → rounds up to 1988 (was 1987 with truncation)
    [InlineData(1000, 0.255, 1255)]    // 1000 × 1.255 = 1255.0 exact
    [InlineData(33, 0.5, 50)]          // 33 × 1.5 = 49.5 → rounds up to 50 (was 49 with truncation)
    public void ApplyPlantBoost_RoundsCorrectly(int baseXp, double boostPercentage, int expectedXp)
    {
        // Act
        var result = XpCalculator.ApplyPlantBoost(baseXp, (decimal)boostPercentage);

        // Assert
        result.Should().Be(expectedXp);
    }
}
