using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Google Play in-app review service with monthly throttling.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IReviewPlatformLauncher _platformLauncher;
    private readonly ILogger<ReviewService> _logger;
    private const int MaxPromptsPerMonth = 2;
    private const int MinUserLevel = 6;

    public ReviewService(
        IAppSettingsProvider settingsProvider,
        IReviewPlatformLauncher platformLauncher,
        ILogger<ReviewService> logger)
    {
        _settingsProvider = settingsProvider;
        _platformLauncher = platformLauncher;
        _logger = logger;
    }

    public async Task TryRequestReviewAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            var now = DateTime.UtcNow;
            var lastPromptDate = settings.LastReviewPromptDate;
            bool sameMonth = lastPromptDate.HasValue &&
                             lastPromptDate.Value.Year == now.Year &&
                             lastPromptDate.Value.Month == now.Month;

            int promptsThisMonth = sameMonth ? settings.ReviewPromptMonthCount : 0;

            _logger.LogInformation(
                "[ReviewService] TryRequestReview — Level={Level}, LastPrompt={LastPrompt}, MonthCount={MonthCount}",
                settings.UserLevel,
                settings.LastReviewPromptDate,
                settings.ReviewPromptMonthCount);

            if (settings.UserLevel < MinUserLevel)
            {
                _logger.LogInformation(
                    "[ReviewService] Skipped: level {Level} < {MinLevel}",
                    settings.UserLevel,
                    MinUserLevel);
                return;
            }

            if (promptsThisMonth >= MaxPromptsPerMonth)
            {
                _logger.LogInformation(
                    "[ReviewService] Skipped: month count {Count} >= {Max}",
                    promptsThisMonth,
                    MaxPromptsPerMonth);
                return;
            }

            var launchOutcome = await _platformLauncher.TryLaunchAsync(ct);
            if (launchOutcome != ReviewLaunchOutcome.RequestedFromPlayStore)
            {
                _logger.LogInformation(
                    "[ReviewService] Skipped: native review flow outcome was {Outcome}",
                    launchOutcome);
                return;
            }

            settings.LastReviewPromptDate = now;
            settings.ReviewPromptMonthCount = promptsThisMonth + 1;

            await _settingsProvider.UpdateSettingsAsync(settings, ct);

            _logger.LogInformation(
                "[ReviewService] Settings updated — MonthCount now {Count}",
                settings.ReviewPromptMonthCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReviewService] Exception in TryRequestReviewAsync");
        }
    }
}
