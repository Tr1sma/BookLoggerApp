using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using System.IO;
using Microsoft.Maui.Storage;

namespace BookLoggerApp.Services;

public class MigrationService : IMigrationService
{
    private readonly string _logFilePath;

    public MigrationService()
    {
        _logFilePath = Path.Combine(FileSystem.AppDataDirectory, "migration_debug.log");
    }

    public string GetMigrationLog()
    {
        // Combine memory log (from startup helper) and persistent log
        var memoryLog = DatabaseMigrationHelper.Log.ToString();
        var fileLog = File.Exists(_logFilePath) ? File.ReadAllText(_logFilePath) : string.Empty;
        
        return $"--- Memory Log (Current Session) ---\n{memoryLog}\n\n--- Persistent Log (History) ---\n{fileLog}";
    }

    public void Log(string message)
    {
        var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        System.Diagnostics.Debug.WriteLine(logMsg);
        
        // Update memory log
        DatabaseMigrationHelper.Log.AppendLine(logMsg);
        
        // Update persistent log
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
