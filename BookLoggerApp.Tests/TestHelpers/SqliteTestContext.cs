using BookLoggerApp.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// SQLite-backed (in-memory) test context factory.
///
/// <para>The EF Core <c>InMemory</c> provider does NOT enforce primary-key/unique
/// constraints, real transactions, or optimistic-concurrency tokens, so bugs in those
/// areas (e.g. RowVersion concurrency, duplicate-PK import, restore rollback) cannot be
/// reproduced with it. This fixture uses a real SQLite engine via a single shared,
/// kept-open connection so the in-memory database survives across multiple
/// <see cref="AppDbContext"/> instances — mirroring how the app uses
/// <c>IDbContextFactory</c> to create a fresh context per operation against one file DB.</para>
///
/// <para>Schema is created from the EF model via <c>EnsureCreated()</c> (including seed
/// data), so it always matches the current model regardless of migrations.</para>
/// </summary>
public sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestContext()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a fresh <see cref="AppDbContext"/> bound to the shared in-memory connection.
    /// Each call returns a new context (new change-tracker) over the same database.
    /// </summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Returns an <see cref="IDbContextFactory{AppDbContext}"/> over the shared connection,
    /// mirroring how production services (e.g. <c>AppSettingsProvider</c>) create a fresh
    /// context per operation.
    /// </summary>
    public IDbContextFactory<AppDbContext> CreateFactory() => new Factory(this);

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly SqliteTestContext _owner;
        public Factory(SqliteTestContext owner) => _owner = owner;
        public AppDbContext CreateDbContext() => _owner.CreateContext();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
