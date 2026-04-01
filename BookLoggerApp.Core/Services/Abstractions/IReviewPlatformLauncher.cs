namespace BookLoggerApp.Core.Services.Abstractions;

public enum ReviewLaunchOutcome
{
    RequestedFromPlayStore,
    SkippedNoActivity,
    SkippedUnsupportedPlatform,
    Failed
}

/// <summary>
/// Launches the native in-app review flow for the current platform.
/// </summary>
public interface IReviewPlatformLauncher
{
    /// <summary>
    /// Attempts to start the native review flow and returns the concrete outcome.
    /// </summary>
    Task<ReviewLaunchOutcome> TryLaunchAsync(CancellationToken ct = default);
}
