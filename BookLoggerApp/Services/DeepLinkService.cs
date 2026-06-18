using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>
/// Relays deep-link intents into the Blazor router. Buffers cold-start requests
/// and replays them to the first subscriber.
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
                // Buffer for cold-start replay.
                _pendingRoute = route;
                return;
            }
        }

        handler(route);
    }
}
