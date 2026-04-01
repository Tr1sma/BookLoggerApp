using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
#if ANDROID
using Android.Gms.Extensions;
using Xamarin.Google.Android.Play.Core.Review;
#endif

namespace BookLoggerApp.Services;

/// <summary>
/// Launches the native review flow for the current platform.
/// </summary>
public class ReviewPlatformLauncher : IReviewPlatformLauncher
{
    private readonly ILogger<ReviewPlatformLauncher> _logger;

    public ReviewPlatformLauncher(ILogger<ReviewPlatformLauncher> logger)
    {
        _logger = logger;
    }

    public async Task<ReviewLaunchOutcome> TryLaunchAsync(CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

#if ANDROID
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var activity = Platform.CurrentActivity;
                if (activity == null)
                {
                    _logger.LogWarning("[ReviewPlatformLauncher] Skipped: Platform.CurrentActivity is null");
                    return ReviewLaunchOutcome.SkippedNoActivity;
                }

                IReviewManager manager = ReviewManagerFactory.Create(activity);

                var reviewInfo = await manager.RequestReviewFlow().AsAsync<ReviewInfo>();
                if (reviewInfo == null)
                {
                    _logger.LogWarning("[ReviewPlatformLauncher] ReviewInfo was null after RequestReviewFlow");
                    return ReviewLaunchOutcome.Failed;
                }

                await manager.LaunchReviewFlow(activity, reviewInfo).AsAsync();
                _logger.LogInformation("[ReviewPlatformLauncher] LaunchReviewFlow completed via Play Store");
                return ReviewLaunchOutcome.RequestedFromPlayStore;
            });
#else
            _logger.LogInformation("[ReviewPlatformLauncher] Skipped: platform does not support native review flow");
            await Task.CompletedTask;
            return ReviewLaunchOutcome.SkippedUnsupportedPlatform;
#endif
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ReviewPlatformLauncher] Failed to launch native review flow");
            return ReviewLaunchOutcome.Failed;
        }
    }
}
