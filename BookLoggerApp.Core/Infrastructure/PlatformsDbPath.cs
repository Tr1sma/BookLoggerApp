using System.IO;
using System; // For Environment

namespace BookLoggerApp.Infrastructure;

// Returns a writable path in app data (Android/iOS/Windows safe)
public static class PlatformsDbPath
{
    public static string GetDatabasePath(string fileName = "booklogger.db3")
    {
        // Use Environment.GetFolderPath for cross-platform app data directory
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, fileName);
    }
}
