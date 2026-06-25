using BookLoggerApp.Core.Services.Analytics;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Analytics;

public class AnalyticsParamBuilderTests
{
    [Fact]
    public void Build_returns_added_params()
    {
        var result = AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.Source, "manual")
            .Add(AnalyticsParamNames.HasCover, true)
            .Add(AnalyticsParamNames.PagesBucket, "51-200")
            .Build();

        result.Should().HaveCount(3);
        result[AnalyticsParamNames.Source].Should().Be("manual");
        result[AnalyticsParamNames.HasCover].Should().Be(true);
    }

#if DEBUG
    [Theory]
    [InlineData("title")]
    [InlineData("book_title")]
    [InlineData("author")]
    [InlineData("isbn")]
    [InlineData("quote_text")]
    [InlineData("annotation")]
    [InlineData("email")]
    [InlineData("user_name")]
    public void Add_throws_on_forbidden_key(string forbidden)
    {
        var builder = AnalyticsParamBuilder.Create();
        var act = () => builder.Add(forbidden, "some_value");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Add_throws_on_long_string_value()
    {
        var builder = AnalyticsParamBuilder.Create();
        // Allowlisted key so the length tripwire trips (> 100 = Firebase cap), not the allowlist gate.
        var longValue = new string('x', 101);
        var act = () => builder.Add(AnalyticsParamNames.Source, longValue);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Add_throws_on_empty_key()
    {
        var builder = AnalyticsParamBuilder.Create();
        var act = () => builder.Add("", "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_throws_on_unknown_key()
    {
        // A key with no const in AnalyticsParamNames is rejected by the allowlist.
        var builder = AnalyticsParamBuilder.Create();
        var act = () => builder.Add("definitely_not_allowlisted", "v");
        act.Should().Throw<InvalidOperationException>();
    }
#endif

    [Fact]
    public void Add_accepts_short_string_value()
    {
        var result = AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.Source, "short")
            .Build();
        result.Should().ContainKey(AnalyticsParamNames.Source);
    }

    [Fact]
    public void Add_drops_unknown_key_in_release()
    {
        // In RELEASE the allowlist silently drops a non-declared key; in DEBUG it throws, so only
        // assert the drop when DEBUG is not defined.
        var result = AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.Source, "ok")
            .Build();
#if !DEBUG
        var withUnknown = AnalyticsParamBuilder.Create()
            .Add("not_a_real_key", "x")
            .Build();
        withUnknown.Should().NotContainKey("not_a_real_key");
#endif
        result.Should().ContainKey(AnalyticsParamNames.Source);
    }

    [Fact]
    public void BuildMutable_returns_mutable_copy()
    {
        var builder = AnalyticsParamBuilder.Create().Add(AnalyticsParamNames.CategoryCount, 1);
        var dict = builder.BuildMutable();
        dict["added_later"] = "v";
        dict.Should().HaveCount(2);
    }
}
