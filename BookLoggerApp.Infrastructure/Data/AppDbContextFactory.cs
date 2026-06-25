using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>Design-time factory for AppDbContext used by EF Core migrations.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseSqlite("Data Source=booklogger_designtime.db3");

        return new AppDbContext(optionsBuilder.Options);
    }
}
