namespace BookLoggerApp.Core.Services.Abstractions;

public interface IMigrationService
{
    string GetMigrationLog();
    void Log(string message);
}
