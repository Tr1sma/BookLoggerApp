using System.Globalization;
using BookLoggerApp;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Infrastructure;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Plugin.LocalNotification;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        System.Diagnostics.Debug.WriteLine("=== MauiProgram.CreateMauiApp Started ===");

        // Must run before any string resource is resolved so the very first render
        // (AppStartupOverlay) already picks the correct culture.
        InitializeCulture();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .ConfigureEssentials(essentials =>
               {
                   essentials.UseVersionTracking();
               })
               .UseBarcodeReader()
               .UseLocalNotification();
        builder.Services.AddMauiBlazorWebView();
        // ResourcesPath stays empty: the AppResources marker type lives in the
        // BookLoggerApp.Core.Resources namespace already, and ResourceManagerStringLocalizer
        // derives the base name from the type's namespace. Setting ResourcesPath = "Resources"
        // would produce BookLoggerApp.Core.Resources.Resources.AppResources.
        builder.Services.AddLocalization();

        // Configure platform-specific handlers
        ConfigurePlatformHandlers(builder);

        ConfigureLogging(builder);
        RegisterDatabase(builder);
        RegisterRepositories(builder);
        RegisterBusinessServices(builder);
        RegisterViewModels(builder);
        RegisterValidators(builder);

        System.Diagnostics.Debug.WriteLine("All services registered, building app...");

        var app = builder.Build();

        // Wire the ambient CrashReporter on ViewModelBase so ExecuteSafely* catch-blocks
        // forward non-fatals to Crashlytics (no-op outside Android).
        AnalyticsBootstrapper.Install(app.Services.GetRequiredService<ICrashReportingService>());

        // Wire the ambient localizer on ViewModelBase for the generic fallbacks used
        // by ExecuteSafely*Async when a caller didn't pass an explicit errorPrefix.
        BookLoggerApp.Core.ViewModels.ViewModelBase.Localizer =
            app.Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<BookLoggerApp.Core.Resources.AppResources>>();

        // Setup global exception handler
        SetupGlobalExceptionHandler(app);

        InitializeDatabase(app);

        System.Diagnostics.Debug.WriteLine("=== MauiProgram.CreateMauiApp Completed ===");
        return app;
    }

    /// <summary>
    /// Reads the UI language from <see cref="Preferences"/> and sets the thread culture
    /// before any Blazor render happens. On first launch the <see cref="Preferences"/>
    /// key is absent; in that case the system culture is detected and persisted so the
    /// very first frame already renders in the correct language.
    /// </summary>
    private static void InitializeCulture()
    {
        try
        {
            string lang = Preferences.Default.Get(BookLoggerApp.Services.LanguageService.PrefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(lang))
            {
                string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                lang = string.Equals(iso, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
                Preferences.Default.Set(BookLoggerApp.Services.LanguageService.PrefKey, lang);
            }

            var culture = new CultureInfo(lang);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (Exception ex)
        {
            // Never fail startup on culture setup — fall back to the system default.
            System.Diagnostics.Debug.WriteLine($"InitializeCulture failed: {ex}");
        }
    }

    private static void ConfigurePlatformHandlers(MauiAppBuilder builder)
    {
#if ANDROID
        // Configure BlazorWebView to use custom WebChromeClient for camera permissions
        Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping(
            "CustomWebChromeClient",
            (handler, view) =>
            {
                if (handler.PlatformView is Android.Webkit.WebView webView)
                {
                    webView.SetWebChromeClient(new BookLoggerApp.Platforms.Android.CustomWebChromeClient());
                    System.Diagnostics.Debug.WriteLine("CustomWebChromeClient attached to BlazorWebView");
                }
            });
#endif
    }

    private static void ConfigureLogging(MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
#endif
        // Add Memory Cache for performance optimization
        builder.Services.AddMemoryCache();

#if ANDROID
        // Fan ILogger<T> messages into Firebase Crashlytics breadcrumbs (Information+).
        // The provider is resolved after Build() via a keyed services resolver so the
        // consent gate + crash service are already wired when logs start flowing.
        builder.Logging.Services.AddSingleton<ILoggerProvider>(sp =>
            new BookLoggerApp.Platforms.AndroidImpl.Analytics.CrashlyticsLoggerProvider(
                sp.GetRequiredService<ICrashReportingService>(),
                sp.GetRequiredService<IAnalyticsConsentGate>()));
#endif
    }

    private static void RegisterDatabase(MauiAppBuilder builder)
    {
        var dbPath = PlatformsDbPath.GetDatabasePath();

        // Perform migration check (XP-based recovery)
        DatabaseMigrationHelper.MigrateIfNecessary(dbPath);

        // Register DbContextFactory for creating DbContext instances on demand
        // This is the recommended approach for Blazor to avoid concurrency issues
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Also register DbContext as Transient for compatibility with existing code
        // Each injection gets a fresh instance from the factory
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        }, ServiceLifetime.Transient);
    }

    private static void RegisterRepositories(MauiAppBuilder builder)
    {
        // Register Unit of Work as Transient
        // UnitOfWork creates all repositories internally with the same DbContext instance
        // This ensures all repositories in a single operation share the same context
        builder.Services.AddTransient<IUnitOfWork, UnitOfWork>();

        // NOTE: Individual repositories are no longer registered here
        // All services should use IUnitOfWork instead of direct repository injection
    }

    private static void RegisterBusinessServices(MauiAppBuilder builder)
    {
        // Register File System Abstraction as Singleton (infrastructure layer)
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IFileSystem, BookLoggerApp.Infrastructure.Services.FileSystemAdapter>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IFileSaverService, BookLoggerApp.Services.FileSaverService>();
        
        // Register BackButton Service as Singleton
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IBackButtonService, BookLoggerApp.Infrastructure.Services.BackButtonService>();

        // Register Services as Transient (Recommended for Blazor/MAUI with EF Core to avoid DbContext tracking issues)
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IBookService, BookLoggerApp.Infrastructure.Services.BookService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IProgressService, BookLoggerApp.Infrastructure.Services.ProgressService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IGenreService, BookLoggerApp.Infrastructure.Services.GenreService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IQuoteService, BookLoggerApp.Infrastructure.Services.QuoteService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IAnnotationService, BookLoggerApp.Infrastructure.Services.AnnotationService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IGoalService, BookLoggerApp.Infrastructure.Services.GoalService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IPlantService, BookLoggerApp.Infrastructure.Services.PlantService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IStatsService, BookLoggerApp.Infrastructure.Services.StatsService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IAdvancedStatsService, BookLoggerApp.Infrastructure.Services.AdvancedStatsService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IShareCardService, BookLoggerApp.Infrastructure.Services.ShareCardService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IImageService, BookLoggerApp.Infrastructure.Services.ImageService>();
        builder.Services.AddSingleton<BookLoggerApp.Infrastructure.Services.AppSettingsProvider>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IAppSettingsProvider>(sp => sp.GetRequiredService<BookLoggerApp.Infrastructure.Services.AppSettingsProvider>());
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IEntitlementStore, BookLoggerApp.Infrastructure.Services.EntitlementStore>();
        builder.Services.AddSingleton<BookLoggerApp.Infrastructure.Services.EntitlementLapseHandler>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IEntitlementService, BookLoggerApp.Infrastructure.Services.EntitlementService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IFeatureGuard, BookLoggerApp.Infrastructure.Services.FeatureGuard>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IProductCatalog, BookLoggerApp.Infrastructure.Services.ProductCatalog>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IPaywallCoordinator, BookLoggerApp.Infrastructure.Services.PaywallCoordinator>();

        // Billing: real Android implementation on device, no-op on every other head.
#if ANDROID
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IBillingService, BookLoggerApp.Services.Billing.AndroidBillingService>();
#else
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IBillingService>(_ => BookLoggerApp.Infrastructure.Services.NoOpBillingService.Instance);
#endif
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IPromoCodeService, BookLoggerApp.Infrastructure.Services.PromoCodeService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IOnboardingService, BookLoggerApp.Infrastructure.Services.OnboardingService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IReviewPromptService, BookLoggerApp.Infrastructure.Services.ReviewPromptService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IAppVersionService, BookLoggerApp.Services.AppVersionService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IChangelogService, BookLoggerApp.Services.ChangelogService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IAppUpdateService, BookLoggerApp.Services.AppUpdateService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IProgressionService, BookLoggerApp.Infrastructure.Services.ProgressionService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IImportExportService, BookLoggerApp.Infrastructure.Services.ImportExportService>();
        builder.Services.AddHttpClient<BookLoggerApp.Core.Services.Abstractions.ILookupService, BookLoggerApp.Infrastructure.Services.LookupService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.INotificationService, BookLoggerApp.Services.NotificationService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IShelfService, BookLoggerApp.Infrastructure.Services.ShelfService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IDecorationService, BookLoggerApp.Infrastructure.Services.DecorationService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IWishlistService, BookLoggerApp.Infrastructure.Services.WishlistService>();

        // Database initializer service — used by the UI to retry a failed DB init
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IDatabaseInitializer, BookLoggerApp.Infrastructure.Services.DatabaseInitializer>();

        // Register timer state service as Singleton (must survive across component lifetimes)
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.ITimerStateService, BookLoggerApp.Services.TimerStateService>();

        // Register MAUI-specific services
        builder.Services.AddSingleton<BookLoggerApp.Services.IPermissionService, BookLoggerApp.Services.PermissionService>();
        builder.Services.AddSingleton<BookLoggerApp.Services.IScannerService, BookLoggerApp.Services.ScannerService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IShareService, BookLoggerApp.Services.ShareService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IFilePickerService, BookLoggerApp.Services.FilePickerService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IMigrationService, BookLoggerApp.Services.MigrationService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IAppRestartService, BookLoggerApp.Services.AppRestartService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.ILanguageService, BookLoggerApp.Services.LanguageService>();

        // Register Widget Update Service as Singleton (triggers Android widget refresh on data changes)
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IWidgetUpdateService, BookLoggerApp.Services.WidgetUpdateService>();

        RegisterAnalyticsServices(builder);
    }

    private static void RegisterAnalyticsServices(MauiAppBuilder builder)
    {
        // Consent gate is platform-agnostic — reads from IAppSettingsProvider.
        builder.Services.AddSingleton<IAnalyticsConsentGate, AnalyticsConsentGate>();

#if ANDROID
        // Native Firebase bindings live in Platforms/Android/Analytics.
        builder.Services.AddSingleton<IAnalyticsService, BookLoggerApp.Platforms.AndroidImpl.Analytics.FirebaseAnalyticsService>();
        builder.Services.AddSingleton<ICrashReportingService, BookLoggerApp.Platforms.AndroidImpl.Analytics.FirebaseCrashlyticsService>();
#else
        // Non-Android targets (tests, other MAUI heads) use no-op implementations.
        builder.Services.AddSingleton<IAnalyticsService>(_ => NoOpAnalyticsService.Instance);
        builder.Services.AddSingleton<ICrashReportingService>(_ => NoOpCrashReportingService.Instance);
#endif

        builder.Services.AddSingleton<UserPropertiesPublisher>();
    }

    private static void RegisterViewModels(MauiAppBuilder builder)
    {
        builder.Services.AddTransient<BookListViewModel>();
        builder.Services.AddTransient<BookDetailViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<BookshelfViewModel>();
        builder.Services.AddTransient<BookEditViewModel>();
        builder.Services.AddTransient<ReadingViewModel>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<StatsViewModel>();
        builder.Services.AddTransient<StatsTrendsViewModel>();
        builder.Services.AddTransient<StatsAnalysesViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<PlantShopViewModel>();
        builder.Services.AddTransient<DecorationShopViewModel>();
        builder.Services.AddTransient<UserProgressViewModel>();
        builder.Services.AddTransient<WishlistViewModel>();
        builder.Services.AddTransient<PaywallViewModel>();
        builder.Services.AddSingleton<AppStartupViewModel>();
    }

    private static void RegisterValidators(MauiAppBuilder builder)
    {
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IValidationService, BookLoggerApp.Infrastructure.Services.ValidationService>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.Book>, BookLoggerApp.Core.Validators.BookValidator>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.ReadingSession>, BookLoggerApp.Core.Validators.ReadingSessionValidator>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.ReadingGoal>, BookLoggerApp.Core.Validators.ReadingGoalValidator>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.UserPlant>, BookLoggerApp.Core.Validators.UserPlantValidator>();
    }

    private static void SetupGlobalExceptionHandler(MauiApp app)
    {
        // Global exception handler for unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            var logger = app.Services.GetService<ILogger<MauiApp>>();

            logger?.LogCritical(exception, "Unhandled exception occurred");

            try
            {
                app.Services.GetService<ICrashReportingService>()?.RecordFatal(exception);
            }
            catch (Exception reportEx)
            {
                System.Diagnostics.Debug.WriteLine($"RecordFatal(UnhandledException) failed: {reportEx}");
            }

            // Log user-friendly message for custom exceptions
            if (exception is BookLoggerException bookLoggerEx)
            {
                logger?.LogError("Application error: {Message}", bookLoggerEx.Message);

                // Specific handling for different exception types
                switch (bookLoggerEx)
                {
                    case EntityNotFoundException notFoundEx:
                        logger?.LogWarning("Entity not found: {EntityType} with ID {EntityId}",
                            notFoundEx.EntityType.Name, notFoundEx.EntityId);
                        break;

                    case ConcurrencyException concurrencyEx:
                        logger?.LogWarning("Concurrency conflict: {Message}", concurrencyEx.Message);
                        break;

                    case InsufficientFundsException fundsEx:
                        logger?.LogWarning("Insufficient funds: Required {Required}, Available {Available}",
                            fundsEx.Required, fundsEx.Available);
                        break;

                    case ValidationException validationEx:
                        logger?.LogWarning("Validation failed: {Errors}", string.Join(", ", validationEx.Errors));
                        break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"=== UNHANDLED EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Type: {exception.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Message: {exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {exception.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=========================");
        };

        // MAUI-specific unhandled exception handler
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            var logger = app.Services.GetService<ILogger<MauiApp>>();
            logger?.LogError(args.Exception, "Unobserved task exception");

            System.Diagnostics.Debug.WriteLine($"=== UNOBSERVED TASK EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Exception: {args.Exception}");
            System.Diagnostics.Debug.WriteLine("=================================");

            try
            {
                app.Services.GetService<ICrashReportingService>()?.RecordNonFatal(
                    args.Exception,
                    new Dictionary<string, string> { ["source"] = "unobserved_task" });
            }
            catch (Exception reportEx)
            {
                System.Diagnostics.Debug.WriteLine($"RecordNonFatal(UnobservedTask) failed: {reportEx}");
            }

            // Mark as observed to prevent app crash
            args.SetObserved();
        };
    }

    private static void InitializeDatabase(MauiApp app)
    {
        // Runs DbInitializer on a dedicated background thread rather than Task.Run.
        // On budget Android devices (reported on Samsung Galaxy A16) the ThreadPool
        // during cold start is busy with BlazorWebView, MAUI handlers and Android
        // activity setup, which can delay a Task.Run worker far enough to push
        // EF migrations past the UI timeout. A dedicated thread starts immediately
        // and doesn't compete for a ThreadPool slot.
        System.Diagnostics.Debug.WriteLine("Starting database initialization...");
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.AppendInitLog(
            "MauiProgram.InitializeDatabase: spawning dedicated DbInit thread");
        var thread = new Thread(() =>
        {
            BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.AppendInitLog(
                $"DbInit thread running (managed thread id={Environment.CurrentManagedThreadId})");
            try
            {
                var logger = app.Services.GetService<ILogger<AppDbContext>>();
                DbInitializer.InitializeAsync(app.Services, logger).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== EXCEPTION IN DATABASE INITIALIZATION ===");
                System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"Inner Message: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner Stack: {ex.InnerException.StackTrace}");
                }
                System.Diagnostics.Debug.WriteLine("=== END EXCEPTION ===");

                // Safety net: DbInitializer already calls MarkAsFailed in its own catch,
                // but if it throws before reaching that (e.g. service resolution fails),
                // the TCS would never fault and awaiters would hang until their timeout.
                // MarkAsFailed is idempotent, so calling it here is always safe.
                BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsFailed(ex);
            }
        })
        {
            IsBackground = true,
            Name = "DbInit"
        };
        thread.Start();
    }
}
