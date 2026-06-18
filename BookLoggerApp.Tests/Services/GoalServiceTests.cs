using FluentAssertions;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class GoalServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GoalService _service;

    public GoalServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new GoalService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task UpdateGoalProgressAsync_ShouldUpdateProgress()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 100 pages",
            Type = GoalType.Pages,
            Target = 100,
            Current = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        await _service.UpdateGoalProgressAsync(goal.Id, 50);

        var updated = await _service.GetByIdAsync(goal.Id);
        updated!.Current.Should().Be(50);
        updated.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGoalProgressAsync_ShouldAutoCompleteWhenTargetReached()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 100 pages",
            Type = GoalType.Pages,
            Target = 100,
            Current = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        await _service.UpdateGoalProgressAsync(goal.Id, 100);

        var updated = await _service.GetByIdAsync(goal.Id);
        updated!.Current.Should().Be(100);
        updated.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveGoalsAsync_ShouldReturnOnlyActiveGoals()
    {
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Active Goal",
            Type = GoalType.Books,
            Target = 10,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            IsCompleted = false
        });
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Completed Goal",
            Type = GoalType.Books,
            Target = 10,
            Current = 10,
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(-30),
            IsCompleted = true
        });
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Expired Goal",
            Type = GoalType.Books,
            Target = 10,
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(-1),
            IsCompleted = false
        });

        var activeGoals = await _service.GetActiveGoalsAsync();

        activeGoals.Should().HaveCount(1);
        activeGoals.First().Title.Should().Be("Active Goal");
    }

    [Fact]
    public async Task CalculateGoalProgress_WithUnspecifiedKindDatesFromUiDatePicker_ShouldCountUtcBookInRange()
    {
        // Regression gate for the timezone-kind mismatch in goal progress calculation.
        // The UI binds <input type="date"> to DateTime with Kind=Unspecified (ticks
        // represent the user's local calendar midnight), while Book.DateCompleted is
        // written as DateTime.UtcNow (Kind=Utc). DateTime comparison ignores Kind and
        // uses raw ticks, so without a conversion the comparisons mix local-midnight
        // ticks with UTC-instant ticks and misclassify books near day boundaries in
        // non-UTC timezones. The helper GoalService.GetGoalRangeUtc bridges this by
        // calling ToUniversalTime() on the bounds; this test pins that behavior.

        var nowYear = DateTime.UtcNow.Year;
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "UI-Picker Goal",
            Type = GoalType.Books,
            Target = 5,
            // UI date-picker format
            StartDate = new DateTime(nowYear - 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            EndDate = new DateTime(nowYear + 1, 12, 31, 0, 0, 0, DateTimeKind.Unspecified)
        });

        // Kind=Utc, inside range
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "In-range book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        var activeGoals = await _service.GetActiveGoalsAsync();

        activeGoals.Should().ContainSingle(g => g.Id == goal.Id)
            .Which.Current.Should().Be(1, "the UTC-stamped book must match a Kind=Unspecified goal range");
    }

    [Fact]
    public async Task GetActiveGoalsAsync_WithUnspecifiedEndDateEndingToday_ShouldStillBeActive()
    {
        // Regression gate for the second half of the same kind-mismatch bug: the
        // ReadingGoalRepository.GetActiveGoalsAsync filter previously compared EndDate
        // against DateTime.UtcNow. For a user in a positive-UTC timezone, a goal whose
        // EndDate equals today's local midnight (Kind=Unspecified) would get filtered
        // out several hours before the local day ended, because UtcNow's ticks already
        // exceed the stored local-midnight ticks. The fix compares against
        // DateTime.Now.Date so goals ending "today locally" remain visible all day.

        var localToday = DateTime.Now.Date; // Kind=Local
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Ends Today",
            Type = GoalType.Books,
            Target = 1,
            StartDate = new DateTime(localToday.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            // UI date-picker format
            EndDate = new DateTime(localToday.Year, localToday.Month, localToday.Day, 0, 0, 0, DateTimeKind.Unspecified)
        });

        var activeGoals = await _service.GetActiveGoalsAsync();

        activeGoals.Should().ContainSingle(g => g.Id == goal.Id);
    }

    [Fact]
    public async Task RecalculateGoalProgressAsync_ShouldMarkNewlyCompletedGoalsAndReturnTrue()
    {
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 30 minutes",
            Type = GoalType.Minutes,
            Target = 30,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsCompleted = false
        });

        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            EndedAt = DateTime.UtcNow,
            Minutes = 30
        });
        await _unitOfWork.SaveChangesAsync();

        var result = await _service.RecalculateGoalProgressAsync();

        result.Should().BeTrue();

        var completedGoals = await _service.GetCompletedGoalsAsync();
        completedGoals.Should().ContainSingle();
        completedGoals[0].IsCompleted.Should().BeTrue();
        completedGoals[0].CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckAndCompleteGoalsAsync_ShouldCompleteReachedGoals()
    {
        var goal1 = await _service.AddAsync(new ReadingGoal
        {
            Title = "Goal 1",
            Type = GoalType.Pages,
            Target = 100,
            Current = 100,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            IsCompleted = false
        });
        var goal2 = await _service.AddAsync(new ReadingGoal
        {
            Title = "Goal 2",
            Type = GoalType.Pages,
            Target = 100,
            Current = 50,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            IsCompleted = false
        });

        await _service.CheckAndCompleteGoalsAsync();

        var updated1 = await _service.GetByIdAsync(goal1.Id);
        var updated2 = await _service.GetByIdAsync(goal2.Id);

        updated1!.IsCompleted.Should().BeTrue();
        updated2!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ExcludeBookFromGoalAsync_ShouldExcludeBook()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 5 books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book = new Book
        {
            Title = "Test Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow
        };
        await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync();

        await _service.ExcludeBookFromGoalAsync(goal.Id, book.Id);

        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);
        exclusions.Should().HaveCount(1);
        exclusions.First().BookId.Should().Be(book.Id);
    }

    [Fact]
    public async Task IncludeBookInGoalAsync_ShouldRemoveExclusion()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 5 books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book = new Book
        {
            Title = "Test Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow
        };
        await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync();

        await _service.ExcludeBookFromGoalAsync(goal.Id, book.Id);

        await _service.IncludeBookInGoalAsync(goal.Id, book.Id);

        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);
        exclusions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveGoalsAsync_ShouldNotCountExcludedBooks()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 3 books",
            Type = GoalType.Books,
            Target = 3,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book1 = new Book { Title = "Book 1", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "Book 2", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book3 = new Book { Title = "Book 3", Author = "C", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.Books.AddAsync(book3);
        await _unitOfWork.SaveChangesAsync();

        await _service.ExcludeBookFromGoalAsync(goal.Id, book2.Id);
        var activeGoals = await _service.GetActiveGoalsAsync();

        var loadedGoal = activeGoals.First();
        loadedGoal.Current.Should().Be(2);
        loadedGoal.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ExcludeBookFromGoalAsync_ShouldBeIdempotent()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 5 books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book = new Book { Title = "Book", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync();

        await _service.ExcludeBookFromGoalAsync(goal.Id, book.Id);
        await _service.ExcludeBookFromGoalAsync(goal.Id, book.Id);

        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);
        exclusions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveGoalsAsync_ExcludedBookShouldNotPreventCompletion()
    {
        // target=2, 3 books, exclude 1 → auto-complete
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 2 books",
            Type = GoalType.Books,
            Target = 2,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book1 = new Book { Title = "Book 1", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "Book 2", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book3 = new Book { Title = "Book 3", Author = "C", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.Books.AddAsync(book3);
        await _unitOfWork.SaveChangesAsync();

        await _service.ExcludeBookFromGoalAsync(goal.Id, book3.Id);
        var activeGoals = await _service.GetActiveGoalsAsync();

        var completedGoals = await _service.GetCompletedGoalsAsync();
        var completedGoal = completedGoals.FirstOrDefault(g => g.Id == goal.Id);
        completedGoal.Should().NotBeNull();
        completedGoal!.Current.Should().Be(2);
        completedGoal.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task IncludeBookInGoalAsync_ShouldBeNoOpIfNotExcluded()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 5 books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book = new Book { Title = "Book", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync();

        await _service.IncludeBookInGoalAsync(goal.Id, book.Id);

        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);
        exclusions.Should().BeEmpty();
    }

    // ===== Genre Filter Tests =====

    [Fact]
    public async Task AddGenreToGoalAsync_ShouldAddGenre()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read Fantasy books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var genre = (await _unitOfWork.Genres.GetAllAsync()).First();

        await _service.AddGenreToGoalAsync(goal.Id, genre.Id);

        var goalGenres = await _service.GetGoalGenresAsync(goal.Id);
        goalGenres.Should().HaveCount(1);
        goalGenres.First().GenreId.Should().Be(genre.Id);
    }

    [Fact]
    public async Task AddGenreToGoalAsync_ShouldBeIdempotent()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read Fantasy books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var genre = (await _unitOfWork.Genres.GetAllAsync()).First();

        await _service.AddGenreToGoalAsync(goal.Id, genre.Id);
        await _service.AddGenreToGoalAsync(goal.Id, genre.Id);

        var goalGenres = await _service.GetGoalGenresAsync(goal.Id);
        goalGenres.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveGenreFromGoalAsync_ShouldRemoveGenre()
    {
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read Fantasy books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var genre = (await _unitOfWork.Genres.GetAllAsync()).First();
        await _service.AddGenreToGoalAsync(goal.Id, genre.Id);

        await _service.RemoveGenreFromGoalAsync(goal.Id, genre.Id);

        var goalGenres = await _service.GetGoalGenresAsync(goal.Id);
        goalGenres.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveGoalsAsync_WithGenreFilter_ShouldOnlyCountMatchingBooks()
    {
        var allGenres = (await _unitOfWork.Genres.GetAllAsync()).ToList();
        var fantasy = allGenres.First(g => g.Name == "Fantasy");
        var romance = allGenres.First(g => g.Name == "Romance");

        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 3 Fantasy books",
            Type = GoalType.Books,
            Target = 3,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        await _service.AddGenreToGoalAsync(goal.Id, fantasy.Id);

        // 2 Fantasy, 1 Romance
        var book1 = new Book { Title = "Fantasy Book 1", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "Fantasy Book 2", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book3 = new Book { Title = "Romance Book", Author = "C", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.Books.AddAsync(book3);
        await _unitOfWork.SaveChangesAsync();

        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book1.Id, GenreId = fantasy.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book2.Id, GenreId = fantasy.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book3.Id, GenreId = romance.Id });
        await _unitOfWork.SaveChangesAsync();

        var activeGoals = await _service.GetActiveGoalsAsync();

        var loadedGoal = activeGoals.First();
        loadedGoal.Current.Should().Be(2);
    }

    [Fact]
    public async Task GetActiveGoalsAsync_WithoutGenreFilter_ShouldCountAllBooks()
    {
        // no genre filter
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 3 books",
            Type = GoalType.Books,
            Target = 3,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book1 = new Book { Title = "Book 1", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "Book 2", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.SaveChangesAsync();

        var activeGoals = await _service.GetActiveGoalsAsync();

        var loadedGoal = activeGoals.First();
        loadedGoal.Current.Should().Be(2);
    }

    [Fact]
    public async Task GetActiveGoalsAsync_WithMultipleGenres_ShouldUseOrLogic()
    {
        // Fantasy OR Romance
        var allGenres = (await _unitOfWork.Genres.GetAllAsync()).ToList();
        var fantasy = allGenres.First(g => g.Name == "Fantasy");
        var romance = allGenres.First(g => g.Name == "Romance");
        var mystery = allGenres.First(g => g.Name == "Mystery");

        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read Fantasy or Romance",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        await _service.AddGenreToGoalAsync(goal.Id, fantasy.Id);
        await _service.AddGenreToGoalAsync(goal.Id, romance.Id);

        var book1 = new Book { Title = "Fantasy Book", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "Romance Book", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book3 = new Book { Title = "Mystery Book", Author = "C", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.Books.AddAsync(book3);
        await _unitOfWork.SaveChangesAsync();

        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book1.Id, GenreId = fantasy.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book2.Id, GenreId = romance.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book3.Id, GenreId = mystery.Id });
        await _unitOfWork.SaveChangesAsync();

        var activeGoals = await _service.GetActiveGoalsAsync();

        var loadedGoal = activeGoals.First();
        loadedGoal.Current.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllGoals()
    {
        await _service.AddAsync(new ReadingGoal { Title = "G1", Type = GoalType.Books, Target = 5, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });
        await _service.AddAsync(new ReadingGoal { Title = "G2", Type = GoalType.Books, Target = 5, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30), IsCompleted = true, CompletedAt = DateTime.UtcNow });

        var all = await _service.GetAllAsync();

        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsGoal()
    {
        var goal = await _service.AddAsync(new ReadingGoal { Title = "X", Type = GoalType.Books, Target = 1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1) });

        var result = await _service.GetByIdAsync(goal.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("X");
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Existing_Removes()
    {
        var goal = await _service.AddAsync(new ReadingGoal { Title = "Del", Type = GoalType.Books, Target = 1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1) });

        await _service.DeleteAsync(goal.Id);

        (await _service.GetByIdAsync(goal.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.DeleteAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var goal = await _service.AddAsync(new ReadingGoal { Title = "Original", Type = GoalType.Books, Target = 1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1) });
        goal.Title = "Updated";

        await _service.UpdateAsync(goal);

        var reloaded = await _service.GetByIdAsync(goal.Id);
        reloaded!.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task GetCompletedGoalsAsync_ReturnsOnlyCompleted()
    {
        await _service.AddAsync(new ReadingGoal { Title = "Active", Type = GoalType.Books, Target = 10, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });
        var doneGoal = new ReadingGoal
        {
            Title = "Done",
            Type = GoalType.Books,
            Target = 1,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow
        };
        await _service.AddAsync(doneGoal);

        var completed = await _service.GetCompletedGoalsAsync();

        completed.Should().HaveCount(1);
        completed[0].Title.Should().Be("Done");
    }

    [Fact]
    public async Task GetGoalsByTypeAsync_FiltersByType()
    {
        await _service.AddAsync(new ReadingGoal { Title = "Pages", Type = GoalType.Pages, Target = 100, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });
        await _service.AddAsync(new ReadingGoal { Title = "Books", Type = GoalType.Books, Target = 5, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });
        await _service.AddAsync(new ReadingGoal { Title = "Minutes", Type = GoalType.Minutes, Target = 60, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });

        var pagesGoals = await _service.GetGoalsByTypeAsync(GoalType.Pages);
        var booksGoals = await _service.GetGoalsByTypeAsync(GoalType.Books);
        var minGoals = await _service.GetGoalsByTypeAsync(GoalType.Minutes);

        pagesGoals.Should().HaveCount(1);
        booksGoals.Should().HaveCount(1);
        minGoals.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateGoalProgressAsync_NotExisting_ThrowsEntityNotFound()
    {
        Func<Task> act = async () => await _service.UpdateGoalProgressAsync(Guid.NewGuid(), 10);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public void NotifyGoalsChanged_TriggersEvent()
    {
        var invoked = false;
        _service.GoalsChanged += (s, e) => invoked = true;

        _service.NotifyGoalsChanged();

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task GetExcludedBooksAsync_ReturnsExclusionsForGoal()
    {
        var goal = await _service.AddAsync(new ReadingGoal { Title = "G", Type = GoalType.Books, Target = 10, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });
        var bookId = Guid.NewGuid();
        await _service.ExcludeBookFromGoalAsync(goal.Id, bookId);

        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);

        exclusions.Should().HaveCount(1);
        exclusions[0].BookId.Should().Be(bookId);
    }

    [Fact]
    public async Task GetGoalGenresAsync_ReturnsGenresForGoal()
    {
        var goal = await _service.AddAsync(new ReadingGoal { Title = "G", Type = GoalType.Books, Target = 10, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) });
        var genreId = Guid.NewGuid();
        _context.Genres.Add(new Genre { Id = genreId, Name = "SF" });
        await _context.SaveChangesAsync();
        await _service.AddGenreToGoalAsync(goal.Id, genreId);

        var genres = await _service.GetGoalGenresAsync(goal.Id);

        genres.Should().HaveCount(1);
        genres[0].GenreId.Should().Be(genreId);
    }

    [Fact]
    public async Task RecalculateGoalProgressAsync_NoGoals_ReturnsFalse()
    {
        var result = await _service.RecalculateGoalProgressAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveGoalsAsync_MinutesType_SumsSessionMinutes()
    {
        var book = new Book { Title = "B", Author = "A", Status = ReadingStatus.Reading };
        _context.Books.Add(book);
        var goal = new ReadingGoal
        {
            Title = "MinGoal",
            Type = GoalType.Minutes,
            Target = 120,
            Current = 0,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        _context.ReadingGoals.Add(goal);

        _context.ReadingSessions.Add(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddMinutes(45),
            Minutes = 45
        });
        _context.ReadingSessions.Add(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddMinutes(30),
            Minutes = 30
        });
        await _context.SaveChangesAsync();

        var activeGoals = await _service.GetActiveGoalsAsync();

        activeGoals.Should().HaveCount(1);
        activeGoals[0].Current.Should().Be(75);
    }
}
