using System.IO;
using System.Runtime.CompilerServices;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Helpers to create unique, throwaway database files for each test.
/// </summary>
public static class TestDb
{
    public static string NewPath([CallerMemberName] string testName = "")
    {
        var file = $"booklogger_{testName}_{Guid.NewGuid():N}.db3";
        return Path.Combine(Path.GetTempPath(), file);
    }

    public static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
