using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Retry()
    {
        DatabaseInitializationHelper.ResetForRetry();

        _ = Task.Run(async () =>
        {
            try
            {
                var logger = _serviceProvider.GetService<ILogger<AppDbContext>>();
                await DbInitializer.InitializeAsync(_serviceProvider, logger);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== EXCEPTION IN DATABASE RETRY ===");
                System.Diagnostics.Debug.WriteLine($"{ex}");
                System.Diagnostics.Debug.WriteLine("=== END EXCEPTION ===");

                DatabaseInitializationHelper.MarkAsFailed(ex);
            }
        });
    }
}
