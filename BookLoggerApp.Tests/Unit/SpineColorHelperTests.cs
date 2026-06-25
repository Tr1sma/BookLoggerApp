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
        
        dark.Should().Be(hexCode.TrimStart('#'));

        // Light is a generated lighter shade, so it differs from dark.
        light.Should().NotBe(dark);
        light.Length.Should().Be(6);
    }

    [Fact]
    public void GetColors_WithInvalidHex_ReturnsFallback()
    {
        // Invalid hex must not crash; falls back to a hash-derived color.
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

    [Fact]
    public void GetColors_With3DigitHex_ExpandsToSixDigits()
    {
        var (dark, _) = SpineColorHelper.GetColors("#abc", Guid.NewGuid());

        dark.Should().Be("aabbcc");
    }

    [Fact]
    public void GetColors_With8DigitHex_StripsAlphaChannel()
    {
        var (dark, _) = SpineColorHelper.GetColors("#123456FF", Guid.NewGuid());

        dark.Should().Be("123456");
    }

    [Theory]
    [InlineData("#12345")]   // 5 digits — invalid length
    [InlineData("#GGGGGG")]  // non-hex characters
    [InlineData("#12")]      // too short
    public void GetColors_WithMalformedHex_FallsBackToHashColor(string malformed)
    {
        var bookId = Guid.NewGuid();
        var (dark, light) = SpineColorHelper.GetColors(malformed, bookId);

        // Falls back to a preset palette entry (6-digit, never the broken input).
        dark.Should().NotBe(malformed.TrimStart('#'));
        dark.Length.Should().Be(6);
        light.Length.Should().Be(6);
    }
}
