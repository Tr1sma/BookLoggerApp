namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAppVersionService
{
    string CurrentVersion { get; }
    string CurrentBuild { get; }
    string? PreviousVersion { get; }
    bool IsFirstLaunchForCurrentVersion { get; }

    void TrackCurrentVersion();

    /// <summary>
    /// Version string whose changelog the user last dismissed, or null if none.
    /// Used to suppress repeated changelog prompts within the same installed version,
    /// independent of MAUI's <c>VersionTracking</c> (which is unreliable on some devices).
    /// </summary>
    string? LastSeenChangelogVersion { get; }

    /// <summary>Persist the fact that the user has dismissed the changelog for <paramref name="version"/>.</summary>
    void MarkChangelogSeen(string version);
}
