using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating AppDbContext for EF Core migrations.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use a temporary database path for design-time operations
        optionsBuilder.UseSqlite("Data Source=booklogger_designtime.db3");

        return new AppDbContext(optionsBuilder.Options);
    }
}
