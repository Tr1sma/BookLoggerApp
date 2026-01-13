using System.IO;
using System; // For Environment

namespace BookLoggerApp.Infrastructure;

// Returns a writable path in app data (Android/iOS/Windows safe)
public static class PlatformsDbPath
{
    public static string GetDatabasePath(string fileName = "booklogger.db3")
    {
        // Use Environment.GetFolderPath for cross-platform app data directory
        // New Path: .../files/.local/share/ (on Android)
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, fileName);

        // Legacy Check: .../files/ (on Android) used by System.Environment.SpecialFolder.Personal
        // Many older Xamarin/MAUI examples used Personal.
        var legacyFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        var legacyDbPath = Path.Combine(legacyFolder, fileName);

        return dbPath;
    }
}
