using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class ReadingForecastCalculatorTests
{
    private static readonly DateTime Now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static Book MakeBook(int? pageCount, int currentPage, DateTime? started)
        => new()
        {
            Title = "Test",
            Author = "Author",
            PageCount = pageCount,
            CurrentPage = currentPage,
            DateStarted = started,
            Status = ReadingStatus.Reading
        };

    private static ReadingSession Session(DateTime startedAt, int minutes, int? pagesRead)
        => new() { StartedAt = startedAt, Minutes = minutes, PagesRead = pagesRead };

    [Fact]
    public void HappyPath_ReturnsForecast_WithPositiveRatesAndFutureDate()
    {
        Book book = MakeBook(pageCount: 300, currentPage: 100, started: Now.AddDays(-10));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-8), 30, 20),
            Session(Now.AddDays(-6), 30, 20),
            Session(Now.AddDays(-4), 30, 20),
            Session(Now.AddDays(-2), 30, 20),
            Session(Now.AddDays(-1), 30, 20),
        };

        ReadingForecast? forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now);

        forecast.Should().NotBeNull();
        forecast!.PagesRemaining.Should().Be(200);
        forecast.AveragePagesPerDay.Should().BeGreaterThan(0);
        forecast.AveragePagesPerHour.Should().BeApproximately(40, 0.001); // 20 pages / 0.5h
        forecast.ProjectedCompletionUtc.Should().BeAfter(Now);
        forecast.SessionsUsed.Should().Be(5);
    }

    [Fact]
    public void ConfidenceBand_Orders_OptimisticBeforePointBeforePessimistic()
    {
        Book book = MakeBook(300, 100, Now.AddDays(-10));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-8), 60, 40), // 40 pph
            Session(Now.AddDays(-6), 30, 30), // 60 pph
            Session(Now.AddDays(-4), 40, 10), // 15 pph
            Session(Now.AddDays(-2), 30, 20), // 40 pph
        };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.OptimisticCompletionUtc.Should().BeOnOrBefore(forecast.ProjectedCompletionUtc);
        forecast.ProjectedCompletionUtc.Should().BeOnOrBefore(forecast.PessimisticCompletionUtc);
    }

    [Fact]
    public void NullPageCount_ReturnsNull()
    {
        Book book = MakeBook(pageCount: null, currentPage: 50, started: Now.AddDays(-5));
        var sessions = new List<ReadingSession> { Session(Now.AddDays(-2), 30, 50) };

        ReadingForecastCalculator.TryBuildForecast(book, sessions, Now).Should().BeNull();
    }

    [Fact]
    public void PageCountZero_ReturnsNull()
    {
        Book book = MakeBook(pageCount: 0, currentPage: 0, started: Now.AddDays(-5));
        var sessions = new List<ReadingSession> { Session(Now.AddDays(-2), 30, 10) };

        ReadingForecastCalculator.TryBuildForecast(book, sessions, Now).Should().BeNull();
    }

    [Fact]
    public void NoSessionsAndNoProgress_ReturnsNull()
    {
        Book book = MakeBook(pageCount: 300, currentPage: 0, started: Now.AddDays(-5));

        ReadingForecastCalculator.TryBuildForecast(book, new List<ReadingSession>(), Now).Should().BeNull();
    }

    [Fact]
    public void SingleCountedSession_LowConfidence_BandCollapses()
    {
        Book book = MakeBook(300, 20, Now.AddDays(-2));
        var sessions = new List<ReadingSession> { Session(Now.AddDays(-1), 40, 20) };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.Confidence.Should().Be(ForecastConfidence.Low);
        forecast.HasRange.Should().BeFalse();
        forecast.OptimisticCompletionUtc.Should().Be(forecast.ProjectedCompletionUtc);
        forecast.PessimisticCompletionUtc.Should().Be(forecast.ProjectedCompletionUtc);
    }

    [Fact]
    public void TimeOnlySessions_FallbackPath_DateOnly_LowConfidence()
    {
        Book book = MakeBook(300, 90, Now.AddDays(-3));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-3), 45, null),
            Session(Now.AddDays(-2), 45, null),
            Session(Now.AddDays(-1), 45, null),
        };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.SessionsUsed.Should().Be(0);
        forecast.Confidence.Should().Be(ForecastConfidence.Low);
        forecast.HasRange.Should().BeFalse();
        forecast.AveragePagesPerDay.Should().BeApproximately(30, 0.001); // 90 pages / 3 days
        forecast.PagesRemaining.Should().Be(210);
    }

    [Fact]
    public void CurrentPageAtOrBeyondPageCount_ReturnsNull()
    {
        Book book = MakeBook(300, 300, Now.AddDays(-5));
        var sessions = new List<ReadingSession> { Session(Now.AddDays(-2), 30, 50) };

        ReadingForecastCalculator.TryBuildForecast(book, sessions, Now).Should().BeNull();
    }

    [Fact]
    public void ActualSeries_EndsAtPagesRemaining_WhenSessionSumsDivergeFromCurrentPage()
    {
        // Sessions sum to 80 pages, but the user manually advanced CurrentPage to 100.
        Book book = MakeBook(300, 100, Now.AddDays(-6));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-5), 30, 40),
            Session(Now.AddDays(-3), 30, 40),
        };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.ActualSeries.Should().NotBeEmpty();
        forecast.ActualSeries[0].PagesRemaining.Should().Be(300); // start anchor: full book
        forecast.ActualSeries[^1].PagesRemaining.Should().Be(200); // anchored to PageCount - CurrentPage
    }

    [Fact]
    public void ProjectedSeries_StartsAtRemaining_EndsAtZero()
    {
        Book book = MakeBook(300, 100, Now.AddDays(-6));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-5), 30, 50),
            Session(Now.AddDays(-3), 30, 50),
        };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.ProjectedSeries.Should().HaveCount(2);
        forecast.ProjectedSeries[0].PagesRemaining.Should().Be(forecast.PagesRemaining);
        forecast.ProjectedSeries[^1].PagesRemaining.Should().Be(0);
    }

    [Fact]
    public void RestDays_DilutePagesPerDay()
    {
        var denseSessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-4), 30, 25),
            Session(Now.AddDays(-3), 30, 25),
            Session(Now.AddDays(-2), 30, 25),
            Session(Now.AddDays(-1), 30, 25),
        };
        Book denseBook = MakeBook(300, 100, Now.AddDays(-4));

        var spreadSessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-20), 30, 25),
            Session(Now.AddDays(-15), 30, 25),
            Session(Now.AddDays(-10), 30, 25),
            Session(Now.AddDays(-5), 30, 25),
        };
        Book spreadBook = MakeBook(300, 100, Now.AddDays(-20));

        ReadingForecast dense = ReadingForecastCalculator.TryBuildForecast(denseBook, denseSessions, Now)!;
        ReadingForecast spread = ReadingForecastCalculator.TryBuildForecast(spreadBook, spreadSessions, Now)!;

        dense.AveragePagesPerDay.Should().BeGreaterThan(spread.AveragePagesPerDay);
        dense.ProjectedCompletionUtc.Should().BeBefore(spread.ProjectedCompletionUtc);
    }

    [Fact]
    public void SessionsShorterThanFiveMinutes_AreNotCounted()
    {
        Book book = MakeBook(300, 100, Now.AddDays(-6));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-5), 30, 40), // counts
            Session(Now.AddDays(-4), 3, 15),  // too short — ignored
            Session(Now.AddDays(-2), 2, 25),  // too short — ignored
        };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.SessionsUsed.Should().Be(1); // only the 30-minute session
        forecast.AveragePagesPerHour.Should().BeApproximately(80, 0.001); // 40 pages / 0.5h
    }

    [Fact]
    public void OnlyShortSessions_NoProgress_ReturnsNull()
    {
        Book book = MakeBook(300, 0, Now.AddDays(-4));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-3), 4, 10),
            Session(Now.AddDays(-2), 2, 8),
        };

        ReadingForecastCalculator.TryBuildForecast(book, sessions, Now).Should().BeNull();
    }

    [Fact]
    public void HighVariance_WidensBand_ButStaysWithinSaneBounds()
    {
        Book book = MakeBook(400, 100, Now.AddDays(-12));
        var sessions = new List<ReadingSession>
        {
            Session(Now.AddDays(-10), 60, 80), // 80 pph
            Session(Now.AddDays(-8), 60, 10),  // 10 pph
            Session(Now.AddDays(-6), 60, 70),  // 70 pph
            Session(Now.AddDays(-4), 60, 15),  // 15 pph
            Session(Now.AddDays(-2), 60, 45),  // 45 pph
        };

        ReadingForecast forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, Now)!;

        forecast.HasRange.Should().BeTrue();
        forecast.OptimisticCompletionUtc.Should().BeBefore(forecast.PessimisticCompletionUtc);

        // Band is clamped: slow rate >= 25% of base => pessimistic horizon stays finite & < 10y cap.
        forecast.PessimisticCompletionUtc.Should().BeBefore(Now.AddDays(3650));
    }
}
