using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;

namespace BookLoggerApp.Core.Infrastructure;

/// <summary>
/// Wires the ambient <see cref="ViewModelBase.CrashReporter"/> to the resolved
/// <see cref="ICrashReportingService"/> once the DI container is built.
/// </summary>
public static class AnalyticsBootstrapper
{
    public static void Install(ICrashReportingService crashReporter)
    {
        if (crashReporter is null) throw new ArgumentNullException(nameof(crashReporter));
        ViewModelBase.CrashReporter = crashReporter;
    }
}
