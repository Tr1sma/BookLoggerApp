using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Integration;

/// <summary>Integration tests for the multi-category rating system end-to-end.</summary>
public class RatingIntegrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MockProgressionService _progressionService;
    private readonly MockPlantService _plantService;
    private readonly MockGoalService _goalService;
    private readonly BookService _bookService;
    private readonly StatsService _statsService;

    public RatingIntegrationTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);

        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _goalService = new MockGoalService();
        _bookService = new BookService(_unitOfWork, _progressionService, _plantService, _goalService, null!);
        _statsService = new StatsService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task FullWorkflow_SaveAndRetrieveRatings_ShouldWork()
    {

        var book = new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5,
            SpiceLevelRating = 3,
            PacingRating = 4,
            WorldBuildingRating = 5
        };


        var savedBook = await _bookService.AddAsync(book);


        var retrievedBook = await _bookService.GetByIdAsync(savedBook.Id);


        var averages = await _statsService.GetAllAverageRatingsAsync();
        var topBooks = await _statsService.GetTopRatedBooksAsync(10);

        retrievedBook.Should().NotBeNull();
        retrievedBook!.CharactersRating.Should().Be(5);
        retrievedBook.PlotRating.Should().Be(4);
        retrievedBook.WritingStyleRating.Should().Be(5);
        retrievedBook.SpiceLevelRating.Should().Be(3);
        retrievedBook.PacingRating.Should().Be(4);
        retrievedBook.WorldBuildingRating.Should().Be(5);
        retrievedBook.WorldBuildingRating.Should().Be(5);

        retrievedBook.AverageRating.Should().NotBeNull();
        retrievedBook.AverageRating.Value.Should().BeApproximately(4.33, 0.01);

        averages[RatingCategory.Characters].Should().Be(5.0);
        averages[RatingCategory.Plot].Should().Be(4.0);
        topBooks.Should().ContainSingle();
        topBooks[0].Book.Title.Should().Be("Test Book");
    }

    [Fact]
    public async Task MultipleBooks_StatisticsCalculation_ShouldBeAccurate()
    {

        var books = new[]
        {
            new Book
            {
                Title = "High Rated Book",
                Author = "Author 1",
                Status = ReadingStatus.Completed,
                CharactersRating = 5,
                PlotRating = 5,
                WritingStyleRating = 5
            },
            new Book
            {
                Title = "Medium Rated Book",
                Author = "Author 2",
                Status = ReadingStatus.Completed,
                CharactersRating = 3,
                PlotRating = 4,
                WritingStyleRating = 3
            },
            new Book
            {
                Title = "Low Rated Book",
                Author = "Author 3",
                Status = ReadingStatus.Completed,
                CharactersRating = 2,
                PlotRating = 2,
                WritingStyleRating = 2
            }
        };

        foreach (var book in books)
        {
            await _bookService.AddAsync(book);
        }


        var characterAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Characters);
        var plotAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Plot);
        var writingAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.WritingStyle);
        var topBooks = await _statsService.GetTopRatedBooksAsync(10);

        characterAverage.Should().BeApproximately(3.33, 0.01); // (5 + 3 + 2) / 3
        plotAverage.Should().BeApproximately(3.67, 0.01); // (5 + 4 + 2) / 3
        writingAverage.Should().BeApproximately(3.33, 0.01); // (5 + 3 + 2) / 3
        topBooks.Should().HaveCount(3);
        topBooks[0].Book.Title.Should().Be("High Rated Book");
        topBooks[1].Book.Title.Should().Be("Medium Rated Book");
        topBooks[2].Book.Title.Should().Be("Low Rated Book");
    }

    [Fact]
    public async Task UpdateRating_ShouldReflectInStatistics()
    {

        var book = new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 3
        };
        book = await _bookService.AddAsync(book);


        var initialAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Characters);


        book.CharactersRating = 5;
        await _bookService.UpdateAsync(book);


        var updatedAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Characters);

        initialAverage.Should().Be(3.0);
        updatedAverage.Should().Be(5.0);
    }

    [Fact]
    public async Task MixedRatings_SomeNull_ShouldHandleCorrectly()
    {

        var books = new[]
        {
            new Book
            {
                Title = "Book 1",
                Author = "Author",
                Status = ReadingStatus.Completed,
                CharactersRating = 5,
                PlotRating = null,
                WritingStyleRating = 4
            },
            new Book
            {
                Title = "Book 2",
                Author = "Author",
                Status = ReadingStatus.Completed,
                CharactersRating = null,
                PlotRating = 3,
                WritingStyleRating = 5
            },
            new Book
            {
                Title = "Book 3",
                Author = "Author",
                Status = ReadingStatus.Completed,
                CharactersRating = 4,
                PlotRating = 4,
                WritingStyleRating = null
            }
        };

        foreach (var book in books)
        {
            await _bookService.AddAsync(book);
        }

        // Get averages
        var characterAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Characters);
        var plotAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Plot);
        var writingAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.WritingStyle);

        characterAverage.Should().BeApproximately(4.5, 0.01); // (5 + 4) / 2
        plotAverage.Should().BeApproximately(3.5, 0.01); // (3 + 4) / 2
        writingAverage.Should().BeApproximately(4.5, 0.01); // (4 + 5) / 2
    }

    [Fact]
    public async Task TopRatedBooks_FilterByCategory_ShouldShowCorrectBooks()
    {

        var books = new[]
        {
            new Book
            {
                Title = "Best Characters",
                Author = "Author",
                Status = ReadingStatus.Completed,
                CharactersRating = 5,
                PlotRating = 2,
                WritingStyleRating = 3
            },
            new Book
            {
                Title = "Best Plot",
                Author = "Author",
                Status = ReadingStatus.Completed,
                CharactersRating = 2,
                PlotRating = 5,
                WritingStyleRating = 3
            },
            new Book
            {
                Title = "Best Writing",
                Author = "Author",
                Status = ReadingStatus.Completed,
                CharactersRating = 2,
                PlotRating = 3,
                WritingStyleRating = 5
            }
        };

        foreach (var book in books)
        {
            await _bookService.AddAsync(book);
        }

        // Get top books by different categories
        var topByCharacters = await _statsService.GetTopRatedBooksAsync(10, RatingCategory.Characters);
        var topByPlot = await _statsService.GetTopRatedBooksAsync(10, RatingCategory.Plot);
        var topByWriting = await _statsService.GetTopRatedBooksAsync(10, RatingCategory.WritingStyle);

        topByCharacters[0].Book.Title.Should().Be("Best Characters");
        topByPlot[0].Book.Title.Should().Be("Best Plot");
        topByWriting[0].Book.Title.Should().Be("Best Writing");
    }



    [Fact]
    public async Task DateRangeFilter_ShouldFilterCorrectly()
    {
        var oldBook = new Book
        {
            Title = "Old Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow.AddDays(-60),
            CharactersRating = 2
        };

        var midBook = new Book
        {
            Title = "Mid Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow.AddDays(-30),
            CharactersRating = 3
        };

        var recentBook = new Book
        {
            Title = "Recent Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow.AddDays(-5),
            CharactersRating = 5
        };

        await _bookService.AddAsync(oldBook);
        await _bookService.AddAsync(midBook);
        await _bookService.AddAsync(recentBook);

        var allTimeAverage = await _statsService.GetAverageRatingByCategoryAsync(RatingCategory.Characters);
        var lastMonthAverage = await _statsService.GetAverageRatingByCategoryAsync(
            RatingCategory.Characters,
            startDate: DateTime.UtcNow.AddDays(-45),
            endDate: DateTime.UtcNow
        );
        var lastWeekAverage = await _statsService.GetAverageRatingByCategoryAsync(
            RatingCategory.Characters,
            startDate: DateTime.UtcNow.AddDays(-10),
            endDate: DateTime.UtcNow
        );

        allTimeAverage.Should().BeApproximately(3.33, 0.01); // (2 + 3 + 5) / 3
        lastMonthAverage.Should().BeApproximately(4.0, 0.01); // (3 + 5) / 2
        lastWeekAverage.Should().Be(5.0); // Only recent book
    }

    [Fact]
    public async Task BookRatingSummary_ShouldIncludeAllRatingCategories()
    {
        var book = new Book
        {
            Title = "Complete Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5,
            SpiceLevelRating = 3,
            PacingRating = 4,
            WorldBuildingRating = 5,
            SpannungRating = 4,
            HumorRating = 3,
            InformationsgehaltRating = 5,
            EmotionaleTiefeRating = 4,
            AtmosphaereRating = 5,
        };
        await _bookService.AddAsync(book);

        var booksWithRatings = await _statsService.GetBooksWithRatingsAsync();

        booksWithRatings.Should().ContainSingle();
        var summary = booksWithRatings[0];

        summary.AverageRating.Should().BeApproximately(4.27, 0.01);
        summary.Ratings.Should().HaveCount(11);
        summary.Ratings[RatingCategory.Characters].Should().Be(5);
        summary.Ratings[RatingCategory.Plot].Should().Be(4);
        summary.Ratings[RatingCategory.WritingStyle].Should().Be(5);
        summary.Ratings[RatingCategory.SpiceLevel].Should().Be(3);
        summary.Ratings[RatingCategory.Pacing].Should().Be(4);
        summary.Ratings[RatingCategory.WorldBuilding].Should().Be(5);
        summary.Ratings[RatingCategory.Spannung].Should().Be(4);
        summary.Ratings[RatingCategory.Humor].Should().Be(3);
        summary.Ratings[RatingCategory.Informationsgehalt].Should().Be(5);
        summary.Ratings[RatingCategory.EmotionaleTiefe].Should().Be(4);
        summary.Ratings[RatingCategory.Atmosphaere].Should().Be(5);
    }
}
