namespace BookLoggerApp.Core.Models;

public sealed class AppUpdateState
{
    public static AppUpdateState Unsupported { get; } = new()
    {
        IsSupported = false
    };

    public bool IsSupported { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public bool IsUpdateDownloaded { get; init; }
    public bool IsUpdateInProgress { get; init; }
    public bool CanStartFlexibleUpdate { get; init; }
}
