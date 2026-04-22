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
        var longValue = new string('x', 100);
        var act = () => builder.Add("custom_key", longValue);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Add_throws_on_empty_key()
    {
        var builder = AnalyticsParamBuilder.Create();
        var act = () => builder.Add("", "value");
        act.Should().Throw<ArgumentException>();
    }
#endif

    [Fact]
    public void Add_accepts_short_string_value()
    {
        var result = AnalyticsParamBuilder.Create()
            .Add("custom_key", "short")
            .Build();
        result.Should().ContainKey("custom_key");
    }

    [Fact]
    public void BuildMutable_returns_mutable_copy()
    {
        var builder = AnalyticsParamBuilder.Create().Add("k", 1);
        var dict = builder.BuildMutable();
        dict["added_later"] = "v";
        dict.Should().HaveCount(2);
    }
}
