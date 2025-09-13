using BookLoggerApp.Core.Services;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Services (DI)
            builder.Services.AddSingleton<IBookService, InMemoryBookService>();
            builder.Services.AddSingleton<IProgressService, InMemoryProgressService>();

            // ViewModels (DI)
            builder.Services.AddTransient<BookListViewModel>();
            builder.Services.AddTransient<BookDetailViewModel>();

            return builder.Build();
        }
    }
}
