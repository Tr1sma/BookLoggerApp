using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Helper class that wraps AppDbContext for test scenarios requiring a disposable context wrapper.
/// </summary>
public class DbContextTestHelper : IDisposable
{
    public AppDbContext Context { get; }

    private DbContextTestHelper(AppDbContext context)
    {
        Context = context;
    }

    public static DbContextTestHelper CreateTestContext()
    {
        var context = TestDbContext.Create();
        return new DbContextTestHelper(context);
    }

    public void Dispose()
    {
        Context?.Dispose();
    }
}

