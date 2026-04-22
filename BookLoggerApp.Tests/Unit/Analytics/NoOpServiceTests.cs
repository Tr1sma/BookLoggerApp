using BookLoggerApp.Core.Services.Analytics;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Analytics;

public class NoOpServiceTests
{
    [Fact]
    public void NoOpAnalyticsService_never_throws()
    {
        var svc = NoOpAnalyticsService.Instance;

        var act = () =>
        {
            svc.LogEvent("event");
            svc.LogEvent("event", null);
            svc.LogEvent("event", new Dictionary<string, object?> { ["k"] = "v" });
            svc.LogScreenView("screen");
            svc.LogScreenView("screen", "class");
            svc.SetUserProperty("prop", "val");
            svc.SetUserProperty("prop", null);
            svc.SetUserId(null);
            svc.SetUserId("id");
            svc.SetAnalyticsCollectionEnabled(true);
            svc.SetAnalyticsCollectionEnabled(false);
            svc.ResetAnalyticsData();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void NoOpCrashReportingService_never_throws()
    {
        var svc = NoOpCrashReportingService.Instance;

        var act = () =>
        {
            svc.RecordNonFatal(new InvalidOperationException("x"));
            svc.RecordNonFatal(new InvalidOperationException("x"), new Dictionary<string, string> { ["k"] = "v" });
            svc.RecordFatal(new InvalidOperationException("x"));
            svc.Log("message");
            svc.SetUserId(null);
            svc.SetUserId("id");
            svc.SetCustomKey("key", null);
            svc.SetCustomKey("key", "value");
            svc.SetCrashlyticsCollectionEnabled(true);
            svc.SetCrashlyticsCollectionEnabled(false);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Singletons_are_stable()
    {
        NoOpAnalyticsService.Instance.Should().BeSameAs(NoOpAnalyticsService.Instance);
        NoOpCrashReportingService.Instance.Should().BeSameAs(NoOpCrashReportingService.Instance);
    }
}
