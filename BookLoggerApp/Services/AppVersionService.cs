using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Maui.ApplicationModel;

namespace BookLoggerApp.Services;

public sealed class AppVersionService : IAppVersionService
{
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
}
