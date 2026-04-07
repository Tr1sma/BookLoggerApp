namespace BookLoggerApp.Core.Models;

public sealed class ChangelogRelease
{
    public string Version { get; init; } = string.Empty;
    public string DisplayVersion { get; init; } = string.Empty;
    public string? ReleaseDate { get; init; }
    public IReadOnlyList<ChangelogSection> Sections { get; init; } = Array.Empty<ChangelogSection>();
}

public sealed class ChangelogSection
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Entries { get; init; } = Array.Empty<string>();
}
