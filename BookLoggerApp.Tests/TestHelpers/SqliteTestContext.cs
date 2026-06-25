using BookLoggerApp.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// SQLite-backed in-memory test context. Unlike the EF InMemory provider, real SQLite enforces
/// constraints, transactions and concurrency tokens, so those bugs are reproducible. Uses one
/// shared kept-open connection so the DB survives across <see cref="AppDbContext"/> instances;
/// schema built via <c>EnsureCreated()</c> so it always matches the current model.
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
    /// Creates a fresh <see cref="AppDbContext"/> (new change-tracker) over the shared connection.
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
    /// mirroring how production services create a fresh context per operation.
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
