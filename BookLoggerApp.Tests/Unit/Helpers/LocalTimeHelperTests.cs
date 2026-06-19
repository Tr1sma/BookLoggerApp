using System;
using BookLoggerApp.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class LocalTimeHelperTests
{
    // A fixed, machine-independent zone so these assertions never depend on the CI/dev offset.
    private static readonly TimeZoneInfo PlusFive =
        TimeZoneInfo.CreateCustomTimeZone("test+5", TimeSpan.FromHours(5), "test+5", "test+5");

    private static readonly TimeZoneInfo MinusSix =
        TimeZoneInfo.CreateCustomTimeZone("test-6", TimeSpan.FromHours(-6), "test-6", "test-6");

    [Fact]
    public void ToLocal_AppliesTimeZoneOffset()
    {
        var utc = new DateTime(2025, 1, 1, 23, 0, 0, DateTimeKind.Utc);

        var local = LocalTimeHelper.ToLocal(utc, PlusFive);

        local.Should().Be(new DateTime(2025, 1, 2, 4, 0, 0));
    }

    [Fact]
    public void ToLocal_TreatsUnspecifiedKindAsUtc()
    {
        // SQLite reads DateTime back as Kind=Unspecified; the helper must still treat it as UTC.
        var unspecified = new DateTime(2025, 1, 1, 23, 0, 0, DateTimeKind.Unspecified);

        var local = LocalTimeHelper.ToLocal(unspecified, PlusFive);

        local.Should().Be(new DateTime(2025, 1, 2, 4, 0, 0));
    }

    [Fact]
    public void LocalDate_CrossesMidnightForward_ForPositiveOffset()
    {
        var utc = new DateTime(2025, 3, 15, 22, 30, 0, DateTimeKind.Utc);

        LocalTimeHelper.LocalDate(utc, PlusFive).Should().Be(new DateTime(2025, 3, 16));
    }

    [Fact]
    public void LocalDate_CrossesMidnightBackward_ForNegativeOffset()
    {
        var utc = new DateTime(2025, 3, 15, 3, 0, 0, DateTimeKind.Utc);

        LocalTimeHelper.LocalDate(utc, MinusSix).Should().Be(new DateTime(2025, 3, 14));
    }

    [Fact]
    public void ToLocal_NullTimeZone_Throws()
    {
        var act = () => LocalTimeHelper.ToLocal(DateTime.UtcNow, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
