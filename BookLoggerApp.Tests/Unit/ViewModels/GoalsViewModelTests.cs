using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class GoalsViewModelTests
{
    private readonly IGoalService _goalService;
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;
    private readonly GoalsViewModel _vm;

    public GoalsViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _goalService = Substitute.For<IGoalService>();
        _bookService = Substitute.For<IBookService>();
        _genreService = Substitute.For<IGenreService>();

        _goalService.GetActiveGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ReadingGoal>>(new List<ReadingGoal>()));
        _goalService.GetCompletedGoalsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ReadingGoal>>(new List<ReadingGoal>()));
        _goalService.GetGoalGenresAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GoalGenre>>(new List<GoalGenre>()));
        _goalService.GetExcludedBooksAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GoalExcludedBook>>(new List<GoalExcludedBook>()));
        _genreService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Genre>>(new List<Genre>()));
        _bookService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book>()));

        _vm = new GoalsViewModel(_goalService, _bookService, _genreService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesCollections()
    {
        _goalService.GetActiveGoalsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ReadingGoal>>(
            new List<ReadingGoal> { new() { Title = "Active" } }));
        _goalService.GetCompletedGoalsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ReadingGoal>>(
            new List<ReadingGoal> { new() { Title = "Done" }, new() { Title = "Done2" } }));
        _genreService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Genre>>(
            new List<Genre> { new() { Name = "Zebra" }, new() { Name = "Alpha" } }));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.ActiveGoals.Should().HaveCount(1);
        _vm.CompletedGoals.Should().HaveCount(2);
        _vm.AllGenres.Should().HaveCount(2);
        _vm.AllGenres[0].Name.Should().Be("Alpha"); // sorted
    }

    [Fact]
    public void OpenCreateForm_InitializesNewGoalWithYearRange()
    {
        _vm.OpenCreateFormCommand.Execute(null);

        _vm.ShowCreateForm.Should().BeTrue();
        _vm.IsEditing.Should().BeFalse();
        _vm.NewGoal.Should().NotBeNull();
        _vm.NewGoal!.Type.Should().Be(GoalType.Books);
        _vm.NewGoal.StartDate.Month.Should().Be(1);
        _vm.NewGoal.EndDate.Month.Should().Be(12);
    }

    [Fact]
    public void CancelCreate_ClearsForm()
    {
        _vm.OpenCreateFormCommand.Execute(null);

        _vm.CancelCreateCommand.Execute(null);

        _vm.ShowCreateForm.Should().BeFalse();
        _vm.NewGoal.Should().BeNull();
        _vm.IsEditing.Should().BeFalse();
    }

    [Fact]
    public async Task SaveGoalAsync_NoTitle_SetsError()
    {
        _vm.OpenCreateFormCommand.Execute(null);
        _vm.NewGoal!.Title = "";

        await _vm.SaveGoalCommand.ExecuteAsync(null);

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("Goal title is required");
        await _goalService.DidNotReceive().AddAsync(Arg.Any<ReadingGoal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveGoalAsync_NullNewGoal_DoesNothing()
    {
        _vm.NewGoal = null;

        await _vm.SaveGoalCommand.ExecuteAsync(null);

        await _goalService.DidNotReceive().AddAsync(Arg.Any<ReadingGoal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveGoalAsync_NewGoal_CallsAddAndLoads()
    {
        _vm.OpenCreateFormCommand.Execute(null);
        _vm.NewGoal!.Title = "Read 50 books";
        var added = new ReadingGoal { Id = Guid.NewGuid(), Title = "Read 50 books" };
        _goalService.AddAsync(Arg.Any<ReadingGoal>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(added));

        await _vm.SaveGoalCommand.ExecuteAsync(null);

        await _goalService.Received(1).AddAsync(Arg.Any<ReadingGoal>(), Arg.Any<CancellationToken>());
        _vm.ShowCreateForm.Should().BeFalse();
        _vm.StatusMessage.Should().Be("Ziel erstellt");
    }

    [Fact]
    public async Task SaveGoalAsync_NewGoalWithGenres_AddsGenresAfterCreate()
    {
        _vm.OpenCreateFormCommand.Execute(null);
        _vm.NewGoal!.Title = "Genre Goal";
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        _vm.SelectedGenreIds = new HashSet<Guid> { g1, g2 };
        _goalService.AddAsync(Arg.Any<ReadingGoal>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult((ReadingGoal)ci[0]));

        await _vm.SaveGoalCommand.ExecuteAsync(null);

        await _goalService.Received(1).AddGenreToGoalAsync(Arg.Any<Guid>(), g1, Arg.Any<CancellationToken>());
        await _goalService.Received(1).AddGenreToGoalAsync(Arg.Any<Guid>(), g2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenEditFormAsync_PopulatesGoalCopyAndGenres()
    {
        var goal = new ReadingGoal
        {
            Id = Guid.NewGuid(),
            Title = "Original",
            Type = GoalType.Pages,
            Target = 1000
        };
        var genre1 = Guid.NewGuid();
        _goalService.GetGoalGenresAsync(goal.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GoalGenre>>(new List<GoalGenre>
            {
                new() { ReadingGoalId = goal.Id, GenreId = genre1 }
            }));

        await _vm.OpenEditFormCommand.ExecuteAsync(goal);

        _vm.IsEditing.Should().BeTrue();
        _vm.ShowCreateForm.Should().BeTrue();
        _vm.NewGoal.Should().NotBeNull();
        _vm.NewGoal!.Id.Should().Be(goal.Id);
        _vm.NewGoal.Title.Should().Be("Original");
        _vm.SelectedGenreIds.Should().Contain(genre1);
    }

    [Fact]
    public async Task SaveGoalAsync_InEditMode_CallsUpdate()
    {
        var goal = new ReadingGoal { Id = Guid.NewGuid(), Title = "Orig" };
        await _vm.OpenEditFormCommand.ExecuteAsync(goal);
        _vm.NewGoal!.Title = "Edited";

        await _vm.SaveGoalCommand.ExecuteAsync(null);

        await _goalService.Received(1).UpdateAsync(Arg.Is<ReadingGoal>(g => g.Title == "Edited"), Arg.Any<CancellationToken>());
        _vm.StatusMessage.Should().Be("Update erfolgreich");
    }

    [Fact]
    public async Task SaveGoalAsync_InEditMode_SyncsGenreDifferences()
    {
        var goalId = Guid.NewGuid();
        var existing = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var added = Guid.NewGuid();
        _goalService.GetGoalGenresAsync(goalId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GoalGenre>>(new List<GoalGenre>
            {
                new() { GenreId = existing },
                new() { GenreId = kept }
            }));
        await _vm.OpenEditFormCommand.ExecuteAsync(new ReadingGoal { Id = goalId, Title = "X" });
        _vm.SelectedGenreIds = new HashSet<Guid> { kept, added };

        await _vm.SaveGoalCommand.ExecuteAsync(null);

        await _goalService.Received(1).RemoveGenreFromGoalAsync(goalId, existing, Arg.Any<CancellationToken>());
        await _goalService.Received(1).AddGenreToGoalAsync(goalId, added, Arg.Any<CancellationToken>());
        await _goalService.DidNotReceive().RemoveGenreFromGoalAsync(goalId, kept, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGoalAsync_CallsService()
    {
        var id = Guid.NewGuid();

        await _vm.DeleteGoalCommand.ExecuteAsync(id);

        await _goalService.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
        _vm.StatusMessage.Should().Be("Erfolgreich gelöscht");
    }

    [Fact]
    public async Task DeleteGoalAsync_OpenFormForSameId_ClosesForm()
    {
        var id = Guid.NewGuid();
        _vm.ShowCreateForm = true;
        _vm.NewGoal = new ReadingGoal { Id = id };

        await _vm.DeleteGoalCommand.ExecuteAsync(id);

        _vm.ShowCreateForm.Should().BeFalse();
        _vm.NewGoal.Should().BeNull();
    }

    [Fact]
    public async Task UpdateGoalAsync_CallsService()
    {
        var goal = new ReadingGoal { Title = "X" };

        await _vm.UpdateGoalCommand.ExecuteAsync(goal);

        await _goalService.Received(1).UpdateAsync(goal, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenExcludeModalAsync_PopulatesState()
    {
        var goal = new ReadingGoal { Id = Guid.NewGuid(), Title = "G" };
        var bookId = Guid.NewGuid();
        var genreId = Guid.NewGuid();
        _bookService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book> { new() { Id = Guid.NewGuid(), Title = "B" } }));
        _goalService.GetExcludedBooksAsync(goal.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GoalExcludedBook>>(new List<GoalExcludedBook> { new() { BookId = bookId } }));
        _goalService.GetGoalGenresAsync(goal.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GoalGenre>>(new List<GoalGenre> { new() { GenreId = genreId } }));

        await _vm.OpenExcludeModalCommand.ExecuteAsync(goal);

        _vm.ShowExcludeModal.Should().BeTrue();
        _vm.ExcludeModalGoal.Should().Be(goal);
        _vm.ExcludedBookIds.Should().Contain(bookId);
        _vm.SelectedGenreIds.Should().Contain(genreId);
        _vm.AllBooks.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToggleBookExclusionAsync_NotExcluded_Adds()
    {
        var goal = new ReadingGoal { Id = Guid.NewGuid() };
        _vm.ExcludeModalGoal = goal;
        _vm.ExcludedBookIds = new HashSet<Guid>();
        var bookId = Guid.NewGuid();

        await _vm.ToggleBookExclusionCommand.ExecuteAsync(bookId);

        await _goalService.Received(1).ExcludeBookFromGoalAsync(goal.Id, bookId, Arg.Any<CancellationToken>());
        _vm.ExcludedBookIds.Should().Contain(bookId);
    }

    [Fact]
    public async Task ToggleBookExclusionAsync_AlreadyExcluded_Removes()
    {
        var goal = new ReadingGoal { Id = Guid.NewGuid() };
        var bookId = Guid.NewGuid();
        _vm.ExcludeModalGoal = goal;
        _vm.ExcludedBookIds = new HashSet<Guid> { bookId };

        await _vm.ToggleBookExclusionCommand.ExecuteAsync(bookId);

        await _goalService.Received(1).IncludeBookInGoalAsync(goal.Id, bookId, Arg.Any<CancellationToken>());
        _vm.ExcludedBookIds.Should().NotContain(bookId);
    }

    [Fact]
    public async Task ToggleBookExclusionAsync_NoGoalSet_NoOp()
    {
        _vm.ExcludeModalGoal = null;

        await _vm.ToggleBookExclusionCommand.ExecuteAsync(Guid.NewGuid());

        await _goalService.DidNotReceive().ExcludeBookFromGoalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleGoalGenreAsync_NotSelected_Adds()
    {
        var goal = new ReadingGoal { Id = Guid.NewGuid() };
        _vm.ExcludeModalGoal = goal;
        _vm.SelectedGenreIds = new HashSet<Guid>();
        var genreId = Guid.NewGuid();

        await _vm.ToggleGoalGenreCommand.ExecuteAsync(genreId);

        await _goalService.Received(1).AddGenreToGoalAsync(goal.Id, genreId, Arg.Any<CancellationToken>());
        _vm.SelectedGenreIds.Should().Contain(genreId);
    }

    [Fact]
    public async Task ToggleGoalGenreAsync_Selected_Removes()
    {
        var goal = new ReadingGoal { Id = Guid.NewGuid() };
        var genreId = Guid.NewGuid();
        _vm.ExcludeModalGoal = goal;
        _vm.SelectedGenreIds = new HashSet<Guid> { genreId };

        await _vm.ToggleGoalGenreCommand.ExecuteAsync(genreId);

        await _goalService.Received(1).RemoveGenreFromGoalAsync(goal.Id, genreId, Arg.Any<CancellationToken>());
        _vm.SelectedGenreIds.Should().NotContain(genreId);
    }

    [Fact]
    public async Task ToggleGoalGenreAsync_NoGoalSet_NoOp()
    {
        _vm.ExcludeModalGoal = null;

        await _vm.ToggleGoalGenreCommand.ExecuteAsync(Guid.NewGuid());

        await _goalService.DidNotReceive().AddGenreToGoalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseExcludeModalAsync_ResetsStateAndReloads()
    {
        _vm.ShowExcludeModal = true;
        _vm.ExcludeModalGoal = new ReadingGoal();
        _vm.AllBooks = new List<Book> { new() };
        _vm.ExcludedBookIds = new HashSet<Guid> { Guid.NewGuid() };

        await _vm.CloseExcludeModalCommand.ExecuteAsync(null);

        _vm.ShowExcludeModal.Should().BeFalse();
        _vm.ExcludeModalGoal.Should().BeNull();
        _vm.AllBooks.Should().BeEmpty();
        _vm.ExcludedBookIds.Should().BeEmpty();
        await _goalService.Received().GetActiveGoalsAsync(Arg.Any<CancellationToken>());
    }
}
