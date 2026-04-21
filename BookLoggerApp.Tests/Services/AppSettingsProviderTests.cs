using FluentAssertions;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
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
        await UpsertSettingsAsync(s => s.Coins = coins);
    }

    private async Task UpsertSettingsAsync(Action<AppSettings> configure)
    {
        await using var context = TestDbContext.Create(_databaseName);
        var existing = await context.AppSettings.FirstOrDefaultAsync();
        if (existing != null)
        {
            configure(existing);
        }
        else
        {
            var settings = new AppSettings();
            configure(settings);
            context.AppSettings.Add(settings);
        }
        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Coverage-ergänzende Tests (GetSettings default creation + cache,
    // GetUserCoins/GetUserLevel/GetPlantsPurchased, SpendCoins path,
    // AddCoins path, InsufficientFunds, Increment, Update, Events,
    // RecalculateUserLevel, InvalidateCache, SetCachedSettings)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsAsync_EmptyDb_CreatesDefaultSettings()
    {
        var sut = CreateSut();

        var settings = await sut.GetSettingsAsync();

        settings.Should().NotBeNull();
        settings.Coins.Should().Be(100);
        settings.UserLevel.Should().Be(1);
        settings.TotalXp.Should().Be(0);
    }

    [Fact]
    public async Task GetSettingsAsync_CachedCall_ReturnsSameReference()
    {
        await SeedSettingsAsync(100);
        var sut = CreateSut();

        var first = await sut.GetSettingsAsync();
        var second = await sut.GetSettingsAsync();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetUserCoinsAsync_ReturnsCurrentCoins()
    {
        await SeedSettingsAsync(250);
        var sut = CreateSut();

        var coins = await sut.GetUserCoinsAsync();

        coins.Should().Be(250);
    }

    [Fact]
    public async Task GetUserLevelAsync_ReturnsCurrentLevel()
    {
        await UpsertSettingsAsync(s => { s.UserLevel = 7; s.Coins = 100; });
        var sut = CreateSut();

        var level = await sut.GetUserLevelAsync();

        level.Should().Be(7);
    }

    [Fact]
    public async Task GetPlantsPurchasedAsync_ReturnsCurrentValue()
    {
        await UpsertSettingsAsync(s => { s.PlantsPurchased = 3; s.Coins = 100; });
        var sut = CreateSut();

        var result = await sut.GetPlantsPurchasedAsync();

        result.Should().Be(3);
    }

    [Fact]
    public async Task AddCoinsAsync_IncreasesCoinsAndNotifiesProgression()
    {
        await SeedSettingsAsync(50);
        var sut = CreateSut();
        int progressionChangedCalls = 0;
        sut.ProgressionChanged += (_, _) => progressionChangedCalls++;

        await sut.AddCoinsAsync(25);

        (await sut.GetUserCoinsAsync()).Should().Be(75);
        progressionChangedCalls.Should().Be(1);
    }

    [Fact]
    public async Task SpendCoinsAsync_SufficientFunds_DeductsCoins()
    {
        await SeedSettingsAsync(100);
        var sut = CreateSut();

        await sut.SpendCoinsAsync(40);

        (await sut.GetUserCoinsAsync()).Should().Be(60);
    }

    [Fact]
    public async Task SpendCoinsAsync_InsufficientFunds_Throws()
    {
        await SeedSettingsAsync(10);
        var sut = CreateSut();

        Func<Task> act = async () => await sut.SpendCoinsAsync(50);

        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task IncrementPlantsPurchasedAsync_IncreasesCounter()
    {
        await UpsertSettingsAsync(s => { s.PlantsPurchased = 2; s.Coins = 100; });
        var sut = CreateSut();

        await sut.IncrementPlantsPurchasedAsync();

        (await sut.GetPlantsPurchasedAsync()).Should().Be(3);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PersistsAndRaisesEvents()
    {
        await SeedSettingsAsync(100);
        var sut = CreateSut();
        var settings = await sut.GetSettingsAsync();

        int progressionCalls = 0;
        int settingsCalls = 0;
        sut.ProgressionChanged += (_, _) => progressionCalls++;
        sut.SettingsChanged += (_, _) => settingsCalls++;

        settings.TotalXp += 500;
        settings.UserLevel = 2;
        await sut.UpdateSettingsAsync(settings);

        progressionCalls.Should().Be(1);
        settingsCalls.Should().Be(1);
        var reloaded = await sut.GetSettingsAsync();
        reloaded.TotalXp.Should().Be(500);
        reloaded.UserLevel.Should().Be(2);
    }

    [Fact]
    public async Task UpdateSettingsAsync_NonProgressionChange_OnlyRaisesSettingsChanged()
    {
        await SeedSettingsAsync(100);
        var sut = CreateSut();
        var settings = await sut.GetSettingsAsync();

        int progressionCalls = 0;
        int settingsCalls = 0;
        sut.ProgressionChanged += (_, _) => progressionCalls++;
        sut.SettingsChanged += (_, _) => settingsCalls++;

        settings.Theme = "Dark";
        await sut.UpdateSettingsAsync(settings);

        progressionCalls.Should().Be(0);
        settingsCalls.Should().Be(1);
    }

    [Fact]
    public async Task UpdateSettingsAsync_SubscriberThrows_OtherSubscribersStillRun()
    {
        await SeedSettingsAsync(100);
        var sut = CreateSut();
        var settings = await sut.GetSettingsAsync();

        int reachedSecond = 0;
        sut.SettingsChanged += (_, _) => throw new InvalidOperationException("boom");
        sut.SettingsChanged += (_, _) => reachedSecond++;

        Func<Task> act = async () => await sut.UpdateSettingsAsync(settings);

        await act.Should().NotThrowAsync();
        reachedSecond.Should().Be(1);
    }

    [Fact]
    public async Task RecalculateUserLevelAsync_CorrectsLevelFromXp()
    {
        await UpsertSettingsAsync(s => { s.UserLevel = 1; s.TotalXp = 50_000; s.Coins = 100; });
        int expectedLevel = XpCalculator.CalculateLevelFromXp(50_000);
        var sut = CreateSut();

        await sut.RecalculateUserLevelAsync();

        (await sut.GetUserLevelAsync()).Should().Be(expectedLevel);
    }

    [Fact]
    public async Task RecalculateUserLevelAsync_LevelCorrect_IsNoOp()
    {
        await UpsertSettingsAsync(s => { s.UserLevel = 1; s.TotalXp = 0; s.Coins = 100; });
        var sut = CreateSut();
        int progressionCalls = 0;
        sut.ProgressionChanged += (_, _) => progressionCalls++;

        await sut.RecalculateUserLevelAsync();

        progressionCalls.Should().Be(0);
    }

    [Fact]
    public void InvalidateCache_NotifyFalse_DoesNotRaiseEvent()
    {
        var sut = CreateSut();
        int calls = 0;
        sut.ProgressionChanged += (_, _) => calls++;

        sut.InvalidateCache(notifyProgressionChanged: false);

        calls.Should().Be(0);
    }

    [Fact]
    public void InvalidateCache_DefaultOverload_RaisesEvent()
    {
        var sut = CreateSut();
        int calls = 0;
        sut.ProgressionChanged += (_, _) => calls++;

        sut.InvalidateCache();

        calls.Should().Be(1);
    }

    [Fact]
    public async Task SetCachedSettings_SubsequentGet_ReturnsSameInstance()
    {
        var sut = CreateSut();
        var manual = new AppSettings { Coins = 999, UserLevel = 10 };

        sut.SetCachedSettings(manual);

        var result = await sut.GetSettingsAsync();
        result.Should().BeSameAs(manual);
    }
}
