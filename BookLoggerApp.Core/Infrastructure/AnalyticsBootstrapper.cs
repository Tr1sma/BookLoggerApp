using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;

namespace BookLoggerApp.Core.Infrastructure;

/// <summary>
/// Wires the ambient <see cref="ViewModelBase.CrashReporter"/> to the resolved
/// <see cref="ICrashReportingService"/> after the DI container is built. Only the crash
/// reporter is wired here; analytics is configured elsewhere.
/// </summary>
public static class AnalyticsBootstrapper
{
    public static void InstallCrashReporter(ICrashReportingService crashReporter)
    {
        if (crashReporter is null) throw new ArgumentNullException(nameof(crashReporter));
        ViewModelBase.CrashReporter = crashReporter;
    }
}
