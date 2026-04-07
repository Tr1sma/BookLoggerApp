namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAppVersionService
{
    string CurrentVersion { get; }
    string CurrentBuild { get; }
    string? PreviousVersion { get; }
    bool IsFirstLaunchForCurrentVersion { get; }

    void TrackCurrentVersion();
}
