using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Services;

public class MigrationService : IMigrationService
{
    public string GetMigrationLog()
    {
        return DatabaseMigrationHelper.Log.ToString();
    }
}
