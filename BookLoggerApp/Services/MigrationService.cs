using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Abstractions;
using System.IO;
using System.Text;
using Microsoft.Maui.Storage;

namespace BookLoggerApp.Services;

public class MigrationService : IMigrationService
{
    private static readonly StringBuilder MemoryLog = new();
    private readonly string _logFilePath;

    public MigrationService()
    {
        _logFilePath = Path.Combine(FileSystem.AppDataDirectory, "migration_debug.log");
    }

    public string GetMigrationLog()
    {
        var memoryLog = MemoryLog.ToString();
        string initLog;
        lock (DatabaseInitializationHelper.InitLog)
        {
            initLog = DatabaseInitializationHelper.InitLog.ToString();
        }
        var fileLog = File.Exists(_logFilePath) ? File.ReadAllText(_logFilePath) : string.Empty;

        return
            "--- DB Init Log (Current Session) ---\n" + initLog + "\n\n" +
            "--- Migration Service Log ---\n" + memoryLog + "\n\n" +
            "--- Persistent Log (History) ---\n" + fileLog;
    }

    public void Log(string message)
    {
        var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        System.Diagnostics.Debug.WriteLine(logMsg);
        MemoryLog.AppendLine(logMsg);

        try
        {
            File.AppendAllText(_logFilePath, logMsg + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }
}
