using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.Services.Analytics;

public sealed class AnalyticsConsentGate : IAnalyticsConsentGate, IDisposable
{
    private static readonly TimeSpan DbInitTimeout = TimeSpan.FromSeconds(45);

    private readonly IAppSettingsProvider _settingsProvider;
    private readonly object _lock = new();
    private bool _analyticsAllowed;
    private bool _crashAllowed;
    private bool _initialized;
    private bool _disposed;

    public AnalyticsConsentGate(IAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _settingsProvider.SettingsChanged += OnSettingsChanged;
    }

    public bool AnalyticsAllowed
    {
        get { lock (_lock) return _analyticsAllowed; }
    }

    public bool CrashAllowed
    {
        get { lock (_lock) return _crashAllowed; }
    }

    public bool IsInitialized
    {
        get { lock (_lock) return _initialized; }
    }

    public event EventHandler? ConsentChanged;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await DatabaseInitializationHelper.EnsureInitializedAsync(DbInitTimeout).ConfigureAwait(false);
        }
        catch
        {
            // Fail CLOSED: when the persisted consent choice cannot be confirmed
            // (DB init timed out or threw), default analytics + crash reporting to
            // OFF rather than ON. Honoring an opt-out under DB failure outweighs
            // losing telemetry for a degraded session. See code review SEC-02.
            lock (_lock)
            {
                _analyticsAllowed = false;
                _crashAllowed = false;
                _initialized = true;
            }
            RaiseConsentChanged();
            return;
        }

        var settings = await _settingsProvider.GetSettingsAsync(ct).ConfigureAwait(false);
        ApplyConsentAndNotify(settings);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await _settingsProvider.GetSettingsAsync().ConfigureAwait(false);
                ApplyConsentAndNotify(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AnalyticsConsentGate.OnSettingsChanged failed: {ex}");
            }
        });
    }

    /// <summary>
    /// Single consent-apply path shared by initial load and live settings changes. Compares the
    /// new values against the last-applied ones, marks the gate initialized, and fires
    /// <see cref="ConsentChanged"/> — including on the very first application (so a settings change
    /// that lands before <see cref="InitializeAsync"/> still notifies subscribers).
    /// </summary>
    private void ApplyConsentAndNotify(AppSettings settings)
    {
        bool changed;
        lock (_lock)
        {
            var newAnalytics = settings.AnalyticsEnabled;
            var newCrash = settings.CrashReportingEnabled;
            changed = !_initialized
                      || newAnalytics != _analyticsAllowed
                      || newCrash != _crashAllowed;
            _analyticsAllowed = newAnalytics;
            _crashAllowed = newCrash;
            _initialized = true;
        }
        if (changed) RaiseConsentChanged();
    }

    private void RaiseConsentChanged()
    {
        try
        {
            ConsentChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ConsentChanged handler threw: {ex}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsProvider.SettingsChanged -= OnSettingsChanged;
    }
}
