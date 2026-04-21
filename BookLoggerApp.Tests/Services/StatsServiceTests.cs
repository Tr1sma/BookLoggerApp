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

    [Fact]
    public async Task GetCurrentStreakAsync_ShouldIgnoreOpenPlaceholderSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test Book", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 0,
            PagesRead = 0,
            EndedAt = null
        });
        await _context.SaveChangesAsync();

        // Act
        var streak = await _service.GetCurrentStreakAsync();

        // Assert
        streak.Should().Be(1);
    }

    [Fact]
    public async Task GetLongestStreakAsync_ShouldIgnoreOpenPlaceholderSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test Book", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-5),
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-4),
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-3),
            Minutes = 0,
            PagesRead = 0,
            EndedAt = null
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-2),
            Minutes = 15
        });
        await _context.SaveChangesAsync();

        // Act
        var longestStreak = await _service.GetLongestStreakAsync();

        // Assert
        longestStreak.Should().Be(2);
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

        });
        await _context.SaveChangesAsync();

        // Act
        var averages = await _service.GetAllAverageRatingsAsync();

        // Assert
        averages.Should().HaveCount(11);
        averages[RatingCategory.Characters].Should().Be(5.0);
        averages[RatingCategory.Plot].Should().Be(4.0);
        averages[RatingCategory.WritingStyle].Should().Be(5.0);
        averages[RatingCategory.SpiceLevel].Should().Be(3.0);
        averages[RatingCategory.Pacing].Should().Be(4.0);
        averages[RatingCategory.WorldBuilding].Should().Be(5.0);
        averages[RatingCategory.Spannung].Should().Be(0.0);
        averages[RatingCategory.Humor].Should().Be(0.0);
        averages[RatingCategory.Informationsgehalt].Should().Be(0.0);
        averages[RatingCategory.EmotionaleTiefe].Should().Be(0.0);
        averages[RatingCategory.Atmosphaere].Should().Be(0.0);
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
                CharactersRating = i % 5 + 1
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
            CharactersRating = 5
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed Book 2",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 4
        });

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading Book",
            Author = "Author",
            Status = ReadingStatus.Reading,
            CharactersRating = 3
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
            WorldBuildingRating = 5
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

    #region Argument Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetAveragePagesPerDayAsync_WithZeroOrNegativeDays_ShouldThrowArgumentOutOfRangeException(int days)
    {
        // Act
        var act = () => _service.GetAveragePagesPerDayAsync(days);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("days");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetAverageMinutesPerDayAsync_WithZeroOrNegativeDays_ShouldThrowArgumentOutOfRangeException(int days)
    {
        // Act
        var act = () => _service.GetAverageMinutesPerDayAsync(days);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("days");
    }

    [Fact]
    public async Task GetAveragePagesPerDayAsync_WithPositiveDays_ShouldNotThrow()
    {
        // Act
        var result = await _service.GetAveragePagesPerDayAsync(7);

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetAverageMinutesPerDayAsync_WithPositiveDays_ShouldNotThrow()
    {
        // Act
        var result = await _service.GetAverageMinutesPerDayAsync(7);

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0);
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

    #region Coverage-ergänzende Tests

    [Fact]
    public async Task GetTotalBooksReadAsync_CountsOnlyCompleted()
    {
        await _unitOfWork.Books.AddAsync(new Book { Title = "c1", Author = "a", Status = ReadingStatus.Completed });
        await _unitOfWork.Books.AddAsync(new Book { Title = "c2", Author = "a", Status = ReadingStatus.Completed });
        await _unitOfWork.Books.AddAsync(new Book { Title = "r", Author = "a", Status = ReadingStatus.Reading });
        await _context.SaveChangesAsync();

        var count = await _service.GetTotalBooksReadAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetTotalPagesReadAsync_SumsCompletedBookPages()
    {
        await _unitOfWork.Books.AddAsync(new Book { Title = "c1", Author = "a", Status = ReadingStatus.Completed, PageCount = 200 });
        await _unitOfWork.Books.AddAsync(new Book { Title = "c2", Author = "a", Status = ReadingStatus.Completed, PageCount = 300 });
        await _unitOfWork.Books.AddAsync(new Book { Title = "r", Author = "a", Status = ReadingStatus.Reading, PageCount = 150 });
        await _unitOfWork.Books.AddAsync(new Book { Title = "nopages", Author = "a", Status = ReadingStatus.Completed, PageCount = null });
        await _context.SaveChangesAsync();

        var total = await _service.GetTotalPagesReadAsync();

        total.Should().Be(500);
    }

    [Fact]
    public async Task GetTotalMinutesReadAsync_SumsSessionMinutes()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 20 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 35 });
        await _context.SaveChangesAsync();

        var total = await _service.GetTotalMinutesReadAsync();

        total.Should().Be(55);
    }

    [Fact]
    public async Task GetReadingTrendAsync_GroupsSessionsByDate()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var d1 = DateTime.UtcNow.Date.AddDays(-3);
        var d2 = DateTime.UtcNow.Date.AddDays(-1);
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = d1, EndedAt = d1.AddMinutes(20), Minutes = 20 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = d1, EndedAt = d1.AddMinutes(10), Minutes = 10 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = d2, EndedAt = d2.AddMinutes(15), Minutes = 15 });
        await _context.SaveChangesAsync();

        var result = await _service.GetReadingTrendAsync(d1.AddDays(-1), d2.AddDays(1));

        result.Should().HaveCount(2);
        result[d1].Should().Be(30);
        result[d2].Should().Be(15);
    }

    [Fact]
    public async Task GetPagesReadInRangeAsync_SumsOnlyInRange()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = today.AddDays(-10), EndedAt = today.AddDays(-10).AddMinutes(5), Minutes = 5, PagesRead = 10 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = today, EndedAt = today.AddMinutes(5), Minutes = 5, PagesRead = 20 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = today, EndedAt = today.AddMinutes(5), Minutes = 5, PagesRead = null });
        await _context.SaveChangesAsync();

        var pages = await _service.GetPagesReadInRangeAsync(today.AddDays(-1), today.AddDays(1));

        pages.Should().Be(20);
    }

    [Fact]
    public async Task GetBooksCompletedInYearAsync_FiltersByYear()
    {
        await _unitOfWork.Books.AddAsync(new Book { Title = "2023", Author = "a", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2023, 6, 15) });
        await _unitOfWork.Books.AddAsync(new Book { Title = "2024", Author = "a", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2024, 1, 5) });
        await _unitOfWork.Books.AddAsync(new Book { Title = "2024b", Author = "a", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2024, 12, 31) });
        await _context.SaveChangesAsync();

        (await _service.GetBooksCompletedInYearAsync(2023)).Should().Be(1);
        (await _service.GetBooksCompletedInYearAsync(2024)).Should().Be(2);
        (await _service.GetBooksCompletedInYearAsync(2022)).Should().Be(0);
    }

    [Fact]
    public async Task GetBooksCompletedInRangeAsync_FiltersByDateRange()
    {
        await _unitOfWork.Books.AddAsync(new Book { Title = "In", Author = "a", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2024, 6, 15) });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Out", Author = "a", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2023, 1, 1) });
        await _context.SaveChangesAsync();

        var count = await _service.GetBooksCompletedInRangeAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetTopBooksInRangeAsync_OrdersByAverageRating()
    {
        var low = new Book { Title = "Low", Author = "a", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow, CharactersRating = 1, PlotRating = 1 };
        var high = new Book { Title = "High", Author = "a", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow, CharactersRating = 5, PlotRating = 5 };
        var mid = new Book { Title = "Mid", Author = "a", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow, CharactersRating = 3, PlotRating = 3 };
        await _unitOfWork.Books.AddAsync(low);
        await _unitOfWork.Books.AddAsync(high);
        await _unitOfWork.Books.AddAsync(mid);
        await _context.SaveChangesAsync();

        var result = await _service.GetTopBooksInRangeAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), count: 2);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("High");
        result[1].Title.Should().Be("Mid");
    }

    [Fact]
    public async Task GetBooksByGenreAsync_GroupsCorrectly()
    {
        var fantasy = new Genre { Name = "Fantasy" };
        var sf = new Genre { Name = "SF" };
        _context.Genres.AddRange(fantasy, sf);
        var b1 = new Book { Title = "B1", Author = "A", Status = ReadingStatus.Completed };
        var b2 = new Book { Title = "B2", Author = "A", Status = ReadingStatus.Completed };
        var b3 = new Book { Title = "B3", Author = "A", Status = ReadingStatus.Completed };
        _context.Books.AddRange(b1, b2, b3);
        _context.BookGenres.AddRange(
            new BookGenre { BookId = b1.Id, GenreId = fantasy.Id },
            new BookGenre { BookId = b2.Id, GenreId = fantasy.Id },
            new BookGenre { BookId = b3.Id, GenreId = sf.Id }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetBooksByGenreAsync();

        result["Fantasy"].Should().Be(2);
        result["SF"].Should().Be(1);
    }

    [Fact]
    public async Task GetFavoriteGenreAsync_NoBooks_ReturnsNull()
    {
        var result = await _service.GetFavoriteGenreAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFavoriteGenreAsync_ReturnsMostFrequent()
    {
        var fantasy = new Genre { Name = "Fantasy" };
        var sf = new Genre { Name = "SF" };
        _context.Genres.AddRange(fantasy, sf);
        var b1 = new Book { Title = "B1", Author = "A", Status = ReadingStatus.Completed };
        var b2 = new Book { Title = "B2", Author = "A", Status = ReadingStatus.Completed };
        var b3 = new Book { Title = "B3", Author = "A", Status = ReadingStatus.Completed };
        _context.Books.AddRange(b1, b2, b3);
        _context.BookGenres.AddRange(
            new BookGenre { BookId = b1.Id, GenreId = fantasy.Id },
            new BookGenre { BookId = b2.Id, GenreId = fantasy.Id },
            new BookGenre { BookId = b3.Id, GenreId = sf.Id }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetFavoriteGenreAsync();

        result.Should().Be("Fantasy");
    }

    [Fact]
    public async Task GetAverageRatingAsync_NoBooks_ReturnsZero()
    {
        var result = await _service.GetAverageRatingAsync();

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetAverageRatingAsync_ComputesAverage()
    {
        await _unitOfWork.Books.AddAsync(new Book { Title = "A", Author = "x", CharactersRating = 4, PlotRating = 4 });
        await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "x", CharactersRating = 2, PlotRating = 2 });
        await _context.SaveChangesAsync();

        var avg = await _service.GetAverageRatingAsync();

        avg.Should().Be(3.0);
    }

    [Fact]
    public async Task GetActiveReadingPeriodsAsync_ReturnsDistinctYearMonthTuples()
    {
        await _unitOfWork.Books.AddAsync(new Book { Title = "A", Author = "x", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2024, 3, 10) });
        await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "x", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2024, 3, 20) });
        await _unitOfWork.Books.AddAsync(new Book { Title = "C", Author = "x", Status = ReadingStatus.Completed, DateCompleted = new DateTime(2024, 5, 5) });
        await _context.SaveChangesAsync();

        var periods = await _service.GetActiveReadingPeriodsAsync();

        periods.Should().HaveCount(2);
        periods[0].Should().Be((2024, 5));
        periods[1].Should().Be((2024, 3));
    }

    #endregion
}
