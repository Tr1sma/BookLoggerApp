using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp
{
    public partial class App : Application
    {
        public App(AppDbContext dbContext)
        {
            InitializeComponent();

            // Apply migrations on startup (synchronously to avoid race conditions)
            try
            {
                dbContext.Database.Migrate();
                System.Diagnostics.Debug.WriteLine("Database migration completed successfully");
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Database migration failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to make the error visible
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "BookLoggerApp" };
        }
    }
}
