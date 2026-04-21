#if DEBUG
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// AppDbContextFactory is conditionally compiled only in Debug builds
/// (it's a design-time factory for EF Core migrations — see BookLoggerApp.Infrastructure.csproj).
/// These tests are therefore only included in Debug builds.
/// </summary>
public class AppDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsAppDbContextInstance()
    {
        var factory = new BookLoggerApp.Infrastructure.Data.AppDbContextFactory();

        using var context = factory.CreateDbContext(Array.Empty<string>());

        context.Should().NotBeNull();
        context.Should().BeOfType<BookLoggerApp.Infrastructure.Data.AppDbContext>();
    }

    [Fact]
    public void CreateDbContext_WithArguments_StillProducesContext()
    {
        var factory = new BookLoggerApp.Infrastructure.Data.AppDbContextFactory();

        using var context = factory.CreateDbContext(new[] { "arg1", "arg2" });

        context.Should().NotBeNull();
    }
}
#endif
