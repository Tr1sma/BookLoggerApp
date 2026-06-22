using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Abstractions;
using System.IO;
using System.Text;
using Microsoft.Maui.Storage;

namespace BookLoggerApp.Services;

public class MigrationService : IMigrationService
{
    // Z.711: bound both sinks so a long-lived install or a chatty migration loop can't grow the
    // debug log without limit. The persistent file is truncated to its tail once it crosses the
    // cap; the in-memory buffer keeps only its most recent slice.
    private const long MaxLogFileBytes = 256 * 1024;
    private const int MaxMemoryLogChars = 64 * 1024;

    private static readonly StringBuilder MemoryLog = new();
    private readonly string _logFilePath;

    public MigrationService()
    {
        _logFilePath = Path.Combine(FileSystem.AppDataDirectory, "migration_debug.log");
    }

    public string GetMigrationLog()
    {
        // Combine in-memory service log, DB-init log and persistent log
        string memoryLog;
        lock (MemoryLog)
        {
            memoryLog = MemoryLog.ToString();
        }
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

        // Update memory log (bounded — drop the oldest slice once over the cap)
        lock (MemoryLog)
        {
            MemoryLog.AppendLine(logMsg);
            if (MemoryLog.Length > MaxMemoryLogChars)
            {
                MemoryLog.Remove(0, MemoryLog.Length - MaxMemoryLogChars);
            }
        }

        // Update persistent log (rotate first so it stays bounded)
        try
        {
            RotateLogFileIfOversized();
            File.AppendAllText(_logFilePath, logMsg + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Truncates the persistent log to its most recent half once it grows past
    /// <see cref="MaxLogFileBytes"/>, so the migration history survives across runs without the
    /// file growing unbounded. A partial leading line is dropped so the kept log starts cleanly.
    /// </summary>
    private void RotateLogFileIfOversized()
    {
        try
        {
            var info = new FileInfo(_logFilePath);
            if (!info.Exists || info.Length <= MaxLogFileBytes)
            {
                return;
            }

            var text = File.ReadAllText(_logFilePath);
            int keep = (int)(MaxLogFileBytes / 2);
            if (text.Length > keep)
            {
                text = text.Substring(text.Length - keep);
                int firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0 && firstNewline + 1 < text.Length)
                {
                    text = text.Substring(firstNewline + 1);
                }
            }

            File.WriteAllText(_logFilePath, "--- log rotated (truncated) ---" + Environment.NewLine + text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Log rotation failed: {ex.Message}");
        }
    }
}
