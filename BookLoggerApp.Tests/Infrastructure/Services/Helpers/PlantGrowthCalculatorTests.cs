using FluentAssertions;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure.Services.Helpers;

public class PlantGrowthCalculatorTests
{
    #region XP Calculation Tests

    [Theory]
    [InlineData(1, 0)]      // L1: 0 XP
    [InlineData(2, 150)]    // L2: 100 * 1.5^1
    [InlineData(3, 225)]    // L3: 100 * 1.5^2
    [InlineData(4, 337)]    // L4: 100 * 1.5^3
    [InlineData(5, 506)]    // L5: 100 * 1.5^4
    public void GetXpForLevel_ShouldReturnCorrectXp(int level, int expectedXp)
    {
        var xp = PlantGrowthCalculator.GetXpForLevel(level);

        xp.Should().BeCloseTo(expectedXp, 2);
    }

    [Fact]
    public void GetXpForLevel_WithFasterGrowthRate_ShouldRequireLessXp()
    {
        int level = 5;
        double fasterGrowthRate = 1.5;

        int normalXp = PlantGrowthCalculator.GetXpForLevel(level);
        int fasterXp = PlantGrowthCalculator.GetXpForLevel(level, fasterGrowthRate);

        fasterXp.Should().BeLessThan(normalXp);
        fasterXp.Should().BeCloseTo((int)(normalXp / fasterGrowthRate), 2);
    }

    [Fact]
    public void GetTotalXpForLevel_ShouldReturnCumulativeXp()
    {
        // L3 = 150 (L2) + 225 (L3) = 375
        var totalXp = PlantGrowthCalculator.GetTotalXpForLevel(3);

        totalXp.Should().BeCloseTo(375, 2);
    }

    [Theory]
    [InlineData(0, 1)]      // 0 XP = Level 1
    [InlineData(150, 2)]    // total for L2 = 150
    [InlineData(375, 3)]    // total for L3 = 375
    [InlineData(712, 4)]    // total for L4 = 712
    public void CalculateLevelFromXp_ShouldReturnCorrectLevel(int totalXp, int expectedLevel)
    {
        var level = PlantGrowthCalculator.CalculateLevelFromXp(totalXp);

        level.Should().Be(expectedLevel);
    }

    [Fact]
    public void CalculateLevelFromXp_WithMaxLevel_ShouldNotExceedMax()
    {
        var level = PlantGrowthCalculator.CalculateLevelFromXp(1000000, 1.0, 10);

        level.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void GetXpToNextLevel_ShouldReturnRemainingXpNeeded()
    {
        // L2 total=150, L3 total=375; 200 XP → 50 into L2 → needs 225-50=175 more
        var xpToNext = PlantGrowthCalculator.GetXpToNextLevel(2, 200);

        xpToNext.Should().BeCloseTo(175, 2);
    }

    [Fact]
    public void GetXpPercentage_ShouldReturnProgressPercentage()
    {
        // L2 total=150, L3 total=375; 200 XP → 50/225 ≈ 22%
        var percentage = PlantGrowthCalculator.GetXpPercentage(2, 200);

        percentage.Should().BeCloseTo(22, 2);
    }

    [Fact]
    public void CanLevelUp_WhenEnoughXp_ShouldReturnTrue()
    {
        // L2 with 375 XP = enough for L3 (150+225)
        var canLevel = PlantGrowthCalculator.CanLevelUp(2, 375, 1.0, 10);

        canLevel.Should().BeTrue();
    }

    [Fact]
    public void CanLevelUp_WhenNotEnoughXp_ShouldReturnFalse()
    {
        // L2 with 300 XP < 375 needed for L3
        var canLevel = PlantGrowthCalculator.CanLevelUp(2, 300, 1.0, 10);

        canLevel.Should().BeFalse();
    }

    [Fact]
    public void CanLevelUp_WhenAtMaxLevel_ShouldReturnFalse()
    {
        var canLevel = PlantGrowthCalculator.CanLevelUp(10, 1000000, 1.0, 10);

        canLevel.Should().BeFalse();
    }

    #endregion

    #region Plant Status Tests

    [Fact]
    public void CalculatePlantStatus_Healthy_WhenRecentlyWatered()
    {
        var status = PlantGrowthCalculator.CalculatePlantStatus(DateTime.UtcNow.AddDays(-1), 3);

        status.Should().Be(PlantStatus.Healthy);
    }

    [Fact]
    public void CalculatePlantStatus_Thirsty_WhenOverdue()
    {
        var status = PlantGrowthCalculator.CalculatePlantStatus(DateTime.UtcNow.AddDays(-3.5), 3);

        status.Should().Be(PlantStatus.Thirsty);
    }

    [Fact]
    public void CalculatePlantStatus_Wilting_WhenLongOverdue()
    {
        var status = PlantGrowthCalculator.CalculatePlantStatus(DateTime.UtcNow.AddDays(-5), 3);

        status.Should().Be(PlantStatus.Wilting);
    }

    [Fact]
    public void CalculatePlantStatus_Dead_WhenTooLate()
    {
        var status = PlantGrowthCalculator.CalculatePlantStatus(DateTime.UtcNow.AddDays(-7), 3);

        status.Should().Be(PlantStatus.Dead);
    }

    [Fact]
    public void NeedsWateringSoon_WhenWithin6Hours_ShouldReturnTrue()
    {
        // 66h elapsed of 72h interval → 5.75h left
        var needsSoon = PlantGrowthCalculator.NeedsWateringSoon(DateTime.UtcNow.AddHours(-66), 3);

        needsSoon.Should().BeTrue();
    }

    [Fact]
    public void NeedsWateringSoon_WhenFarFromThirsty_ShouldReturnFalse()
    {
        // 1 day elapsed of 3-day interval → 48h left
        var needsSoon = PlantGrowthCalculator.NeedsWateringSoon(DateTime.UtcNow.AddDays(-1), 3);

        needsSoon.Should().BeFalse();
    }

    [Fact]
    public void GetDaysUntilWaterNeeded_ShouldReturnCorrectDays()
    {
        var daysUntil = PlantGrowthCalculator.GetDaysUntilWaterNeeded(DateTime.UtcNow.AddDays(-1), 3);

        daysUntil.Should().BeApproximately(2.0, 0.1);
    }

    [Fact]
    public void GetDaysUntilWaterNeeded_WhenOverdue_ShouldReturnZero()
    {
        var daysUntil = PlantGrowthCalculator.GetDaysUntilWaterNeeded(DateTime.UtcNow.AddDays(-5), 3);

        daysUntil.Should().Be(0);
    }

    [Fact]
    public void GetNextWaterDueAt_ShouldReturnLastWateredPlusInterval()
    {
        var lastWatered = new DateTime(2026, 4, 1, 8, 30, 0, DateTimeKind.Utc);

        var nextWaterDue = PlantGrowthCalculator.GetNextWaterDueAt(lastWatered, 4);

        nextWaterDue.Should().Be(new DateTime(2026, 4, 5, 8, 30, 0, DateTimeKind.Utc));
    }

    #endregion

    #region Reading Days Based Leveling Tests

    [Theory]
    [InlineData(0, 1.0, 10, 1)]
    [InlineData(-1, 1.0, 10, 1)]
    [InlineData(-100, 1.0, 10, 1)]
    public void CalculateLevelFromReadingDays_WithZeroOrNegativeDays_ShouldReturnLevelOne(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(1, 1.0, 10, 1)]   // floor(1/3)+1 = 1
    [InlineData(2, 1.0, 10, 1)]   // floor(2/3)+1 = 1
    [InlineData(3, 1.0, 10, 2)]   // floor(3/3)+1 = 2
    [InlineData(4, 1.0, 10, 2)]
    [InlineData(5, 1.0, 10, 2)]
    [InlineData(6, 1.0, 10, 3)]
    [InlineData(9, 1.0, 10, 4)]
    [InlineData(12, 1.0, 10, 5)]
    [InlineData(27, 1.0, 10, 10)]
    public void CalculateLevelFromReadingDays_WithNormalGrowthRate_ShouldCalculateCorrectly(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(1, 1.2, 10, 1)]   // floor(1.2/3)+1 = 1
    [InlineData(2, 1.2, 10, 1)]   // floor(2.4/3)+1 = 1
    [InlineData(3, 1.2, 10, 2)]   // floor(3.6/3)+1 = 2
    [InlineData(5, 1.2, 10, 3)]   // floor(6/3)+1 = 3
    [InlineData(8, 1.2, 10, 4)]   // floor(9.6/3)+1 = 4
    [InlineData(10, 1.2, 10, 5)]  // floor(12/3)+1 = 5
    public void CalculateLevelFromReadingDays_WithFasterGrowthRate_ShouldLevelFaster(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(1, 0.8, 10, 1)]
    [InlineData(3, 0.8, 10, 1)]   // floor(2.4/3)+1 = 1
    [InlineData(4, 0.8, 10, 2)]   // floor(3.2/3)+1 = 2
    [InlineData(7, 0.8, 10, 2)]   // floor(5.6/3)+1 = 2
    [InlineData(8, 0.8, 10, 3)]   // floor(6.4/3)+1 = 3
    [InlineData(15, 0.8, 10, 5)]  // floor(12/3)+1 = 5
    public void CalculateLevelFromReadingDays_WithSlowerGrowthRate_ShouldLevelSlower(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData(100, 1.0, 5, 5)]
    [InlineData(1000, 1.0, 10, 10)]
    [InlineData(27, 1.0, 8, 8)]   // would be 10, capped at 8
    public void CalculateLevelFromReadingDays_ShouldNeverExceedMaxLevel(
        int readingDays, double growthRate, int maxLevel, int expectedLevel)
    {
        var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(readingDays, growthRate, maxLevel);

        level.Should().Be(expectedLevel);
        level.Should().BeLessThanOrEqualTo(maxLevel);
    }

    [Fact]
    public void CalculateLevelFromReadingDays_WithMaxLevelOne_ShouldAlwaysReturnOne()
    {
        for (int days = 0; days <= 100; days++)
        {
            var level = PlantGrowthCalculator.CalculateLevelFromReadingDays(days, 1.0, 1);
            level.Should().Be(1);
        }
    }

    [Fact]
    public void CalculateLevelFromReadingDays_FasterGrowthRateShouldReachLevelSooner()
    {
        var levelFast = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.2, 10);
        var levelNormal = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.0, 10);
        var levelSlow = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 0.8, 10);

        levelFast.Should().BeGreaterThanOrEqualTo(levelNormal);
        levelNormal.Should().BeGreaterThanOrEqualTo(levelSlow);
    }

    [Theory]
    [InlineData(1, 1.0, 0)]
    [InlineData(0, 1.0, 0)]
    [InlineData(-1, 1.0, 0)]
    public void GetReadingDaysForLevel_WithLevelOneOrLess_ShouldReturnZero(
        int level, double growthRate, int expectedDays)
    {
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        days.Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(2, 1.0, 3)]    // ceil(1*3/1) = 3
    [InlineData(3, 1.0, 6)]    // ceil(2*3/1) = 6
    [InlineData(4, 1.0, 9)]
    [InlineData(5, 1.0, 12)]
    [InlineData(10, 1.0, 27)]  // ceil(9*3/1) = 27
    public void GetReadingDaysForLevel_WithNormalGrowthRate_ShouldCalculateCorrectly(
        int level, double growthRate, int expectedDays)
    {
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        days.Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(2, 1.2, 3)]    // ceil(1*3/1.2) = ceil(2.5) = 3
    [InlineData(3, 1.2, 5)]    // ceil(2*3/1.2) = 5
    [InlineData(5, 1.2, 10)]
    [InlineData(10, 1.2, 23)]  // ceil(9*3/1.2) = ceil(22.5) = 23
    public void GetReadingDaysForLevel_WithFasterGrowthRate_ShouldRequireFewerDays(
        int level, double growthRate, int expectedDays)
    {
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        days.Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(2, 0.8, 4)]    // ceil(1*3/0.8) = ceil(3.75) = 4
    [InlineData(3, 0.8, 8)]    // ceil(2*3/0.8) = ceil(7.5) = 8
    [InlineData(5, 0.8, 15)]
    [InlineData(10, 0.8, 34)]  // ceil(9*3/0.8) = ceil(33.75) = 34
    public void GetReadingDaysForLevel_WithSlowerGrowthRate_ShouldRequireMoreDays(
        int level, double growthRate, int expectedDays)
    {
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);

        days.Should().Be(expectedDays);
    }

    [Fact]
    public void GetReadingDaysForLevel_ShouldBeConsistentWithLevelCalculation()
    {
        // Forward and inverse must agree: days for level X → CalculateLevelFromReadingDays ≥ X
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

    [Theory]
    [InlineData(1, 0, 1.0, 10, 3)]   // need 3 to reach L2
    [InlineData(1, 1, 1.0, 10, 2)]
    [InlineData(1, 2, 1.0, 10, 1)]
    [InlineData(1, 3, 1.0, 10, 0)]   // already enough
    [InlineData(2, 3, 1.0, 10, 3)]
    [InlineData(2, 4, 1.0, 10, 2)]
    [InlineData(2, 5, 1.0, 10, 1)]
    [InlineData(2, 6, 1.0, 10, 0)]
    public void GetReadingDaysToNextLevel_ShouldCalculateRemainingDays(
        int currentLevel, int readingDays, double growthRate, int maxLevel, int expectedDaysNeeded)
    {
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel, readingDays, growthRate, maxLevel);

        daysToNext.Should().Be(expectedDaysNeeded);
    }

    [Theory]
    [InlineData(10, 0, 1.0, 10)]
    [InlineData(10, 100, 1.0, 10)]
    [InlineData(5, 50, 1.0, 5)]
    public void GetReadingDaysToNextLevel_AtMaxLevel_ShouldReturnZero(
        int currentLevel, int readingDays, double growthRate, int maxLevel)
    {
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel, readingDays, growthRate, maxLevel);

        daysToNext.Should().Be(0);
    }

    [Fact]
    public void GetReadingDaysToNextLevel_WithExcessDays_ShouldReturnZero()
    {
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel: 1,
            readingDays: 10,
            growthRate: 1.0,
            maxLevel: 10);

        daysToNext.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 0, 1.2, 10, 3)]  // L2 needs ceil(3/1.2) = 3
    [InlineData(1, 0, 0.8, 10, 4)]  // L2 needs ceil(3/0.8) = 4
    public void GetReadingDaysToNextLevel_ShouldRespectGrowthRate(
        int currentLevel, int readingDays, double growthRate, int maxLevel, int expectedDaysNeeded)
    {
        var daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel, readingDays, growthRate, maxLevel);

        daysToNext.Should().Be(expectedDaysNeeded);
    }

    [Theory]
    [InlineData(1, 0, 1.0, 10, 0)]    // 0%
    [InlineData(1, 1, 1.0, 10, 33)]   // 1/3 = 33%
    [InlineData(1, 2, 1.0, 10, 66)]   // 2/3 = 66%
    [InlineData(2, 3, 1.0, 10, 0)]    // just started L2
    [InlineData(2, 4, 1.0, 10, 33)]
    [InlineData(2, 5, 1.0, 10, 66)]
    public void GetReadingDaysPercentage_ShouldCalculateProgressCorrectly(
        int currentLevel, int readingDays, double growthRate, int maxLevel, int expectedPercentage)
    {
        var percentage = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel, readingDays, growthRate, maxLevel);

        percentage.Should().Be(expectedPercentage);
    }

    [Theory]
    [InlineData(10, 0, 1.0, 10)]
    [InlineData(10, 100, 1.0, 10)]
    [InlineData(5, 50, 1.0, 5)]
    public void GetReadingDaysPercentage_AtMaxLevel_ShouldReturn100(
        int currentLevel, int readingDays, double growthRate, int maxLevel)
    {
        var percentage = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel, readingDays, growthRate, maxLevel);

        percentage.Should().Be(100);
    }

    [Fact]
    public void GetReadingDaysPercentage_ShouldClampBetweenZeroAndHundred()
    {
        for (int days = 0; days <= 30; days++)
        {
            var pct = PlantGrowthCalculator.GetReadingDaysPercentage(1, days, 1.0, 10);
            pct.Should().BeInRange(0, 100);
        }
    }

    [Theory]
    [InlineData(1, 0, 1.2, 10)]
    [InlineData(1, 0, 0.8, 10)]
    [InlineData(2, 3, 1.2, 10)]
    public void GetReadingDaysPercentage_WithDifferentGrowthRates_ShouldCalculate(
        int currentLevel, int readingDays, double growthRate, int maxLevel)
    {
        var percentage = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel, readingDays, growthRate, maxLevel);

        percentage.Should().BeInRange(0, 100);
    }

    [Fact]
    public void ReadingDays_UserRequirement_3DaysEqualsOneLevelAtGrowthRate1()
    {
        // "3 Lese tage = 1 Level (bei GrowthRate 1.0)"
        int startLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(0, 1.0, 10);
        int afterThreeDays = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.0, 10);

        startLevel.Should().Be(1);
        afterThreeDays.Should().Be(2);
        (afterThreeDays - startLevel).Should().Be(1, "Should gain exactly 1 level after 3 days");
    }

    [Fact]
    public void ReadingDays_UserRequirement_GrowthRate1Point2Is20PercentFaster()
    {
        // GR 1.2 = 20% faster → fewer days per level
        int daysGR10 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 1.0);
        int daysGR12 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 1.2);
        daysGR12.Should().BeLessThanOrEqualTo(daysGR10);

        // L10: 27 vs 23 days
        PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.2).Should().Be(23);
        PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.0).Should().Be(27);
    }

    [Fact]
    public void ReadingDays_UserRequirement_GrowthRate0Point8Is20PercentSlower()
    {
        // GR 0.8 = 20% slower → more days per level
        int daysGR10 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 1.0);
        int daysGR08 = PlantGrowthCalculator.GetReadingDaysForLevel(2, 0.8);
        daysGR08.Should().BeGreaterThan(daysGR10);
        daysGR08.Should().Be(4); // ceil(3/0.8) = 4

        // L10: 27 vs 34 days
        PlantGrowthCalculator.GetReadingDaysForLevel(10, 0.8).Should().Be(34);
        PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.0).Should().Be(27);
    }

    [Fact]
    public void ReadingDays_ConsistencyBetweenCalculationsAndInverses()
    {
        double[] growthRates = [0.5, 0.8, 1.0, 1.2, 1.5, 2.0];
        int maxLevel = 10;

        foreach (var growthRate in growthRates)
        {
            for (int level = 1; level <= maxLevel; level++)
            {
                int daysForLevel = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate);
                int calculatedLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(daysForLevel, growthRate, maxLevel);

                calculatedLevel.Should().BeGreaterThanOrEqualTo(level,
                    $"At GR {growthRate}, {daysForLevel} days should give at least level {level}");
            }
        }
    }

    #endregion

    #region Global Growth Multiplier (Herz der Geschichten) Tests

    // Regression guard for V9 code-review: inverse helpers used to ignore the global
    // multiplier while CalculateLevelFromReadingDays honoured it. UI would then show
    // ~2x the actual days to next level while Herz der Geschichten was active.

    [Theory]
    [InlineData(2, 1.0, 2.0, 2)]    // ceil(3/2) = 2 (was 3)
    [InlineData(5, 1.0, 2.0, 6)]    // ceil(12/2) = 6 (was 12)
    [InlineData(10, 1.0, 2.0, 14)]  // ceil(27/2) = 14 (was 27)
    public void GetReadingDaysForLevel_WithGlobalGrowthMultiplier_HalvesDays(
        int level, double growthRate, double multiplier, int expectedDays)
    {
        var days = PlantGrowthCalculator.GetReadingDaysForLevel(level, growthRate, multiplier);

        days.Should().Be(expectedDays);
    }

    [Fact]
    public void GetReadingDaysForLevel_DefaultMultiplier_UnchangedBehavior()
    {
        // Default multiplier (1.0) must not change existing behaviour.
        PlantGrowthCalculator.GetReadingDaysForLevel(5, 1.0).Should().Be(12);
        PlantGrowthCalculator.GetReadingDaysForLevel(10, 1.2).Should().Be(23);
    }

    [Fact]
    public void GetReadingDaysToNextLevel_WithMultiplier2_MatchesForwardFormula()
    {
        // Forward: 6 days × 2.0 / 3 = 4 → Level 5
        int forwardLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(6, 1.0, 100, 2.0);
        forwardLevel.Should().Be(5);

        // Inverse: GetReadingDaysForLevel(6, 1.0, 2.0) = ceil(5*3/2) = 8 → 8-6 = 2 remaining
        int daysToNext = PlantGrowthCalculator.GetReadingDaysToNextLevel(
            currentLevel: 5,
            readingDays: 6,
            growthRate: 1.0,
            maxLevel: 100,
            globalGrowthMultiplier: 2.0);

        daysToNext.Should().Be(2);
    }

    [Fact]
    public void GetReadingDaysPercentage_WithMultiplier_ConsistentWithLevel()
    {
        // Multiplier=2.0: floor(3*2/3)+1 = 3 at 3 days
        int level = PlantGrowthCalculator.CalculateLevelFromReadingDays(3, 1.0, 100, 2.0);
        level.Should().Be(3);

        // GetReadingDaysForLevel(3)=3, GetReadingDaysForLevel(4)=5 → daysIntoLevel=0 → 0%
        int pct = PlantGrowthCalculator.GetReadingDaysPercentage(
            currentLevel: 3,
            readingDays: 3,
            growthRate: 1.0,
            maxLevel: 100,
            globalGrowthMultiplier: 2.0);

        pct.Should().Be(0);
    }

    [Fact]
    public void ReadingDays_FullCycleConsistencyWithMultiplier()
    {
        // Guards against future drift like the V9 bug: forward × inverse must agree.
        double[] multipliers = [1.0, 1.5, 2.0];
        double[] growthRates = [0.8, 1.0, 1.2];
        int maxLevel = 10;

        foreach (var mult in multipliers)
        {
            foreach (var rate in growthRates)
            {
                for (int level = 2; level <= maxLevel; level++)
                {
                    int daysForLevel = PlantGrowthCalculator.GetReadingDaysForLevel(level, rate, mult);
                    int calculatedLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(
                        daysForLevel, rate, maxLevel, mult);

                    calculatedLevel.Should().BeGreaterThanOrEqualTo(level,
                        $"mult={mult}, rate={rate}: {daysForLevel} days should give at least level {level}");
                }
            }
        }
    }

    #endregion
}
