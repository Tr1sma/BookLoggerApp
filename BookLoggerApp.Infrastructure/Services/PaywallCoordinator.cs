using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;

namespace BookLoggerApp.Infrastructure.Services;

public class PaywallCoordinator : IPaywallCoordinator
{
    private readonly IAnalyticsService _analytics;

    public PaywallCoordinator(IAnalyticsService? analytics = null)
    {
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    public event EventHandler? VisibilityChanged;

    public bool IsVisible { get; private set; }

    public FeatureKey? TriggerFeature { get; private set; }

    public Task ShowAsync(FeatureKey? trigger = null)
    {
        TriggerFeature = trigger;
        IsVisible = true;

        _analytics.LogEvent(AnalyticsEventNames.PaywallShown, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.TriggerFeature, trigger?.ToString() ?? "manual")
            .BuildMutable());

        Raise();
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        string? trigger = TriggerFeature?.ToString();
        IsVisible = false;
        TriggerFeature = null;

        _analytics.LogEvent(AnalyticsEventNames.PaywallDismissed, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.TriggerFeature, trigger ?? "manual")
            .BuildMutable());

        Raise();
        return Task.CompletedTask;
    }

    private void Raise()
    {
        EventHandler? handlers = VisibilityChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler)handler).Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PaywallCoordinator handler threw: {ex}");
            }
        }
    }
}
