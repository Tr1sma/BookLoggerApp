using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAppUpdateService
{
    event EventHandler<AppUpdateState>? StateChanged;

    Task<AppUpdateState> GetStateAsync(CancellationToken ct = default);
    Task<bool> StartFlexibleUpdateAsync(CancellationToken ct = default);
    Task<bool> CompleteUpdateAsync(CancellationToken ct = default);
}
