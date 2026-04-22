namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAnalyticsConsentGate
{
    bool AnalyticsAllowed { get; }

    bool CrashAllowed { get; }

    bool IsInitialized { get; }

    event EventHandler? ConsentChanged;

    Task InitializeAsync(CancellationToken ct = default);
}
