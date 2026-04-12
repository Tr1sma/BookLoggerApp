using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

public sealed class ChangelogService : IChangelogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ChangelogRelease>? _allReleases;

    public async Task<IReadOnlyList<ChangelogRelease>> GetReleaseHistoryAsync(CancellationToken ct = default)
    {
        await EnsureParsedAsync(ct);
        return _allReleases!
            .Where(r => !string.Equals(r.Version, ChangelogParser.UnreleasedVersion, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<ChangelogRelease?> GetReleaseAsync(string version, CancellationToken ct = default)
    {
        var normalizedVersion = ChangelogParser.NormalizeVersion(version);
        var releases = await GetReleaseHistoryAsync(ct);

        return releases.FirstOrDefault(release =>
            string.Equals(release.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ChangelogRelease?> GetUnreleasedChangesAsync(CancellationToken ct = default)
    {
        await EnsureParsedAsync(ct);
        return _allReleases!.FirstOrDefault(r =>
            string.Equals(r.Version, ChangelogParser.UnreleasedVersion, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureParsedAsync(CancellationToken ct)
    {
        if (_allReleases != null)
        {
            return;
        }

        await _gate.WaitAsync(ct);

        try
        {
            if (_allReleases != null)
            {
                return;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync("CHANGELOG.md");
            using var reader = new StreamReader(stream);
            var markdown = await reader.ReadToEndAsync(ct);
            _allReleases = ChangelogParser.Parse(markdown);
        }
        finally
        {
            _gate.Release();
        }
    }
}
