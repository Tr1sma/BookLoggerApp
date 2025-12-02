using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Helper class that wraps AppDbContext for test scenarios requiring a disposable context wrapper.
/// </summary>
public class DbContextTestHelper : IDisposable
{
    public AppDbContext Context { get; }
    public string DatabaseName { get; }

    private DbContextTestHelper(AppDbContext context, string databaseName)
    {
        Context = context;
        DatabaseName = databaseName;
    }

    public static DbContextTestHelper CreateTestContext()
    {
        var databaseName = Guid.NewGuid().ToString();
        var context = TestDbContext.Create(databaseName);
        return new DbContextTestHelper(context, databaseName);
    }

    public void Dispose()
    {
        Context?.Dispose();
    }
}

