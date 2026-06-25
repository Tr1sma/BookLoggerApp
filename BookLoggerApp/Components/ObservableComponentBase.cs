using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace BookLoggerApp.Components;

/// <summary>
/// Base class for Blazor components observing an <see cref="INotifyPropertyChanged"/> source
/// (typically a ViewModel); each PropertyChanged event triggers StateHasChanged on the dispatcher.
/// Blazor's ComponentBase does not auto-observe INotifyPropertyChanged, so late ViewModel updates
/// (e.g. IsBusy on a background refresh) would otherwise not re-render.
/// </summary>
public abstract class ObservableComponentBase : ComponentBase, IDisposable
{
    private readonly List<INotifyPropertyChanged> _subscribed = new();
    private bool _disposed;

    /// <summary>
    /// Subscribes to the source's PropertyChanged event; auto-unsubscribed on <see cref="Dispose"/>.
    /// Safe to call with multiple sources, each tracked independently.
    /// </summary>
    protected void Observe(INotifyPropertyChanged source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_disposed) return;
        source.PropertyChanged += OnObservedPropertyChanged;
        _subscribed.Add(source);
    }

    private void OnObservedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // PropertyChanged may fire off-thread; StateHasChanged must run on Blazor's dispatcher.
        _ = InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var src in _subscribed)
        {
            src.PropertyChanged -= OnObservedPropertyChanged;
        }
        _subscribed.Clear();

        GC.SuppressFinalize(this);
    }
}
