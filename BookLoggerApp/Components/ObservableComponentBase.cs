using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace BookLoggerApp.Components;

/// <summary>
/// Bridges INotifyPropertyChanged sources (ViewModels) to Blazor re-renders.
/// Fixes "stuck state" where ViewModel updates after OnInitializedAsync don't re-render
/// because Blazor's ComponentBase doesn't auto-observe INotifyPropertyChanged.
/// </summary>
public abstract class ObservableComponentBase : ComponentBase, IDisposable
{
    private readonly List<INotifyPropertyChanged> _subscribed = new();
    private bool _disposed;

    /// <summary>Subscribes to PropertyChanged; auto-unsubscribes on Dispose.</summary>
    protected void Observe(INotifyPropertyChanged source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_disposed) return;
        source.PropertyChanged += OnObservedPropertyChanged;
        _subscribed.Add(source);
    }

    private void OnObservedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // PropertyChanged may fire off-thread; marshal to Blazor's renderer context.
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
