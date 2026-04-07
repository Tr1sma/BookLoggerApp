using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IChangelogService
{
    Task<IReadOnlyList<ChangelogRelease>> GetReleaseHistoryAsync(CancellationToken ct = default);
    Task<ChangelogRelease?> GetReleaseAsync(string version, CancellationToken ct = default);
}
