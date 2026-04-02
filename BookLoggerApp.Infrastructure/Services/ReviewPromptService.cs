using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using System.Threading;

namespace BookLoggerApp.Infrastructure.Services;

public class ReviewPromptService : IReviewPromptService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ReviewPromptService(IAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public async Task<bool> TryStartPromptAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);

        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            if (settings.ReviewPromptDisabled || settings.UserLevel <= 6)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            ResetMonthlyCounterIfNeeded(settings, now);
            if (settings.ReviewPromptMonthCount >= 2)
            {
                return false;
            }

            settings.ReviewPromptMonthCount++;
            settings.LastReviewPromptDate = now;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisablePromptAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);

        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            if (settings.ReviewPromptDisabled)
            {
                return;
            }

            settings.ReviewPromptDisabled = true;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void ResetMonthlyCounterIfNeeded(AppSettings settings, DateTime now)
    {
        if (!settings.LastReviewPromptDate.HasValue)
        {
            settings.ReviewPromptMonthCount = 0;
            return;
        }

        var lastPromptDate = settings.LastReviewPromptDate.Value;
        if (lastPromptDate.Year != now.Year || lastPromptDate.Month != now.Month)
        {
            settings.ReviewPromptMonthCount = 0;
        }
    }
}