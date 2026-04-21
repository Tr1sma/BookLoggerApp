using BookLoggerApp.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Infrastructure;

public class PlatformsDbPathTests
{
    [Fact]
    public void GetDatabasePath_DefaultFileName_ReturnsPathEndingWithBookLoggerDb()
    {
        var result = PlatformsDbPath.GetDatabasePath();

        result.Should().EndWith("booklogger.db3");
    }

    [Fact]
    public void GetDatabasePath_CustomFileName_UsesProvidedName()
    {
        var result = PlatformsDbPath.GetDatabasePath("custom.db3");

        result.Should().EndWith("custom.db3");
    }

    [Fact]
    public void GetDatabasePath_ReturnsAbsolutePath()
    {
        var result = PlatformsDbPath.GetDatabasePath();

        System.IO.Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void GetDatabasePath_FolderExistsAfterCall()
    {
        var result = PlatformsDbPath.GetDatabasePath();

        var folder = System.IO.Path.GetDirectoryName(result);
        System.IO.Directory.Exists(folder).Should().BeTrue();
    }
}
