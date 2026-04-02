namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Notifies Android home screen widgets to refresh their displayed data.
/// No-op on non-Android platforms.
/// </summary>
public interface IWidgetUpdateService
{
    void NotifyDataChanged();
}
