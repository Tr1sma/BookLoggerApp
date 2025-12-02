using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Helper class for creating in-memory test database contexts.
/// </summary>
public static class TestDbContext
{
    public static AppDbContext Create()
    {
        return Create(Guid.NewGuid().ToString());
    }

    public static AppDbContext Create(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}

/// <summary>
/// Test implementation of IDbContextFactory for use in unit tests.
/// Creates new context instances that share the same in-memory database.
/// </summary>
public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly string _databaseName;

    public TestDbContextFactory(string databaseName)
    {
        _databaseName = databaseName;
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
