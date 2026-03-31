namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for requesting Google Play In-App Reviews.
/// Handles throttling internally — safe to call after any positive moment.
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Checks throttle constraints (level >= 6, max 2x/month) and,
    /// if allowed, launches the native review dialog.
    /// Silently no-ops if throttled or on non-Android platforms.
    /// </summary>
    Task TryRequestReviewAsync(CancellationToken ct = default);
}
