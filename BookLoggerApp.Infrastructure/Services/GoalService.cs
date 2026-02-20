using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing reading goals.
/// </summary>
public class GoalService : IGoalService
{
    private readonly IUnitOfWork _unitOfWork;

    public GoalService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public event EventHandler? GoalsChanged;

    /// <inheritdoc />
    public void NotifyGoalsChanged()
    {
        GoalsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetAllAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetAllAsync();
        return goals.ToList();
    }

    public async Task<ReadingGoal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingGoals.GetByIdAsync(id);
    }

    public async Task<ReadingGoal> AddAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        var result = await _unitOfWork.ReadingGoals.AddAsync(goal);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task UpdateAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        await _unitOfWork.ReadingGoals.UpdateAsync(goal, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var goal = await _unitOfWork.ReadingGoals.GetByIdAsync(id);
        if (goal != null)
        {
            await _unitOfWork.ReadingGoals.DeleteAsync(goal);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetActiveGoalsAsync();
        var goalsList = goals.ToList();

        // Calculate current progress for each goal dynamically
        await CalculateGoalProgressAsync(goalsList, ct);

        return goalsList;
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetCompletedGoalsAsync();
        var goalsList = goals.ToList();

        // Also calculate progress for completed goals to show final values
        await CalculateGoalProgressAsync(goalsList, ct);

        return goalsList;
    }

    private async Task CalculateGoalProgressAsync(List<ReadingGoal> goals, CancellationToken ct)
    {
        if (!goals.Any()) return;

        // Get all books and sessions for calculation
        var books = await _unitOfWork.Books.GetAllAsync(ct);
        var sessions = await _unitOfWork.ReadingSessions.GetAllAsync(ct);

        // Load all exclusions in one query
        var allExclusions = await _unitOfWork.GoalExcludedBooks.GetAllAsync(ct);

        // Load all goal-genre associations in one query
        var allGoalGenres = await _unitOfWork.GoalGenres.GetAllAsync(ct);
        var anyGoalHasGenres = allGoalGenres.Any();

        // Load book-genre associations only if at least one goal uses genre filtering
        IEnumerable<BookGenre>? allBookGenres = null;
        if (anyGoalHasGenres)
        {
            allBookGenres = await _unitOfWork.BookGenres.GetAllAsync(ct);
        }

        // Build genre lookup for populating GoalGenres on each goal (for UI display)
        Dictionary<Guid, Genre>? genreLookup = null;
        if (anyGoalHasGenres)
        {
            var genres = await _unitOfWork.Genres.GetAllAsync(ct);
            genreLookup = genres.ToDictionary(g => g.Id);
        }

        bool anyGoalNewlyCompleted = false;

        foreach (var goal in goals)
        {
            var excludedBookIds = allExclusions
                .Where(e => e.ReadingGoalId == goal.Id)
                .Select(e => e.BookId)
                .ToHashSet();

            // Build genre-matching book IDs (null = no genre filter)
            HashSet<Guid>? genreMatchingBookIds = null;
            var goalGenreIds = allGoalGenres
                .Where(gg => gg.ReadingGoalId == goal.Id)
                .Select(gg => gg.GenreId)
                .ToHashSet();

            if (goalGenreIds.Count > 0 && allBookGenres != null)
            {
                // OR-logic: book matches if it has ANY of the goal's genres
                genreMatchingBookIds = allBookGenres
                    .Where(bg => goalGenreIds.Contains(bg.GenreId))
                    .Select(bg => bg.BookId)
                    .ToHashSet();

                // Populate GoalGenres navigation for UI display
                if (genreLookup != null)
                {
                    goal.GoalGenres = goalGenreIds
                        .Where(gid => genreLookup.ContainsKey(gid))
                        .Select(gid => new GoalGenre
                        {
                            ReadingGoalId = goal.Id,
                            GenreId = gid,
                            Genre = genreLookup[gid]
                        })
                        .ToList();
                }
            }

            goal.Current = goal.Type switch
            {
                GoalType.Books => CalculateBooksProgress(books, goal, excludedBookIds, genreMatchingBookIds),
                GoalType.Pages => CalculatePagesProgress(sessions, goal, excludedBookIds, genreMatchingBookIds),
                GoalType.Minutes => CalculateMinutesProgress(sessions, goal, excludedBookIds, genreMatchingBookIds),
                _ => goal.Current
            };

            // Auto-mark as completed if target is reached
            if (goal.Current >= goal.Target && !goal.IsCompleted)
            {
                goal.IsCompleted = true;
                goal.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.ReadingGoals.UpdateAsync(goal);
                anyGoalNewlyCompleted = true;
            }
        }

        // Only persist changes when a goal was actually newly completed,
        // not on every read. Current is computed dynamically each time.
        if (anyGoalNewlyCompleted)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private int CalculateBooksProgress(IEnumerable<Book> books, ReadingGoal goal, HashSet<Guid> excludedBookIds, HashSet<Guid>? genreMatchingBookIds)
    {
        // Use date-only comparison to include all books completed on the start/end days
        var startDate = goal.StartDate.Date;
        var endDate = goal.EndDate.Date.AddDays(1).AddTicks(-1); // End of day

        return books.Count(b =>
            !excludedBookIds.Contains(b.Id) &&
            (genreMatchingBookIds == null || genreMatchingBookIds.Contains(b.Id)) &&
            b.Status == ReadingStatus.Completed &&
            b.DateCompleted.HasValue &&
            b.DateCompleted.Value >= startDate &&
            b.DateCompleted.Value <= endDate);
    }

    private int CalculatePagesProgress(IEnumerable<ReadingSession> sessions, ReadingGoal goal, HashSet<Guid> excludedBookIds, HashSet<Guid>? genreMatchingBookIds)
    {
        // Use date-only comparison to include all sessions on the start/end days
        var startDate = goal.StartDate.Date;
        var endDate = goal.EndDate.Date.AddDays(1).AddTicks(-1); // End of day

        return sessions
            .Where(s => !excludedBookIds.Contains(s.BookId) &&
                        (genreMatchingBookIds == null || genreMatchingBookIds.Contains(s.BookId)) &&
                        s.EndedAt.HasValue && s.EndedAt.Value >= startDate && s.EndedAt.Value <= endDate)
            .Sum(s => s.PagesRead ?? 0);
    }

    private int CalculateMinutesProgress(IEnumerable<ReadingSession> sessions, ReadingGoal goal, HashSet<Guid> excludedBookIds, HashSet<Guid>? genreMatchingBookIds)
    {
        // Use date-only comparison to include all sessions on the start/end days
        var startDate = goal.StartDate.Date;
        var endDate = goal.EndDate.Date.AddDays(1).AddTicks(-1); // End of day

        return sessions
            .Where(s => !excludedBookIds.Contains(s.BookId) &&
                        (genreMatchingBookIds == null || genreMatchingBookIds.Contains(s.BookId)) &&
                        s.EndedAt.HasValue && s.EndedAt.Value >= startDate && s.EndedAt.Value <= endDate)
            .Sum(s => s.Minutes);
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetGoalsByTypeAsync(GoalType type, CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.FindAsync(g => g.Type == type);
        return goals.ToList();
    }

    public async Task UpdateGoalProgressAsync(Guid goalId, int progress, CancellationToken ct = default)
    {
        var goal = await _unitOfWork.ReadingGoals.GetByIdAsync(goalId);
        if (goal == null)
            throw new EntityNotFoundException(typeof(ReadingGoal), goalId);

        goal.Current = progress;

        // Auto-complete if target reached
        if (goal.Current >= goal.Target)
        {
            goal.IsCompleted = true;
            goal.CompletedAt = DateTime.UtcNow;
        }

        await _unitOfWork.ReadingGoals.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CheckAndCompleteGoalsAsync(CancellationToken ct = default)
    {
        var activeGoals = await _unitOfWork.ReadingGoals.GetActiveGoalsAsync();

        foreach (var goal in activeGoals)
        {
            if (goal.Current >= goal.Target)
            {
                goal.IsCompleted = true;
                goal.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.ReadingGoals.UpdateAsync(goal);
            }
        }

        // Single SaveChanges for all updates
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GoalExcludedBook>> GetExcludedBooksAsync(Guid goalId, CancellationToken ct = default)
    {
        var exclusions = await _unitOfWork.GoalExcludedBooks.FindAsync(e => e.ReadingGoalId == goalId);
        return exclusions.ToList();
    }

    public async Task ExcludeBookFromGoalAsync(Guid goalId, Guid bookId, CancellationToken ct = default)
    {
        var exists = await _unitOfWork.GoalExcludedBooks.ExistsAsync(
            e => e.ReadingGoalId == goalId && e.BookId == bookId);

        if (!exists)
        {
            await _unitOfWork.GoalExcludedBooks.AddAsync(new GoalExcludedBook
            {
                ReadingGoalId = goalId,
                BookId = bookId
            });
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task IncludeBookInGoalAsync(Guid goalId, Guid bookId, CancellationToken ct = default)
    {
        var exclusion = await _unitOfWork.Context.Set<GoalExcludedBook>()
            .FirstOrDefaultAsync(e => e.ReadingGoalId == goalId && e.BookId == bookId, ct);

        if (exclusion != null)
        {
            _unitOfWork.Context.Set<GoalExcludedBook>().Remove(exclusion);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<GoalGenre>> GetGoalGenresAsync(Guid goalId, CancellationToken ct = default)
    {
        var goalGenres = await _unitOfWork.GoalGenres.FindAsync(gg => gg.ReadingGoalId == goalId);
        return goalGenres.ToList();
    }

    public async Task AddGenreToGoalAsync(Guid goalId, Guid genreId, CancellationToken ct = default)
    {
        var exists = await _unitOfWork.GoalGenres.ExistsAsync(
            gg => gg.ReadingGoalId == goalId && gg.GenreId == genreId);

        if (!exists)
        {
            await _unitOfWork.GoalGenres.AddAsync(new GoalGenre
            {
                ReadingGoalId = goalId,
                GenreId = genreId
            });
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveGenreFromGoalAsync(Guid goalId, Guid genreId, CancellationToken ct = default)
    {
        var goalGenre = await _unitOfWork.Context.Set<GoalGenre>()
            .FirstOrDefaultAsync(gg => gg.ReadingGoalId == goalId && gg.GenreId == genreId, ct);

        if (goalGenre != null)
        {
            _unitOfWork.Context.Set<GoalGenre>().Remove(goalGenre);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
