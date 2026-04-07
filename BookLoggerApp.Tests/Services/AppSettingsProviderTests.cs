using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public sealed class AppSettingsProviderTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public void Dispose()
    {
        using var context = TestDbContext.Create(_databaseName);
        context.Database.EnsureDeleted();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SpendCoinsAsync_WhenAmountIsZeroOrNegative_ShouldThrowArgumentOutOfRangeException(int amount)
    {
        // Arrange
        await SeedSettingsAsync(100);
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.SpendCoinsAsync(amount);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        exception.Which.ParamName.Should().Be("amount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddCoinsAsync_WhenAmountIsZeroOrNegative_ShouldThrowArgumentOutOfRangeException(int amount)
    {
        // Arrange
        await SeedSettingsAsync(100);
        var sut = CreateSut();

        // Act
        Func<Task> act = async () => await sut.AddCoinsAsync(amount);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        exception.Which.ParamName.Should().Be("amount");
    }

    private AppSettingsProvider CreateSut()
    {
        var contextFactory = new TestDbContextFactory(_databaseName);
        return new AppSettingsProvider(contextFactory);
    }

    private async Task SeedSettingsAsync(int coins)
    {
        await using var context = TestDbContext.Create(_databaseName);
        context.AppSettings.Add(new AppSettings
        {
            Coins = coins
        });
        await context.SaveChangesAsync();
    }
}
