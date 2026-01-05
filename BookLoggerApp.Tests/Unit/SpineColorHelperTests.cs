using FluentAssertions;
using BookLoggerApp.Core.Helpers;
using Xunit;

namespace BookLoggerApp.Tests.Unit;

public class SpineColorHelperTests
{
    [Theory]
    [InlineData("red", "8B0000", "CD5C5C")] // Known preset
    [InlineData("BLUE", "00008B", "4169E1")] // Case insensitive
    [InlineData("chocolate", "D2691E", "F4A460")] // New preset
    public void GetColors_WithPreset_ReturnsCorrectColors(string colorName, string expectedDark, string expectedLight)
    {
        var (dark, light) = SpineColorHelper.GetColors(colorName, Guid.NewGuid());
        
        dark.Should().Be(expectedDark);
        light.Should().Be(expectedLight);
    }

    [Theory]
    [InlineData("#123456")]
    [InlineData("#ABCDEF")]
    [InlineData("#000000")]
    public void GetColors_WithHexCode_ReturnsGeneratedGradient(string hexCode)
    {
        var (dark, light) = SpineColorHelper.GetColors(hexCode, Guid.NewGuid());
        
        // Dark should be the base hex without hash
        dark.Should().Be(hexCode.TrimStart('#'));
        
        // Light should be different (lighter)
        light.Should().NotBe(dark);
        light.Length.Should().Be(6);
    }

    [Fact]
    public void GetColors_WithInvalidHex_ReturnsFallback()
    {
        // Invalid hex code shouldn't crash, should fallback (either to hash or safe default)
        // Since our implementation falls back to hash if preset/hex logic fails
        var bookId = Guid.NewGuid();
        
        var (dark, light) = SpineColorHelper.GetColors("not-a-color", bookId);
        
        dark.Should().NotBeNullOrEmpty();
        light.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetColors_WithNull_ReturnsFallback()
    {
        var bookId = Guid.NewGuid();
        var (dark, light) = SpineColorHelper.GetColors(null, bookId);
        
        dark.Should().NotBeNullOrEmpty();
        light.Should().NotBeNullOrEmpty();
    }
}
