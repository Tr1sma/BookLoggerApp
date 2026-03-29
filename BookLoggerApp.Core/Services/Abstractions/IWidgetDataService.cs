using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IWidgetDataService
{
    Task<WidgetData> GetWidgetDataAsync(CancellationToken ct = default);
}
