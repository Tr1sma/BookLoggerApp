using BookLoggerApp.Core.Infrastructure;
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
            lock (_lock)
            {
                _analyticsAllowed = true;
                _crashAllowed = true;
                _initialized = true;
            }
            RaiseConsentChanged();
            return;
        }

        var settings = await _settingsProvider.GetSettingsAsync(ct).ConfigureAwait(false);
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

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await _settingsProvider.GetSettingsAsync().ConfigureAwait(false);
                bool changed;
                lock (_lock)
                {
                    var newAnalytics = settings.AnalyticsEnabled;
                    var newCrash = settings.CrashReportingEnabled;
                    changed = newAnalytics != _analyticsAllowed || newCrash != _crashAllowed;
                    _analyticsAllowed = newAnalytics;
                    _crashAllowed = newCrash;
                }
                if (changed) RaiseConsentChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AnalyticsConsentGate.OnSettingsChanged failed: {ex}");
            }
        });
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
