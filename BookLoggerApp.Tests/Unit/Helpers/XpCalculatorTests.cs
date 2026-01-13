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
    // Level 1: 100 XP required. So 0-99 XP = Level 1? No, usually level 1 starts at 0.
    // Let's re-read the logic. 
    // CalculateLevelFromXp loop: while (totalXp >= xpRequired(level)) { totalXp -= xpRequired; level++; }
    //
    // Scenario: 0 XP
    // Level = 1. xpRequired = GetXpForLevel(1) = 100.
    // 0 >= 100 is false. Returns Level 1.
    [InlineData(0, 1)] 
    
    // Scenario: 99 XP
    // 99 >= 100 is false. Returns Level 1.
    [InlineData(99, 1)]

    // Scenario: 100 XP
    // 100 >= 100 is true. totalXp becomes 0. Level becomes 2. xpRequired = GetXpForLevel(2) = 400.
    // 0 >= 400 is false. Returns Level 2.
    [InlineData(100, 2)]

    // Scenario: 499 XP (Level 2 requires 100 + 400 = 500 total cumulative XP to finish?)
    // Wait, the while loop subtracts XP. 
    // 499 XP:
    // 1. 499 >= 100 (L1). Rem: 399. Level: 2. Req: 400.
    // 2. 399 >= 400 (L2). False. Returns Level 2.
    [InlineData(499, 2)]

    // Scenario: 500 XP
    // 1. 500 >= 100 (L1). Rem: 400. Level: 2. Req: 400.
    // 2. 400 >= 400 (L2). Rem: 0. Level: 3. Req: 2500 (L3 = 100*3^2 = 900? No wait, GetXpForLevel(3) = 900).
    // 2. 400 >= 400 is True. Rem: 0. Level: 3. Req: 900.
    // 3. 0 >= 900. False. Returns Level 3.
    [InlineData(500, 3)]
    public void CalculateLevelFromXp_ReturnsCorrectLevel(int totalXp, int expectedLevel)
    {
        // Act
        var result = XpCalculator.CalculateLevelFromXp(totalXp);
        
        // Assert
        result.Should().Be(expectedLevel);
    }
}
