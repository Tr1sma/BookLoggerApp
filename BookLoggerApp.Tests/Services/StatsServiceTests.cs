using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class StatsServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StatsService _service;

    public StatsServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new StatsService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Multi-Category Rating Tests

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_WithNoBooks_ShouldReturnZero()
    {
        // Arrange - No books in database

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Characters);

        // Assert
        average.Should().Be(0);
    }

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_WithSingleBook_ShouldReturnCorrectAverage()
    {
        // Arrange
        var book = new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5
        };
        await _unitOfWork.Books.AddAsync(book);
        await _context.SaveChangesAsync();

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Characters);

        // Assert
        average.Should().Be(5.0);
    }

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_WithMultipleBooks_ShouldCalculateCorrectly()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1",
            Author = "Author 1",
            Status = ReadingStatus.Completed,
            CharactersRating = 5
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2",
            Author = "Author 2",
            Status = ReadingStatus.Completed,
            CharactersRating = 3
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3",
            Author = "Author 3",
            Status = ReadingStatus.Completed,
            CharactersRating = 4
        });
        await _context.SaveChangesAsync();

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Characters);

        // Assert
        average.Should().BeApproximately(4.0, 0.01); // (5 + 3 + 4) / 3
    }

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_ShouldIgnoreNullRatings()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1",
            Author = "Author 1",
            Status = ReadingStatus.Completed,
            PlotRating = 5
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2",
            Author = "Author 2",
            Status = ReadingStatus.Completed,
            PlotRating = null
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3",
            Author = "Author 3",
            Status = ReadingStatus.Completed,
            PlotRating = 3
        });
        await _context.SaveChangesAsync();

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Plot);

        // Assert
        average.Should().BeApproximately(4.0, 0.01); // (5 + 3) / 2
    }

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_ShouldOnlyIncludeCompletedBooks()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            WritingStyleRating = 5
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading Book",
            Author = "Author",
            Status = ReadingStatus.Reading,
            WritingStyleRating = 3
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Planned Book",
            Author = "Author",
            Status = ReadingStatus.Planned,
            WritingStyleRating = 1
        });
        await _context.SaveChangesAsync();

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(RatingCategory.WritingStyle);

        // Assert
        average.Should().Be(5.0); // Only completed book
    }

    [Fact]
    public async Task GetAllAverageRatingsAsync_ShouldReturnAllCategories()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5,
            SpiceLevelRating = 3,
            PacingRating = 4,
            WorldBuildingRating = 5,
            OverallRating = 4
        });
        await _context.SaveChangesAsync();

        // Act
        var averages = await _service.GetAllAverageRatingsAsync();

        // Assert
        averages.Should().HaveCount(7);
        averages[RatingCategory.Characters].Should().Be(5.0);
        averages[RatingCategory.Plot].Should().Be(4.0);
        averages[RatingCategory.WritingStyle].Should().Be(5.0);
        averages[RatingCategory.SpiceLevel].Should().Be(3.0);
        averages[RatingCategory.Pacing].Should().Be(4.0);
        averages[RatingCategory.WorldBuilding].Should().Be(5.0);
        averages[RatingCategory.Overall].Should().Be(4.0);
    }

    [Fact]
    public async Task GetTopRatedBooksAsync_ShouldReturnBooksOrderedByRating()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Low Rated",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 2,
            PlotRating = 2
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "High Rated",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 5
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Medium Rated",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 3,
            PlotRating = 4
        });
        await _context.SaveChangesAsync();

        // Act
        var topBooks = await _service.GetTopRatedBooksAsync(10);

        // Assert
        topBooks.Should().HaveCount(3);
        topBooks[0].Book.Title.Should().Be("High Rated");
        topBooks[0].AverageRating.Should().Be(5.0);
        topBooks[1].Book.Title.Should().Be("Medium Rated");
        topBooks[2].Book.Title.Should().Be("Low Rated");
    }

    [Fact]
    public async Task GetTopRatedBooksAsync_ShouldRespectCountParameter()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await _unitOfWork.Books.AddAsync(new Book
            {
                Title = $"Book {i}",
                Author = "Author",
                Status = ReadingStatus.Completed,
                OverallRating = i % 5 + 1
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var topBooks = await _service.GetTopRatedBooksAsync(5);

        // Assert
        topBooks.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetTopRatedBooksAsync_FilteredByCategory_ShouldOnlyConsiderThatCategory()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Best Plot",
            Author = "Author",
            Status = ReadingStatus.Completed,
            PlotRating = 5,
            CharactersRating = 2
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Best Characters",
            Author = "Author",
            Status = ReadingStatus.Completed,
            PlotRating = 2,
            CharactersRating = 5
        });
        await _context.SaveChangesAsync();

        // Act
        var topByPlot = await _service.GetTopRatedBooksAsync(10, RatingCategory.Plot);
        var topByCharacters = await _service.GetTopRatedBooksAsync(10, RatingCategory.Characters);

        // Assert
        topByPlot[0].Book.Title.Should().Be("Best Plot");
        topByCharacters[0].Book.Title.Should().Be("Best Characters");
    }

    [Fact]
    public async Task GetBooksWithRatingsAsync_ShouldReturnAllCompletedBooks()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed Book 1",
            Author = "Author",
            Status = ReadingStatus.Completed,
            OverallRating = 5
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed Book 2",
            Author = "Author",
            Status = ReadingStatus.Completed,
            OverallRating = 4
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading Book",
            Author = "Author",
            Status = ReadingStatus.Reading,
            OverallRating = 3
        });
        await _context.SaveChangesAsync();

        // Act
        var books = await _service.GetBooksWithRatingsAsync();

        // Assert
        books.Should().HaveCount(2);
        books.All(b => b.Book.Status == ReadingStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public async Task GetBooksWithRatingsAsync_ShouldIncludeRatingsDictionary()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = null,
            SpiceLevelRating = 3
        });
        await _context.SaveChangesAsync();

        // Act
        var books = await _service.GetBooksWithRatingsAsync();

        // Assert
        var book = books.First();
        book.Ratings.Should().ContainKey(RatingCategory.Characters);
        book.Ratings[RatingCategory.Characters].Should().Be(5);
        book.Ratings.Should().ContainKey(RatingCategory.Plot);
        book.Ratings[RatingCategory.Plot].Should().Be(4);
        book.Ratings.Should().ContainKey(RatingCategory.SpiceLevel);
        book.Ratings[RatingCategory.SpiceLevel].Should().Be(3);
    }

    [Theory]
    [InlineData(RatingCategory.Characters)]
    [InlineData(RatingCategory.Plot)]
    [InlineData(RatingCategory.WritingStyle)]
    [InlineData(RatingCategory.SpiceLevel)]
    [InlineData(RatingCategory.Pacing)]
    [InlineData(RatingCategory.WorldBuilding)]
    [InlineData(RatingCategory.Overall)]
    public async Task GetAverageRatingByCategoryAsync_AllCategories_ShouldWork(RatingCategory category)
    {
        // Arrange
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
            WorldBuildingRating = 5,
            OverallRating = 4
        };
        await _unitOfWork.Books.AddAsync(book);
        await _context.SaveChangesAsync();

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(category);

        // Assert
        average.Should().BeGreaterThan(0);
        average.Should().BeLessThanOrEqualTo(5);
    }

    #endregion

    #region Date Range Filter Tests

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_WithDateRange_ShouldFilterCorrectly()
    {
        // Arrange
        var oldDate = DateTime.UtcNow.AddDays(-30);
        var recentDate = DateTime.UtcNow.AddDays(-5);

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Old Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = oldDate,
            CharactersRating = 2
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Recent Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = recentDate,
            CharactersRating = 5
        });
        await _context.SaveChangesAsync();

        // Act
        var allAverage = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Characters);
        var filteredAverage = await _service.GetAverageRatingByCategoryAsync(
            RatingCategory.Characters,
            startDate: DateTime.UtcNow.AddDays(-10),
            endDate: DateTime.UtcNow
        );

        // Assert
        allAverage.Should().BeApproximately(3.5, 0.01); // (2 + 5) / 2
        filteredAverage.Should().Be(5.0); // Only recent book
    }

    #endregion
}
