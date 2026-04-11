using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IOnboardingService
{
    event EventHandler? StateChanged;

    Task<OnboardingSnapshot> GetSnapshotAsync(CancellationToken ct = default);
    Task<OnboardingSnapshot> RefreshSnapshotAsync(CancellationToken ct = default);
    Task<OnboardingSnapshot> AdvanceIntroAsync(CancellationToken ct = default);
    Task<OnboardingSnapshot> RetreatIntroAsync(CancellationToken ct = default);
    Task<OnboardingSnapshot> CompleteIntroAsync(bool skipped, CancellationToken ct = default);
    Task<OnboardingSnapshot> ResetIntroAsync(CancellationToken ct = default);
    Task<OnboardingSnapshot> ResetAllAsync(CancellationToken ct = default);
    Task<OnboardingSnapshot> TrackEventAsync(OnboardingEvent onboardingEvent, Guid? entityId = null, CancellationToken ct = default);
}
