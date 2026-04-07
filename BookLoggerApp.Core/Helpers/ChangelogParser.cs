using System.Text.RegularExpressions;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

public static class ChangelogParser
{
    private static readonly Regex ReleaseHeaderRegex = new(
        @"^## \[(?<version>[^\]]+)\](?: - (?<date>.+))?$",
        RegexOptions.Compiled);

    public static IReadOnlyList<ChangelogRelease> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<ChangelogRelease>();
        }

        var releases = new List<ChangelogRelease>();
        ChangelogReleaseBuilder? currentRelease = null;
        ChangelogSectionBuilder? currentSection = null;

        foreach (var rawLine in NormalizeLineEndings(markdown).Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (TryParseReleaseHeader(line, out var version, out var displayVersion, out var releaseDate))
            {
                if (currentRelease != null)
                {
                    releases.Add(currentRelease.Build());
                }

                currentSection = null;

                if (string.Equals(displayVersion, "Unveröffentlicht", StringComparison.OrdinalIgnoreCase))
                {
                    currentRelease = null;
                    continue;
                }

                currentRelease = new ChangelogReleaseBuilder(version, displayVersion, releaseDate);
                continue;
            }

            if (currentRelease == null)
            {
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                currentSection = currentRelease.AddSection(line["### ".Length..].Trim());
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) && currentSection != null)
            {
                currentSection.Entries.Add(line[2..].Trim());
            }
        }

        if (currentRelease != null)
        {
            releases.Add(currentRelease.Build());
        }

        return releases;
    }

    public static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = version.Trim();
        if (normalized.StartsWith("V", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return normalized.Trim();
    }

    private static bool TryParseReleaseHeader(
        string line,
        out string normalizedVersion,
        out string displayVersion,
        out string? releaseDate)
    {
        normalizedVersion = string.Empty;
        displayVersion = string.Empty;
        releaseDate = null;

        var match = ReleaseHeaderRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        displayVersion = match.Groups["version"].Value.Trim();
        normalizedVersion = NormalizeVersion(displayVersion);
        releaseDate = match.Groups["date"].Success
            ? match.Groups["date"].Value.Trim()
            : null;

        return true;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private sealed class ChangelogReleaseBuilder
    {
        private readonly List<ChangelogSectionBuilder> _sections = new();

        public ChangelogReleaseBuilder(string version, string displayVersion, string? releaseDate)
        {
            Version = version;
            DisplayVersion = displayVersion;
            ReleaseDate = releaseDate;
        }

        public string Version { get; }
        public string DisplayVersion { get; }
        public string? ReleaseDate { get; }

        public ChangelogSectionBuilder AddSection(string title)
        {
            var section = new ChangelogSectionBuilder(title);
            _sections.Add(section);
            return section;
        }

        public ChangelogRelease Build()
        {
            return new ChangelogRelease
            {
                Version = Version,
                DisplayVersion = DisplayVersion,
                ReleaseDate = ReleaseDate,
                Sections = _sections
                    .Where(section => section.Entries.Count > 0)
                    .Select(section => section.Build())
                    .ToArray()
            };
        }
    }

    private sealed class ChangelogSectionBuilder
    {
        public ChangelogSectionBuilder(string title)
        {
            Title = title;
        }

        public string Title { get; }
        public List<string> Entries { get; } = new();

        public ChangelogSection Build()
        {
            return new ChangelogSection
            {
                Title = Title,
                Entries = Entries.ToArray()
            };
        }
    }
}
