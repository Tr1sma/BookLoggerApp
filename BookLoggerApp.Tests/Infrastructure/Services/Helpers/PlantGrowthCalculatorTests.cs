using FluentAssertions;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure.Services.Helpers;

public class PlantGrowthCalculatorTests
{
    #region XP Calculation Tests

    [Theory]
    [InlineData(1, 0)]      // Level 1 requires 0 XP
    [InlineData(2, 150)]    // Level 2 requires 100 * 1.5^1 = 150 XP
    [InlineData(3, 225)]    // Level 3 requires 100 * 1.5^2 = 225 XP
    [InlineData(4, 337)]    // Level 4 requires 100 * 1.5^3 = 337 XP
    [InlineData(5, 506)]    // Level 5 requires 100 * 1.5^4 = 506 XP
    public void GetXpForLevel_ShouldReturnCorrectXp(int level, int expectedXp)
    {
        // Act
        var xp = PlantGrowthCalculator.GetXpForLevel(level);

        // Assert
        xp.Should().BeCloseTo(expectedXp, 2); // Allow 2 XP tolerance for rounding
    }

    [Fact]
    public void GetXpForLevel_WithFasterGrowthRate_ShouldRequireLessXp()
    {
        // Arrange
        int level = 5;
        double fasterGrowthRate = 1.5; // 50% faster growth

        // Act
        int normalXp = PlantGrowthCalculator.GetXpForLevel(level);
        int fasterXp = PlantGrowthCalculator.GetXpForLevel(level, fasterGrowthRate);

        // Assert
        fasterXp.Should().BeLessThan(normalXp);
        fasterXp.Should().BeCloseTo((int)(normalXp / fasterGrowthRate), 2);
    }

    [Fact]
    public void GetTotalXpForLevel_ShouldReturnCumulativeXp()
    {
        // Arrange
        int level = 3;

        // Act
        var totalXp = PlantGrowthCalculator.GetTotalXpForLevel(level);

        // Assert - Level 3 = 150 (L2) + 225 (L3) = 375
        totalXp.Should().BeCloseTo(375, 2);
    }

    [Theory]
    [InlineData(0, 1)]      // 0 XP = Level 1
    [InlineData(150, 2)]    // 150 XP = Level 2 (total for L2 = 150)
    [InlineData(375, 3)]    // 375 XP = Level 3 (total for L3 = 375)
    [InlineData(712, 4)]    // 712 XP = Level 4 (total for L4 = 712)
    public void CalculateLevelFromXp_ShouldReturnCorrectLevel(int totalXp, int expectedLevel)
    {
        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromXp(totalXp);

        // Assert
        level.Should().Be(expectedLevel);
    }

    [Fact]
    public void CalculateLevelFromXp_WithMaxLevel_ShouldNotExceedMax()
    {
        // Arrange
        int hugeXp = 1000000;
        int maxLevel = 10;

        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromXp(hugeXp, 1.0, maxLevel);

        // Assert
        level.Should().BeLessThanOrEqualTo(maxLevel);
    }

    [Fact]
    public void GetXpToNextLevel_ShouldReturnRemainingXpNeeded()
    {
        // Arrange - Player is level 2 with 200 total XP (50 into level 2)
        int currentLevel = 2;
        int currentXp = 200;

        // Act
        var xpToNext = PlantGrowthCalculator.GetXpToNextLevel(currentLevel, currentXp);

        // Assert - Level 3 requires 225 XP from level 2, player has 50 into L2, needs 175 more
        // Total for L2 = 150, Total for L3 = 375, XP into L2 = 200-150 = 50, XP remaining = 225 - 50 = 175
        xpToNext.Should().BeCloseTo(175, 2);
    }

    [Fact]
    public void GetXpPercentage_ShouldReturnProgressPercentage()
    {
        // Arrange - Level 2, 200 XP (50 into level 2 out of 225 needed for L3)
        int currentLevel = 2;
        int currentXp = 200;

        // Act
        var percentage = PlantGrowthCalculator.GetXpPercentage(currentLevel, currentXp);

        // Assert - Total for L2=150, Total for L3=375, XP into L2=50, XP needed=225
        // Percentage: 50/225 * 100 = ~22%
        percentage.Should().BeCloseTo(22, 2);
    }

    [Fact]
    public void CanLevelUp_WhenEnoughXp_ShouldReturnTrue()
    {
        // Arrange - Level 2 with 375 total XP (enough for level 3)
        // Total XP for L3 = 375 (150 + 225)
        int currentLevel = 2;
        int currentXp = 375;
        double growthRate = 1.0;
        int maxLevel = 10;

        // Act
        var canLevel = PlantGrowthCalculator.CanLevelUp(currentLevel, currentXp, growthRate, maxLevel);

        // Assert
        canLevel.Should().BeTrue();
    }

    [Fact]
    public void CanLevelUp_WhenNotEnoughXp_ShouldReturnFalse()
    {
        // Arrange - Level 2 with 300 XP (not enough for level 3 which needs 375)
        int currentLevel = 2;
        int currentXp = 300;
        double growthRate = 1.0;
        int maxLevel = 10;

        // Act
        var canLevel = PlantGrowthCalculator.CanLevelUp(currentLevel, currentXp, growthRate, maxLevel);

        // Assert
        canLevel.Should().BeFalse();
    }

    [Fact]
    public void CanLevelUp_WhenAtMaxLevel_ShouldReturnFalse()
    {
        // Arrange
        int currentLevel = 10;
        int currentXp = 1000000;
        double growthRate = 1.0;
        int maxLevel = 10;

        // Act
        var canLevel = PlantGrowthCalculator.CanLevelUp(currentLevel, currentXp, growthRate, maxLevel);

        // Assert
        canLevel.Should().BeFalse();
    }

    #endregion

    #region Plant Status Tests

    [Fact]
    public void CalculatePlantStatus_Healthy_WhenRecentlyWatered()
    {
        // Arrange - Watered 1 day ago, needs water every 3 days
        var lastWatered = DateTime.UtcNow.AddDays(-1);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Healthy);
    }

    [Fact]
    public void CalculatePlantStatus_Thirsty_WhenOverdue()
    {
        // Arrange - Watered 3.5 days ago, needs water every 3 days
        var lastWatered = DateTime.UtcNow.AddDays(-3.5);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Thirsty);
    }

    [Fact]
    public void CalculatePlantStatus_Wilting_WhenLongOverdue()
    {
        // Arrange - Watered 5 days ago, needs water every 3 days
        var lastWatered = DateTime.UtcNow.AddDays(-5);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Wilting);
    }

    [Fact]
    public void CalculatePlantStatus_Dead_WhenTooLate()
    {
        // Arrange - Watered 7 days ago, needs water every 3 days
        var lastWatered = DateTime.UtcNow.AddDays(-7);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Dead);
    }

    [Fact]
    public void NeedsWateringSoon_WhenWithin6Hours_ShouldReturnTrue()
    {
        // Arrange - Watered 2.75 days ago, needs water every 3 days (5.75 hours left)
        var lastWatered = DateTime.UtcNow.AddHours(-66);
        int waterIntervalDays = 3;

        // Act
        var needsSoon = PlantGrowthCalculator.NeedsWateringSoon(lastWatered, waterIntervalDays);

        // Assert
        needsSoon.Should().BeTrue();
    }

    [Fact]
    public void NeedsWateringSoon_WhenFarFromThirsty_ShouldReturnFalse()
    {
        // Arrange - Watered 1 day ago, needs water every 3 days (48 hours left)
        var lastWatered = DateTime.UtcNow.AddDays(-1);
        int waterIntervalDays = 3;

        // Act
        var needsSoon = PlantGrowthCalculator.NeedsWateringSoon(lastWatered, waterIntervalDays);

        // Assert
        needsSoon.Should().BeFalse();
    }

    [Fact]
    public void GetDaysUntilWaterNeeded_ShouldReturnCorrectDays()
    {
        // Arrange - Watered 1 day ago, needs water every 3 days
        var lastWatered = DateTime.UtcNow.AddDays(-1);
        int waterIntervalDays = 3;

        // Act
        var daysUntil = PlantGrowthCalculator.GetDaysUntilWaterNeeded(lastWatered, waterIntervalDays);

        // Assert
        daysUntil.Should().BeApproximately(2.0, 0.1); // ~2 days left
    }

    [Fact]
    public void GetDaysUntilWaterNeeded_WhenOverdue_ShouldReturnZero()
    {
        // Arrange - Watered 5 days ago, needs water every 3 days
        var lastWatered = DateTime.UtcNow.AddDays(-5);
        int waterIntervalDays = 3;

        // Act
        var daysUntil = PlantGrowthCalculator.GetDaysUntilWaterNeeded(lastWatered, waterIntervalDays);

        // Assert
        daysUntil.Should().Be(0);
    }

    #endregion
}
