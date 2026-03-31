using BookLoggerApp.Core.Services.Abstractions;
#if ANDROID
using Android.Gms.Extensions;
using Xamarin.Google.Android.Play.Core.Review;
#endif

namespace BookLoggerApp.Services;

/// <summary>
/// Google Play In-App Review service with throttling.
/// Handles both rate-limiting (max ~2x/month, min Level 6) and native API calls.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private const int MinDaysBetweenPrompts = 14;
    private const int MinUserLevel = 6;

    public ReviewService(IAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public async Task TryRequestReviewAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);

            if (settings.UserLevel < MinUserLevel)
                return;

            if (settings.LastReviewPromptDate.HasValue)
            {
                var daysSince = (DateTime.UtcNow - settings.LastReviewPromptDate.Value).TotalDays;
                if (daysSince < MinDaysBetweenPrompts)
                    return;
            }

#if ANDROID
            var activity = Platform.CurrentActivity;
            if (activity == null)
                return;

            var manager = ReviewManagerFactory.Create(activity);

            var reviewInfo = await manager.RequestReviewFlow().AsAsync<ReviewInfo>();
            if (reviewInfo == null)
                return;

            await manager.LaunchReviewFlow(activity, reviewInfo).AsAsync();
#endif

            settings.LastReviewPromptDate = DateTime.UtcNow;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
        }
        catch
        {
            // Review ist nicht kritisch — App darf nie deswegen abstürzen
        }
    }
}
