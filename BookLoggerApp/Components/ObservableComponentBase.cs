using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace BookLoggerApp.Components;

/// <summary>
/// Base class for Blazor components that observe an <see cref="INotifyPropertyChanged"/>
/// source (typically a ViewModel). Subscribing pages call <see cref="Observe"/> once
/// during initialization; every subsequent PropertyChanged event triggers
/// <see cref="Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged"/> on the
/// Blazor dispatcher.
///
/// Fixes the class of "stuck state" bugs where a ViewModel property updates after
/// OnInitializedAsync has already completed (e.g. IsBusy flipping on a background
/// refresh) but the UI does not re-render because nothing told Blazor about it.
/// Blazor's ComponentBase does not auto-observe INotifyPropertyChanged sources.
/// </summary>
public abstract class ObservableComponentBase : ComponentBase, IDisposable
{
    private readonly List<INotifyPropertyChanged> _subscribed = new();
    private bool _disposed;

    /// <summary>
    /// Subscribes the component to the source's PropertyChanged event. Unsubscribes
    /// automatically on <see cref="Dispose"/>. Safe to call multiple times with
    /// different sources; each is tracked independently.
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
        // Marshal back onto Blazor's dispatcher — PropertyChanged may fire from
        // any thread (e.g. the DB-init worker), but StateHasChanged must run on
        // the renderer's sync context.
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
