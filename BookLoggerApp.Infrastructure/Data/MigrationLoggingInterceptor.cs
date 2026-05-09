using System.Data.Common;
using BookLoggerApp.Core.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Logs every SQL command executed by EF Core to <see cref="DatabaseInitializationHelper.InitLog"/>
/// while <see cref="Enabled"/> is true. Used to surface exactly which SQL statement is
/// hanging or running slowly during migrations on slow Android devices.
///
/// Toggled around <see cref="DbInitializer"/>.MigrateDatabaseAsync so the log isn't
/// spammed during normal app operation.
/// </summary>
public sealed class MigrationLoggingInterceptor : DbCommandInterceptor
{
    public static bool Enabled;

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        if (Enabled) LogCommandStart(command, "NonQuery");
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled) LogCommandStart(command, "NonQueryAsync");
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        if (Enabled) LogCommandEnd("NonQuery", result, eventData.Duration.TotalMilliseconds);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled) LogCommandEnd("NonQueryAsync", result, eventData.Duration.TotalMilliseconds);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (Enabled) LogCommandStart(command, "Reader");
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled) LogCommandStart(command, "ReaderAsync");
        return ValueTask.FromResult(result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        if (Enabled) LogCommandEnd("Reader", null, eventData.Duration.TotalMilliseconds);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled) LogCommandEnd("ReaderAsync", null, eventData.Duration.TotalMilliseconds);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        if (Enabled) LogCommandStart(command, "Scalar");
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled) LogCommandStart(command, "ScalarAsync");
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        if (Enabled) LogCommandEnd("Scalar", null, eventData.Duration.TotalMilliseconds);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled) LogCommandEnd("ScalarAsync", null, eventData.Duration.TotalMilliseconds);
        return ValueTask.FromResult(result);
    }

    private static void LogCommandStart(DbCommand command, string kind)
    {
        var sql = command.CommandText.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (sql.Length > 220)
        {
            sql = sql[..220] + "...";
        }
        DatabaseInitializationHelper.AppendInitLog($"    [SQL>] ({kind}) {sql}");
    }

    private static void LogCommandEnd(string kind, int? rowsAffected, double durationMs)
    {
        var rows = rowsAffected.HasValue ? $" rows={rowsAffected.Value}" : string.Empty;
        DatabaseInitializationHelper.AppendInitLog(
            $"    [SQL<] ({kind}) {durationMs:F0}ms{rows}");
    }
}
