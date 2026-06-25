using System.IO;
using System;

namespace BookLoggerApp.Infrastructure;

// Returns a writable app-data path (Android/iOS/Windows safe).
public static class PlatformsDbPath
{
    public static string GetDatabasePath(string fileName = "booklogger.db3")
    {
        // LocalApplicationData → .../files/.local/share/ on Android.
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, fileName);

        // Legacy location (Personal → .../files/ on Android) used by older Xamarin/MAUI examples.
        var legacyFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        var legacyDbPath = Path.Combine(legacyFolder, fileName);

        return dbPath;
    }
}
