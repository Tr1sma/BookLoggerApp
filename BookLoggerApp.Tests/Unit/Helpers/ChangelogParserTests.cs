using BookLoggerApp.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class ChangelogParserTests
{
    [Fact]
    public void Parse_ShouldIncludeUnreleased_WithSpecialVersion()
    {
        const string markdown = """
            # Changelog

            ## [Unveröffentlicht]

            ### Hinzugefügt

            - Noch nicht veröffentlicht

            ## [0.8.0] - 2026-04-07

            ### Hinzugefügt

            - Neue Funktion
            """;

        var releases = ChangelogParser.Parse(markdown);

        releases.Should().HaveCount(2);
        releases[0].Version.Should().Be(ChangelogParser.UnreleasedVersion);
        releases[0].DisplayVersion.Should().Be("Unveröffentlicht");
        releases[0].Sections.Should().ContainSingle();
        releases[0].Sections[0].Entries.Should().ContainSingle("Noch nicht veröffentlicht");
        releases[1].Version.Should().Be("0.8.0");
        releases[1].Sections.Should().ContainSingle();
        releases[1].Sections[0].Entries.Should().ContainSingle("Neue Funktion");
    }

    [Fact]
    public void Parse_ShouldBuildEmptyUnreleasedEntry_WhenNoSectionsExist()
    {
        const string markdown = """
            ## [Unveröffentlicht]

            ## [0.8.0] - 2026-04-07

            ### Hinzugefügt

            - Neue Funktion
            """;

        var releases = ChangelogParser.Parse(markdown);

        releases.Should().HaveCount(2);
        releases[0].Version.Should().Be(ChangelogParser.UnreleasedVersion);
        releases[0].Sections.Should().BeEmpty();
        releases[1].Version.Should().Be("0.8.0");
    }

    [Fact]
    public void Parse_ShouldMatchVersions_WithOrWithoutVPrefix()
    {
        const string markdown = """
            ## [V0.7.5] - 2026-04-02

            ### Geändert

            - Review-Dialog vereinfacht
            """;

        var releases = ChangelogParser.Parse(markdown);

        releases.Should().ContainSingle();
        releases[0].Version.Should().Be("0.7.5");
        ChangelogParser.NormalizeVersion("V0.7.5").Should().Be("0.7.5");
        ChangelogParser.NormalizeVersion("0.7.5").Should().Be("0.7.5");
    }

    [Fact]
    public void Parse_ShouldKeepReleaseOrder_AndSectionEntries()
    {
        const string markdown = """
            ## [0.8.0] - 2026-04-07

            ### Hinzugefügt

            - Erste Neuerung
            - Zweite Neuerung

            ## [0.7.6] - 2026-04-02

            ### Behoben

            - Ein Fix
            """;

        var releases = ChangelogParser.Parse(markdown);

        releases.Select(release => release.Version).Should().ContainInOrder("0.8.0", "0.7.6");
        releases[0].Sections[0].Entries.Should().ContainInOrder("Erste Neuerung", "Zweite Neuerung");
        releases[1].Sections[0].Title.Should().Be("Behoben");
    }
}
