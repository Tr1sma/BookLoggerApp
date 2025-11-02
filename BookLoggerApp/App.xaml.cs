using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp
{
    public partial class App : Application
    {
        private readonly AppDbContext _dbContext;

        public App(AppDbContext dbContext)
        {
            InitializeComponent();
            _dbContext = dbContext;

            // Apply migrations on startup
            Task.Run(async () =>
            {
                try
                {
                    await _dbContext.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    // Log error or show notification
                    System.Diagnostics.Debug.WriteLine($"Database migration failed: {ex.Message}");
                }
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "BookLoggerApp" };
        }
    }
}
