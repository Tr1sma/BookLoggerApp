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

    #region Reading Days Based Leveling Tests

    // ========================================
    // CalculateLevelFromReadingDays Tests
    // ========================================

    [Theory]
    [InlineData(0, 1.0, 10, 1)]   // 0 days = Level 1
    [InlineData(-1, 1.0, 10, 1)]  // Negative days = Level 1
    [InlineData(-100, 1.0, 10, 1)] // Very negative = Level 1
    public void CalculateLevelFromReadingDays_WithZeroOrNegativeDays_ShouldReturnLevelOne(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        // Assert
        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(1, 1.0, 10, 1)]   // 1 day = Level 1 (floor(1*1/3)+1 = 1)
    [InlineData(2, 1.0, 10, 1)]   // 2 days = Level 1 (floor(2*1/3)+1 = 1)
    [InlineData(3, 1.0, 10, 2)]   // 3 days = Level 2 (floor(3*1/3)+1 = 2)
    [InlineData(4, 1.0, 10, 2)]   // 4 days = Level 2
    [InlineData(5, 1.0, 10, 2)]   // 5 days = Level 2
    [InlineData(6, 1.0, 10, 3)]   // 6 days = Level 3
    [InlineData(9, 1.0, 10, 4)]   // 9 days = Level 4
    [InlineData(12, 1.0, 10, 5)]  // 12 days = Level 5
    [InlineData(27, 1.0, 10, 10)] // 27 days = Level 10
    public void CalculateLevelFromReadingDays_WithNormalGrowthRate_ShouldCalculateCorrectly(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        // Assert
        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(1, 1.2, 10, 1)]   // 1 day at 1.2x = Level 1 (floor(1.2/3)+1 = 1)
    [InlineData(2, 1.2, 10, 1)]   // 2 days at 1.2x = Level 1 (floor(2.4/3)+1 = 1)
    [InlineData(3, 1.2, 10, 2)]   // 3 days at 1.2x = Level 2 (floor(3.6/3)+1 = 2)
    [InlineData(5, 1.2, 10, 3)]   // 5 days at 1.2x = Level 3 (floor(6/3)+1 = 3)
    [InlineData(8, 1.2, 10, 4)]   // 8 days at 1.2x = Level 4 (floor(9.6/3)+1 = 4)
    [InlineData(10, 1.2, 10, 5)]  // 10 days at 1.2x = Level 5 (floor(12/3)+1 = 5)
    public void CalculateLevelFromReadingDays_WithFasterGrowthRate_ShouldLevelFaster(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        // Assert
        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(1, 0.8, 10, 1)]   // 1 day at 0.8x = Level 1
    [InlineData(3, 0.8, 10, 1)]   // 3 days at 0.8x = Level 1 (floor(2.4/3)+1 = 1)
    [InlineData(4, 0.8, 10, 2)]   // 4 days at 0.8x = Level 2 (floor(3.2/3)+1 = 2)
    [InlineData(7, 0.8, 10, 2)]   // 7 days at 0.8x = Level 2 (floor(5.6/3)+1 = 2)
    [InlineData(8, 0.8, 10, 3)]   // 8 days at 0.8x = Level 3 (floor(6.4/3)+1 = 3)
    [InlineData(15, 0.8, 10, 5)]  // 15 days at 0.8x = Level 5 (floor(12/3)+1 = 5)
    public void CalculateLevelFromReadingDays_WithSlowerGrowthRate_ShouldLevelSlower(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        // Assert
        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(100, 1.0, 5, 5)]   // 100 days with maxLevel 5 = Level 5
    [InlineData(1000, 1.0, 10, 10)] // 1000 days with maxLevel 10 = Level 10
    [InlineData(27, 1.0, 8, 8)]    // Exact calculation would be 10, capped at 8
    public void CalculateLevelFromReadingDays_ShouldNeverExceedMaxLevel(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        // Assert
        level.Should().Be(expectedLevel);
        level.Should().BeLessThanOrEqualTo(maxLevel);
    }

    [Fact]
    public void CalculateLevelFromReadingDays_WithMaxLevelOne_ShouldAlwaysReturnOne()
    {
        // Any number of days with maxLevel 1 should return 1
        for (int days = 0; days <= 100; days++)
        {
            var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(days, 1.0, 1);
            level.Should().Be(1);
        }
    }

    [Fact]
    public void CalculateLevelFromReadingDays_FasterGrowthRateShouldReachLevelSooner()
    {
        // At 3 days: GR 1.0 reaches level 2, GR 0.8 still at level 1
        var levelFast = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.2, 10);
        var levelNormal = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.0, 10);
        var levelSlow = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 0.8, 10);

        levelFast.Should().BeGreaterThanOrEqualTo(levelNormal);
        levelNormal.Should().BeGreaterThanOrEqualTo(levelSlow);
    }

    // ========================================
    // GetReadingDaysForLevel Tests
    // ========================================

    [Theory]
    [InlineData(1, 1.0, 0)]   // Level 1 needs 0 days
    [InlineData(0, 1.0, 0)]   // Level 0 (edge) needs 0 days
    [InlineData(-1, 1.0, 0)]  // Negative level needs 0 days
    public void GetReadingDaysForLevel_WithLevelOneOrLess_ShouldReturnZero(
        int level, double growthRate, int expectedDays)
    {
        // Act
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        // Assert
        days.Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(2, 1.0, 3)]   // Level 2 = ceil(1*3/1) = 3 days
    [InlineData(3, 1.0, 6)]   // Level 3 = ceil(2*3/1) = 6 days
    [InlineData(4, 1.0, 9)]   // Level 4 = ceil(3*3/1) = 9 days
    [InlineData(5, 1.0, 12)]  // Level 5 = ceil(4*3/1) = 12 days
    [InlineData(10, 1.0, 27)] // Level 10 = ceil(9*3/1) = 27 days
    public void GetReadingDaysForLevel_WithNormalGrowthRate_ShouldCalculateCorrectly(
        int level, double growthRate, int expectedDays)
    {
        // Act
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        // Assert
        days.Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(2, 1.2, 3)]   // Level 2 = ceil(1*3/1.2) = ceil(2.5) = 3 days
    [InlineData(3, 1.2, 5)]   // Level 3 = ceil(2*3/1.2) = ceil(5) = 5 days
    [InlineData(5, 1.2, 10)]  // Level 5 = ceil(4*3/1.2) = ceil(10) = 10 days
    [InlineData(10, 1.2, 23)] // Level 10 = ceil(9*3/1.2) = ceil(22.5) = 23 days
    public void GetReadingDaysForLevel_WithFasterGrowthRate_ShouldRequireFewerDays(
        int level, double growthRate, int expectedDays)
    {
        // Act
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        // Assert
        days.Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(2, 0.8, 4)]   // Level 2 = ceil(1*3/0.8) = ceil(3.75) = 4 days
    [InlineData(3, 0.8, 8)]   // Level 3 = ceil(2*3/0.8) = ceil(7.5) = 8 days
    [InlineData(5, 0.8, 15)]  // Level 5 = ceil(4*3/0.8) = ceil(15) = 15 days
    [InlineData(10, 0.8, 34)] // Level 10 = ceil(9*3/0.8) = ceil(33.75) = 34 days
    public void GetReadingDaysForLevel_WithSlowerGrowthRate_ShouldRequireMoreDays(
        int level, double growthRate, int expectedDays)
    {
        // Act
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        // Assert
        days.Should().Be(expectedDays);
    }

    [Fact]
    public void GetReadingDaysForLevel_ShouldBeConsistentWithLevelCalculation()
    {
        // If GetReadingDaysForLevel says level X needs N days,
        // then CalculateLevelFromReadingDays with N days should give at least level X
        double growthRate = 1.0;
        int maxLevel = 10;

        for (int level = 2; level <= maxLevel; level++)
        {
            int daysNeeded = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);
            int calculatedLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(daysNeeded, growthRate, maxLevel);

            calculatedLevel.Should().BeGreaterThanOrEqualTo(level,
                $"At {daysNeeded} days, should have at least level {level}");
        }
    }

    // ========================================
    // GetReadingDaysToNextLevel Tests
    // ========================================

    [Theory]
    [InlineData(1, 0, 1.0, 10, 3)]  // Level 1, 0 days: need 3 to reach level 2
    [InlineData(1, 1, 1.0, 10, 2)]  // Level 1, 1 day: need 2 more
    [InlineData(1, 2, 1.0, 10, 1)]  // Level 1, 2 days: need 1 more
    [InlineData(1, 3, 1.0, 10, 0)]  // Level 1, 3 days: already enough for level 2
    [InlineData(2, 3, 1.0, 10, 3)]  // Level 2, 3 days: need 3 more for level 3
    [InlineData(2, 4, 1.0, 10, 2)]  // Level 2, 4 days: need 2 more
    [InlineData(2, 5, 1.0, 10, 1)]  // Level 2, 5 days: need 1 more
    [InlineData(2, 6, 1.0, 10, 0)]  // Level 2, 6 days: already enough
    public void GetReadingDaysToNextLevel_ShouldCalculateRemainingDays(
        int currentLevel, int readingDays, double growthRate, int maxLevel, int expectedDaysNeeded)
    {
        // Act
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel, readingDays, growthRate, maxLevel);

        // Assert
        daysToNext.Should().Be(expectedDaysNeeded);
    }

    [Theory]
    [InlineData(10, 0, 1.0, 10)]   // At max level
    [InlineData(10, 100, 1.0, 10)] // At max level with many days
    [InlineData(5, 50, 1.0, 5)]    // At max level (5)
    public void GetReadingDaysToNextLevel_AtMaxLevel_ShouldReturnZero(
        int currentLevel, int readingDays, double growthRate, int maxLevel)
    {
        // Act
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel, readingDays, growthRate, maxLevel);

        // Assert
        daysToNext.Should().Be(0);
    }

    [Fact]
    public void GetReadingDaysToNextLevel_WithExcessDays_ShouldReturnZero()
    {
        // If player has more days than needed for next level
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel: 1,
            readingDays: 10, // Way more than needed for level 2
            growthRate: 1.0,
            maxLevel: 10);

        daysToNext.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 0, 1.2, 10, 3)]  // GR 1.2: Level 2 needs ceil(3/1.2)=3 days
    [InlineData(1, 0, 0.8, 10, 4)]  // GR 0.8: Level 2 needs ceil(3/0.8)=4 days
    public void GetReadingDaysToNextLevel_ShouldRespectGrowthRate(
        int currentLevel, int readingDays, double growthRate, int maxLevel, int expectedDaysNeeded)
    {
        // Act
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel, readingDays, growthRate, maxLevel);

        // Assert
        daysToNext.Should().Be(expectedDaysNeeded);
    }

    // ========================================
    // GetReadingDaysPercentage Tests
    // ========================================

    [Theory]
    [InlineData(1, 0, 1.0, 10, 0)]    // Level 1, 0 days = 0%
    [InlineData(1, 1, 1.0, 10, 33)]   // Level 1, 1 day = 33% (1/3)
    [InlineData(1, 2, 1.0, 10, 66)]   // Level 1, 2 days = 66% (2/3)
    [InlineData(2, 3, 1.0, 10, 0)]    // Level 2, 3 days = 0% (just started level 2)
    [InlineData(2, 4, 1.0, 10, 33)]   // Level 2, 4 days = 33%
    [InlineData(2, 5, 1.0, 10, 66)]   // Level 2, 5 days = 66%
    public void GetReadingDaysPercentage_ShouldCalculateProgressCorrectly(
        int currentLevel, int readingDays, double growthRate, int maxLevel, int expectedPercentage)
    {
        // Act
        var percentage = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel, readingDays, growthRate, maxLevel);

        // Assert
        percentage.Should().Be(expectedPercentage);
    }

    [Theory]
    [InlineData(10, 0, 1.0, 10)]   // At max level
    [InlineData(10, 100, 1.0, 10)] // At max level with many days
    [InlineData(5, 50, 1.0, 5)]    // At max level (5)
    public void GetReadingDaysPercentage_AtMaxLevel_ShouldReturn100(
        int currentLevel, int readingDays, double growthRate, int maxLevel)
    {
        // Act
        var percentage = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel, readingDays, growthRate, maxLevel);

        // Assert
        percentage.Should().Be(100);
    }

    [Fact]
    public void GetReadingDaysPercentage_ShouldClampBetweenZeroAndHundred()
    {
        // Test that result is always between 0 and 100
        var percentages = new List<int>();

        for (int days = 0; days <= 30; days++)
        {
            var pct = PlantGrowthCalculator.GetReadingDaysPercentage(1, days, 1.0, 10);
            percentages.Add(pct);
            pct.Should().BeInRange(0, 100);
        }
    }

    [Theory]
    [InlineData(1, 0, 1.2, 10)]  // Level 1, 0 days with GR 1.2
    [InlineData(1, 0, 0.8, 10)]  // Level 1, 0 days with GR 0.8
    [InlineData(2, 3, 1.2, 10)]  // Level 2, 3 days with GR 1.2
    public void GetReadingDaysPercentage_WithDifferentGrowthRates_ShouldCalculate(
        int currentLevel, int readingDays, double growthRate, int maxLevel)
    {
        // Act
        var percentage = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel, readingDays, growthRate, maxLevel);

        // Assert - Just verify it returns a valid percentage
        percentage.Should().BeInRange(0, 100);
    }

    // ========================================
    // User Requirement Verification Tests
    // ========================================

    [Fact]
    public void ReadingDays_UserRequirement_3DaysEqualsOneLevelAtGrowthRate1()
    {
        // User requirement: "3 Lese tage = 1 Level (bei GrowthRate 1.0)"
        // Starting at level 1, after 3 days should be level 2

        int startLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(0, 1.0, 10);
        int afterThreeDays = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.0, 10);

        startLevel.Should().Be(1);
        afterThreeDays.Should().Be(2);
        (afterThreeDays - startLevel).Should().Be(1, "Should gain exactly 1 level after 3 days");
    }

    [Fact]
    public void ReadingDays_UserRequirement_GrowthRate1Point2Is20PercentFaster()
    {
        // User requirement: "Growth rate 1.2 soll 20% schneller sein als 1"
        // At GR 1.0: 3 days per level
        // At GR 1.2: ~2.5 days per level (20% faster)

        // Days needed for level 2
        int daysGR10 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 1.0);
        int daysGR12 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 1.2);

        daysGR12.Should().BeLessThanOrEqualTo(daysGR10);

        // For level 10
        int daysLevel10GR10 = PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.0);
        int daysLevel10GR12 = PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.2);

        // 27 vs 23 days - roughly 15% fewer days (close to 20% faster)
        daysLevel10GR12.Should().Be(23);
        daysLevel10GR10.Should().Be(27);
        daysLevel10GR12.Should().BeLessThan(daysLevel10GR10);
    }

    [Fact]
    public void ReadingDays_UserRequirement_GrowthRate0Point8Is20PercentSlower()
    {
        // User requirement: "0.8 logischweise 20% langsamer als 1"
        // At GR 1.0: 3 days per level
        // At GR 0.8: ~3.75 days per level (20% slower)

        int daysGR10 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 1.0);
        int daysGR08 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 0.8);

        daysGR08.Should().BeGreaterThan(daysGR10);
        daysGR08.Should().Be(4); // ceil(3/0.8) = ceil(3.75) = 4

        // For level 10
        int daysLevel10GR10 = PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.0);
        int daysLevel10GR08 = PlantGrowthCalculator.GetReadingDaysForLevel(10, 0.8);

        // 27 vs 34 days - roughly 26% more days (close to 25% = 1/0.8)
        daysLevel10GR08.Should().Be(34);
        daysLevel10GR10.Should().Be(27);
        daysLevel10GR08.Should().BeGreaterThan(daysLevel10GR10);
    }

    [Fact]
    public void ReadingDays_ConsistencyBetweenCalculationsAndInverses()
    {
        // Verify that the formulas are mathematically consistent
        double[] growthRates = [0.5, 0.8, 1.0, 1.2, 1.5, 2.0];
        int maxLevel = 10;

        foreach (var growthRate in growthRates)
        {
            for (int level = 1; level <= maxLevel; level++)
            {
                int daysForLevel = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);
                int calculatedLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(daysForLevel, growthRate, maxLevel);

                // When we have exactly the days for a level, we should be at or above that level
                calculatedLevel.Should().BeGreaterThanOrEqualTo(level,
                    $"At GR {growthRate}, {daysForLevel} days should give at least level {level}");
            }
        }
    }

    #endregion
}
