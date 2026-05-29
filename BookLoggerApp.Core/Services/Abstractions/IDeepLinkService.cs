namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Bridges native deep-link intents (e.g. tapping the reading-timer notification) into
/// the Blazor router. The native layer calls <see cref="RequestNavigation"/>; a root
/// Blazor component subscribes to <see cref="NavigationRequested"/> and navigates.
/// </summary>
public interface IDeepLinkService
{
    /// <summary>
    /// Raised when navigation to a relative route is requested (e.g. "/books/{guid}").
    /// </summary>
    event Action<string>? NavigationRequested;

    /// <summary>
    /// Requests navigation to a relative route. If no subscriber is attached yet
    /// (cold start before the router is ready), the route is buffered and replayed
    /// to the next subscriber.
    /// </summary>
    void RequestNavigation(string route);
}
