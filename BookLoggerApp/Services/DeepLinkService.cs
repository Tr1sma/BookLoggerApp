using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>
/// Relays native deep-link intents (e.g. tapping the reading-timer notification) into
/// the Blazor router. Buffers the last requested route so a request arriving before the
/// router subscribes (cold start) is replayed to the next subscriber.
/// </summary>
public class DeepLinkService : IDeepLinkService
{
    private readonly object _lock = new();
    private string? _pendingRoute;
    private Action<string>? _handler;

    public event Action<string>? NavigationRequested
    {
        add
        {
            string? replay = null;
            lock (_lock)
            {
                _handler += value;
                if (_pendingRoute is not null && value is not null)
                {
                    replay = _pendingRoute;
                    _pendingRoute = null;
                }
            }

            if (replay is not null)
            {
                value!(replay);
            }
        }
        remove
        {
            lock (_lock)
            {
                _handler -= value;
            }
        }
    }

    public void RequestNavigation(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;

        Action<string>? handler;
        lock (_lock)
        {
            handler = _handler;
            if (handler is null)
            {
                // No subscriber yet (cold start) — buffer and replay on next subscribe.
                _pendingRoute = route;
                return;
            }
        }

        handler(route);
    }
}
