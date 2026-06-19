using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Security;

public class CsvInjectionTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "'=HYPERLINK(\"http://evil\")")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tTabbed", "'\tTabbed")]
    [InlineData("\rCarriage", "'\rCarriage")]
    public void SanitizeCsvField_prefixes_formula_triggers_with_apostrophe(string input, string expected)
    {
        ImportExportService.SanitizeCsvField(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Normal Title")]
    [InlineData("A book about C# = great")] // '=' not at the start
    [InlineData("")]
    public void SanitizeCsvField_leaves_safe_values_unchanged(string input)
    {
        ImportExportService.SanitizeCsvField(input).Should().Be(input);
    }

    [Fact]
    public void SanitizeCsvField_passes_null_through()
    {
        ImportExportService.SanitizeCsvField(null).Should().BeNull();
    }
}
