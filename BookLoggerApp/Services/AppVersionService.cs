using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace BookLoggerApp.Services;

public sealed class AppVersionService : IAppVersionService
{
    private const string PrefKeyLastSeenChangelogVersion = "last_seen_changelog_version";

    private bool _tracked;

    public string CurrentVersion
    {
        get
        {
            TrackCurrentVersion();
            return AppInfo.Current.VersionString;
        }
    }

    public string CurrentBuild
    {
        get
        {
            TrackCurrentVersion();
            return AppInfo.Current.BuildString;
        }
    }

    public string? PreviousVersion
    {
        get
        {
            TrackCurrentVersion();
            return VersionTracking.Default.PreviousVersion;
        }
    }

    public bool IsFirstLaunchForCurrentVersion
    {
        get
        {
            TrackCurrentVersion();
            return VersionTracking.Default.IsFirstLaunchForCurrentVersion;
        }
    }

    public void TrackCurrentVersion()
    {
        if (_tracked)
        {
            return;
        }

        VersionTracking.Default.Track();
        _tracked = true;
    }

    public string? LastSeenChangelogVersion
    {
        get
        {
            string value = Preferences.Default.Get(PrefKeyLastSeenChangelogVersion, string.Empty);
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    public void MarkChangelogSeen(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        Preferences.Default.Set(PrefKeyLastSeenChangelogVersion, version);
    }
}
