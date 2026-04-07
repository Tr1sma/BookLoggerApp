using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

public sealed class ChangelogService : IChangelogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ChangelogRelease>? _cachedReleases;

    public async Task<IReadOnlyList<ChangelogRelease>> GetReleaseHistoryAsync(CancellationToken ct = default)
    {
        if (_cachedReleases != null)
        {
            return _cachedReleases;
        }

        await _gate.WaitAsync(ct);

        try
        {
            if (_cachedReleases != null)
            {
                return _cachedReleases;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync("CHANGELOG.md");
            using var reader = new StreamReader(stream);
            var markdown = await reader.ReadToEndAsync(ct);
            _cachedReleases = ChangelogParser.Parse(markdown);
            return _cachedReleases;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChangelogRelease?> GetReleaseAsync(string version, CancellationToken ct = default)
    {
        var normalizedVersion = ChangelogParser.NormalizeVersion(version);
        var releases = await GetReleaseHistoryAsync(ct);

        return releases.FirstOrDefault(release =>
            string.Equals(release.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase));
    }
}
