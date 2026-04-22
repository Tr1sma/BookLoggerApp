using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Analytics;

public class AnalyticsConsentGateTests : IDisposable
{
    public AnalyticsConsentGateTests()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsInitialized();
    }

    public void Dispose()
    {
        DatabaseInitializationHelper.ResetForTests();
    }

    [Fact]
    public async Task InitializeAsync_reads_consent_from_settings()
    {
        var provider = new FakeSettingsProvider(analytics: false, crash: true);
        using var gate = new AnalyticsConsentGate(provider);

        await gate.InitializeAsync();

        gate.IsInitialized.Should().BeTrue();
        gate.AnalyticsAllowed.Should().BeFalse();
        gate.CrashAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ConsentChanged_fires_on_settings_update_when_values_differ()
    {
        var provider = new FakeSettingsProvider(analytics: true, crash: true);
        using var gate = new AnalyticsConsentGate(provider);
        await gate.InitializeAsync();

        var fireCount = 0;
        gate.ConsentChanged += (_, _) => Interlocked.Increment(ref fireCount);

        provider.Update(analytics: false, crash: true);

        await WaitUntil(() => fireCount >= 1, TimeSpan.FromSeconds(2));

        fireCount.Should().Be(1);
        gate.AnalyticsAllowed.Should().BeFalse();
        gate.CrashAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ConsentChanged_does_not_fire_on_identical_settings_update()
    {
        var provider = new FakeSettingsProvider(analytics: true, crash: true);
        using var gate = new AnalyticsConsentGate(provider);
        await gate.InitializeAsync();

        var fireCount = 0;
        gate.ConsentChanged += (_, _) => Interlocked.Increment(ref fireCount);

        provider.Update(analytics: true, crash: true);

        await Task.Delay(100);
        fireCount.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_falls_back_to_allowed_on_db_init_timeout()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsFailed(new InvalidOperationException("db down"));

        var provider = new FakeSettingsProvider(analytics: false, crash: false);
        using var gate = new AnalyticsConsentGate(provider);

        await gate.InitializeAsync();

        gate.IsInitialized.Should().BeTrue();
        gate.AnalyticsAllowed.Should().BeTrue();
        gate.CrashAllowed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_unsubscribes_from_SettingsChanged()
    {
        var provider = new FakeSettingsProvider(analytics: true, crash: true);
        var gate = new AnalyticsConsentGate(provider);
        gate.Dispose();

        provider.SubscriberCount.Should().Be(0);
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout) return;
            await Task.Delay(20);
        }
    }

    private sealed class FakeSettingsProvider : IAppSettingsProvider
    {
        private AppSettings _settings;

        public FakeSettingsProvider(bool analytics, bool crash)
        {
            _settings = new AppSettings
            {
                AnalyticsEnabled = analytics,
                CrashReportingEnabled = crash
            };
        }

        public event EventHandler? ProgressionChanged;
        public event EventHandler? SettingsChanged;

        public int SubscriberCount => SettingsChanged?.GetInvocationList().Length ?? 0;

        public void Update(bool analytics, bool crash)
        {
            _settings = new AppSettings
            {
                AnalyticsEnabled = analytics,
                CrashReportingEnabled = crash
            };
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
            => Task.FromResult(_settings);

        public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
        {
            _settings = settings;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task<int> GetUserCoinsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetUserLevelAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task SpendCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task IncrementPlantsPurchasedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void InvalidateCache() { }
        public void InvalidateCache(bool notifyProgressionChanged) { }

        private void RaiseProgressionChanged() => ProgressionChanged?.Invoke(this, EventArgs.Empty);
    }
}
