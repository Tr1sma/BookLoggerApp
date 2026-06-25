using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.Services.Analytics;

public sealed class UserPropertiesPublisher
{
    private readonly IAnalyticsService _analytics;
    private readonly ICrashReportingService? _crashReporting;

    // Crash reporter optional (default null) so tests can construct with analytics only; reporter respects consent.
    public UserPropertiesPublisher(IAnalyticsService analytics, ICrashReportingService? crashReporting = null)
    {
        _analytics = analytics;
        _crashReporting = crashReporting;
    }

    public void PublishSettingsOnly(AppSettings settings)
    {
        try
        {
            _analytics.SetUserProperty(UserPropertyNames.UserLevelBucket, AnalyticsBuckets.Level(settings.UserLevel));
            _analytics.SetUserProperty(UserPropertyNames.Theme, settings.Theme);
            _analytics.SetUserProperty(UserPropertyNames.Language, settings.Language);
            _analytics.SetUserProperty(UserPropertyNames.OnboardingCompleted, settings.HasCompletedOnboarding.ToString().ToLowerInvariant());
            _analytics.SetUserProperty(UserPropertyNames.NotificationsEnabled, settings.NotificationsEnabled.ToString().ToLowerInvariant());
            _analytics.SetUserProperty(UserPropertyNames.AppInstallAgeBucket, AnalyticsBuckets.InstallAge(settings.CreatedAt));
            _analytics.SetUserProperty(UserPropertyNames.PlantsOwnedBucket, AnalyticsBuckets.Plants(settings.PlantsPurchased));
        }
        catch (Exception ex)
        {
            ReportNonFatal(ex, "PublishSettingsOnly");
        }
    }

    public void PublishWithStats(AppSettings settings, int totalBooks, int completedBooks, int totalSessions, int plantsOwned, bool hasActiveGoal)
    {
        PublishSettingsOnly(settings);
        try
        {
            _analytics.SetUserProperty(UserPropertyNames.TotalBooksBucket, AnalyticsBuckets.BookCount(totalBooks));
            _analytics.SetUserProperty(UserPropertyNames.CompletedBooksBucket, AnalyticsBuckets.BookCount(completedBooks));
            _analytics.SetUserProperty(UserPropertyNames.TotalSessionsBucket, AnalyticsBuckets.Sessions(totalSessions));
            _analytics.SetUserProperty(UserPropertyNames.PlantsOwnedBucket, AnalyticsBuckets.Plants(plantsOwned));
            _analytics.SetUserProperty(UserPropertyNames.HasActiveGoal, hasActiveGoal.ToString().ToLowerInvariant());
        }
        catch (Exception ex)
        {
            ReportNonFatal(ex, "PublishWithStats");
        }
    }

    // Surface swallowed failure as a non-fatal (consent-gated) so publish breakages are observable, not silent.
    private void ReportNonFatal(Exception ex, string phase)
    {
        System.Diagnostics.Debug.WriteLine($"UserPropertiesPublisher.{phase} failed: {ex}");
        try
        {
            _crashReporting?.RecordNonFatal(ex, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "user_properties_publisher",
                ["phase"] = phase,
            });
        }
        catch
        {
            // A failing crash sink must never replace the original swallow.
        }
    }
}
