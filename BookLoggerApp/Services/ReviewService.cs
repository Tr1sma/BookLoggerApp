using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;
#if ANDROID
using Android.Gms.Extensions;
using Xamarin.Google.Android.Play.Core.Review;
using Xamarin.Google.Android.Play.Core.Review.Testing;
#endif

namespace BookLoggerApp.Services;

/// <summary>
/// Google Play In-App Review service with throttling.
/// Handles both rate-limiting (max ~2x/month, min Level 6) and native API calls.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly ILogger<ReviewService> _logger;
    private const int MinDaysBetweenPrompts = 14;
    private const int MaxPromptsPerMonth = 2;
    private const int MinUserLevel = 6;

    public ReviewService(IAppSettingsProvider settingsProvider, ILogger<ReviewService> logger)
    {
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task TryRequestReviewAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            var now = DateTime.UtcNow;

            _logger.LogInformation("[ReviewService] TryRequestReview — Level={Level}, LastPrompt={LastPrompt}, MonthCount={MonthCount}",
                settings.UserLevel, settings.LastReviewPromptDate, settings.ReviewPromptMonthCount);

            // 1. Mindest-Level prüfen
            if (settings.UserLevel < MinUserLevel)
            {
                _logger.LogInformation("[ReviewService] Skipped: level {Level} < {MinLevel}", settings.UserLevel, MinUserLevel);
                return;
            }

            // 2. Monatlichen Zähler prüfen (max. 2x pro Kalendermonat)
            var lastDate = settings.LastReviewPromptDate;
            if (lastDate.HasValue &&
                lastDate.Value.Year == now.Year &&
                lastDate.Value.Month == now.Month &&
                settings.ReviewPromptMonthCount >= MaxPromptsPerMonth)
            {
                _logger.LogInformation("[ReviewService] Skipped: month count {Count} >= {Max}", settings.ReviewPromptMonthCount, MaxPromptsPerMonth);
                return;
            }

            // 3. Mindestabstand von 14 Tagen zwischen zwei Prompts prüfen
            if (lastDate.HasValue && (now - lastDate.Value).TotalDays < MinDaysBetweenPrompts)
            {
                _logger.LogInformation("[ReviewService] Skipped: only {Days:F1} days since last prompt", (now - lastDate.Value).TotalDays);
                return;
            }

            _logger.LogInformation("[ReviewService] All checks passed — launching review flow on main thread");

#if ANDROID
            // Play Core Review API muss auf dem Main Thread aufgerufen werden
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var activity = Platform.CurrentActivity;
                if (activity == null)
                {
                    _logger.LogWarning("[ReviewService] Skipped: Platform.CurrentActivity is null");
                    return;
                }

#if DEBUG
                // FakeReviewManager simuliert den Dialog ohne Play-Store-Install — nur für Entwicklung
                IReviewManager manager = new FakeReviewManager(activity);
#else
                IReviewManager manager = ReviewManagerFactory.Create(activity);
#endif

                var reviewInfo = await manager.RequestReviewFlow().AsAsync<ReviewInfo>();
                if (reviewInfo == null)
                {
                    _logger.LogWarning("[ReviewService] ReviewInfo was null after RequestReviewFlow");
                    return;
                }

                await manager.LaunchReviewFlow(activity, reviewInfo).AsAsync();
                _logger.LogInformation("[ReviewService] LaunchReviewFlow completed");
            });
#endif

            // Datum und Monats-Zähler aktualisieren
            bool sameMonth = lastDate.HasValue &&
                             lastDate.Value.Year == now.Year &&
                             lastDate.Value.Month == now.Month;

            settings.LastReviewPromptDate = now;
            settings.ReviewPromptMonthCount = sameMonth ? settings.ReviewPromptMonthCount + 1 : 1;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
            _logger.LogInformation("[ReviewService] Settings updated — MonthCount now {Count}", settings.ReviewPromptMonthCount);
        }
        catch (Exception ex)
        {
            // Review ist nicht kritisch — App darf nie deswegen abstürzen
            _logger.LogError(ex, "[ReviewService] Exception in TryRequestReviewAsync");
        }
    }
}
