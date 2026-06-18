using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Validators;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// CODE_REVIEW BUG-05: the four FluentValidation validators were registered but never invoked
/// on any production write path, so garbage data (empty titles, zero/over-limit targets,
/// invalid sessions/plants) reached the DB unchecked. The services now resolve
/// <see cref="IValidationService"/> (optional, auto-injected in production / null in legacy
/// unit tests) and call ValidateAndThrowAsync at the start of their write methods. These tests
/// wire a real ValidationService with the four validators and assert invalid entities are
/// rejected by the service — not just by the validator in isolation.
/// </summary>
public class ServiceValidationTests : IDisposable
{
    private readonly DbContextTestHelper _dbHelper;

    public ServiceValidationTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();
    }

    public void Dispose() => _dbHelper.Dispose();

    private static IValidationService CreateValidationService()
    {
        var services = new ServiceCollection();
        services.AddTransient<IValidator<Book>, BookValidator>();
        services.AddTransient<IValidator<ReadingGoal>, ReadingGoalValidator>();
        services.AddTransient<IValidator<ReadingSession>, ReadingSessionValidator>();
        services.AddTransient<IValidator<UserPlant>, UserPlantValidator>();
        return new ValidationService(services.BuildServiceProvider());
    }

    private BookService CreateBookService() => new(
        new UnitOfWork(_dbHelper.Context),
        Substitute.For<IProgressionService>(),
        Substitute.For<IPlantService>(),
        Substitute.For<IGoalService>(),
        NullLogger<BookService>.Instance,
        analytics: null,
        featureGuard: null,
        validationService: CreateValidationService());

    [Fact]
    public async Task BookService_AddAsync_InvalidBook_ThrowsAndPersistsNothing()
    {
        var service = CreateBookService();
        var invalid = new Book { Title = "", Author = "" }; // empty title + author

        Func<Task> act = () => service.AddAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>();
        (await _dbHelper.Context.Books.CountAsync()).Should().Be(0, "validation must reject before persistence");
    }

    [Fact]
    public async Task BookService_AddAsync_ValidBook_Succeeds()
    {
        var service = CreateBookService();
        var valid = new Book { Title = "Valid Title", Author = "Valid Author" };

        var result = await service.AddAsync(valid);

        result.Should().NotBeNull();
        (await _dbHelper.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task BookService_SaveBookWithRelationsAsync_InvalidBook_ThrowsAndPersistsNothing()
    {
        var service = CreateBookService();
        var invalid = new Book { Title = "", Author = "Author" }; // empty title

        Func<Task> act = () => service.SaveBookWithRelationsAsync(
            invalid,
            Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());

        await act.Should().ThrowAsync<ValidationException>();
        (await _dbHelper.Context.Books.CountAsync()).Should().Be(0, "the load-bearing BookEdit save path must validate too");
    }

    [Fact]
    public async Task GoalService_AddAsync_InvalidGoal_Throws()
    {
        var service = new GoalService(
            new UnitOfWork(_dbHelper.Context),
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        var invalid = new ReadingGoal
        {
            Title = "", // empty
            Type = GoalType.Books,
            Target = 0, // must be > 0
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        Func<Task> act = () => service.AddAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>();
        (await _dbHelper.Context.ReadingGoals.CountAsync()).Should().Be(0, "validation must reject before persistence");
    }

    [Fact]
    public async Task GoalService_AddAsync_ValidGoal_Succeeds()
    {
        var service = new GoalService(
            new UnitOfWork(_dbHelper.Context),
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        var valid = new ReadingGoal
        {
            Title = "Read 12 books",
            Type = GoalType.Books,
            Target = 12,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(300)
        };

        await service.AddAsync(valid);

        (await _dbHelper.Context.ReadingGoals.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GoalService_UpdateAsync_InvalidGoal_Throws()
    {
        // Seed a valid goal directly, then attempt to update it to an invalid state.
        var goal = new ReadingGoal
        {
            Id = Guid.NewGuid(), Title = "Original", Type = GoalType.Books, Target = 5,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        _dbHelper.Context.ReadingGoals.Add(goal);
        await _dbHelper.Context.SaveChangesAsync();
        _dbHelper.Context.ChangeTracker.Clear();

        var service = new GoalService(
            new UnitOfWork(_dbHelper.Context),
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        goal.Title = ""; // now invalid

        Func<Task> act = () => service.UpdateAsync(goal);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ProgressService_AddSessionAsync_InvalidSession_Throws()
    {
        var service = new ProgressService(
            new UnitOfWork(_dbHelper.Context),
            Substitute.For<IProgressionService>(),
            Substitute.For<IPlantService>(),
            Substitute.For<IBookService>(),
            Substitute.For<IGoalService>(),
            Substitute.For<IDecorationService>(),
            Substitute.For<IAppSettingsProvider>(),
            analytics: null,
            validationService: CreateValidationService());

        // Minutes must be > 0 per ReadingSessionValidator.
        var invalid = new ReadingSession { BookId = Guid.NewGuid(), Minutes = 0, StartedAt = DateTime.UtcNow };

        Func<Task> act = () => service.AddSessionAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>();
        (await _dbHelper.Context.ReadingSessions.CountAsync())
            .Should().Be(0, "validation must reject before any session is persisted or XP/coin side-effects run");
    }

    [Fact]
    public async Task PlantService_AddAsync_InvalidPlant_Throws()
    {
        var cache = new ServiceCollection().AddMemoryCache().BuildServiceProvider()
            .GetRequiredService<IMemoryCache>();
        var service = new PlantService(
            new UnitOfWork(_dbHelper.Context),
            Substitute.For<IAppSettingsProvider>(),
            Substitute.For<IDecorationService>(),
            cache,
            NullLogger<PlantService>.Instance,
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        // Empty name + empty SpeciesId fail UserPlantValidator.
        var invalid = new UserPlant { Name = "", SpeciesId = Guid.Empty, CurrentLevel = 1, Experience = 0 };

        Func<Task> act = () => service.AddAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>();
        (await _dbHelper.Context.UserPlants.CountAsync()).Should().Be(0, "validation must reject before persistence");
    }

    [Fact]
    public async Task PlantService_AddAsync_ValidPlant_WithoutTimestamps_Succeeds()
    {
        var cache = new ServiceCollection().AddMemoryCache().BuildServiceProvider()
            .GetRequiredService<IMemoryCache>();
        var service = new PlantService(
            new UnitOfWork(_dbHelper.Context),
            Substitute.For<IAppSettingsProvider>(),
            Substitute.For<IDecorationService>(),
            cache,
            NullLogger<PlantService>.Instance,
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        // No PlantedAt/LastWatered: AddAsync defaults them BEFORE validating, so the (otherwise
        // required, must-be-in-the-past) timestamp rules must still pass. Guards the defaulting order.
        var valid = new UserPlant { Name = "Ferdinand", SpeciesId = Guid.NewGuid(), CurrentLevel = 1, Experience = 0 };

        await service.AddAsync(valid);

        (await _dbHelper.Context.UserPlants.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PlantService_PurchasePlantAsync_InvalidName_ThrowsBeforeSpendingCoins()
    {
        // CODE_REVIEW BUG-05 follow-up: PurchasePlantAsync (not AddAsync) is the real production
        // plant-creation path, so plant validation must run there too — and before coins are spent.
        var cache = new ServiceCollection().AddMemoryCache().BuildServiceProvider()
            .GetRequiredService<IMemoryCache>();
        var factory = new TestDbContextFactory(_dbHelper.DatabaseName);
        var settingsProvider = new AppSettingsProvider(factory);

        var coinSettings = await _dbHelper.Context.AppSettings.FirstAsync();
        coinSettings.Coins = 10_000;
        var species = new PlantSpecies
        {
            Name = "Free Fern", ImagePath = "/x.svg", BaseCost = 100, UnlockLevel = 1,
            IsAvailable = true, IsFreeTier = true
        };
        _dbHelper.Context.PlantSpecies.Add(species);
        await _dbHelper.Context.SaveChangesAsync();
        settingsProvider.InvalidateCache();

        var service = new PlantService(
            new UnitOfWork(_dbHelper.Context),
            settingsProvider,
            Substitute.For<IDecorationService>(),
            cache,
            NullLogger<PlantService>.Instance,
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        var tooLongName = new string('x', 101); // UserPlantValidator caps Name at 100

        Func<Task> act = () => service.PurchasePlantAsync(species.Id, tooLongName);

        await act.Should().ThrowAsync<ValidationException>();
        (await settingsProvider.GetUserCoinsAsync()).Should().Be(10_000, "validation must run before coins are spent");
        (await _dbHelper.Context.UserPlants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BookService_UpdateAsync_PreexistingInvalidBook_DoesNotThrow()
    {
        // A book that violates a current rule (CurrentPage > PageCount) — e.g. legacy data from
        // before the progress clamp existed. A single-field edit (rating/note) calls UpdateAsync
        // with the whole entity and must NOT be rejected for a pre-existing inconsistency it did
        // not introduce. Validation lives on the user-entry paths (AddAsync / SaveBookWithRelations).
        var book = new Book { Title = "Legacy", Author = "A", PageCount = 100, CurrentPage = 600 };
        _dbHelper.Context.Books.Add(book);
        await _dbHelper.Context.SaveChangesAsync();
        _dbHelper.Context.ChangeTracker.Clear();

        var service = CreateBookService();
        book.CharactersRating = 5; // a rating edit on the pre-existing book

        Func<Task> act = () => service.UpdateAsync(book);

        await act.Should().NotThrowAsync("a single-field edit must not be blocked by a pre-existing violation");
        (await _dbHelper.Context.Books.FirstAsync(b => b.Id == book.Id)).CharactersRating.Should().Be(5);
    }

    [Fact]
    public async Task BookService_SaveBookWithRelationsAsync_ValidBook_Persists()
    {
        // Uses SQLite because SaveBookWithRelationsAsync opens a real transaction (unsupported by InMemory).
        using var sqlite = new SqliteTestContext();
        await using var ctx = sqlite.CreateContext();
        var service = new BookService(
            new UnitOfWork(ctx),
            Substitute.For<IProgressionService>(),
            Substitute.For<IPlantService>(),
            Substitute.For<IGoalService>(),
            NullLogger<BookService>.Instance,
            analytics: null,
            featureGuard: null,
            validationService: CreateValidationService());

        var valid = new Book { Title = "Valid", Author = "Author", Status = ReadingStatus.Planned };

        var result = await service.SaveBookWithRelationsAsync(
            valid, Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());

        result.Book.Should().NotBeNull();
        (await ctx.Books.CountAsync()).Should().Be(1);
    }
}
