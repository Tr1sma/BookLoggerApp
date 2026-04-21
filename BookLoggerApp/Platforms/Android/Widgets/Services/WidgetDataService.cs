using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Platforms.Android.Widgets.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Platforms.Android.Widgets.Services;

/// <summary>
/// Standalone data access for Android widgets.
/// Creates its own DbContext directly (no DI) because AppWidgetProvider
/// runs as a BroadcastReceiver and MAUI's service container may not be available.
/// All queries use AsNoTracking() for read-only, minimal overhead access.
/// </summary>
public static class WidgetDataService
{
    private static AppDbContext CreateDbContext()
    {
        var dbPath = PlatformsDbPath.GetDatabasePath();

        if (!File.Exists(dbPath))
            throw new FileNotFoundException("Database not found. App may not have been launched yet.", dbPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Gets the most recently started book with Status = Reading.
    /// Returns null if no book is currently being read.
    /// </summary>
    public static async Task<CurrentBookWidgetData?> GetCurrentBookDataAsync()
    {
        try
        {
            using var context = CreateDbContext();

            var book = await context.Books
                .AsNoTracking()
                .Where(b => b.Status == ReadingStatus.Reading)
                .OrderByDescending(b => b.DateStarted)
                .FirstOrDefaultAsync();

            if (book is null)
                return null;

            // Resolve full cover image path
            string? fullCoverPath = null;
            if (!string.IsNullOrEmpty(book.CoverImagePath))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var jpgPath = Path.Combine(appData, "covers", $"{book.Id}.jpg");
                var pngPath = Path.Combine(appData, "covers", $"{book.Id}.png");

                if (File.Exists(jpgPath))
                    fullCoverPath = jpgPath;
                else if (File.Exists(pngPath))
                    fullCoverPath = pngPath;
            }

            var totalPages = book.PageCount ?? 0;
            var currentPage = totalPages > 0 ? Math.Min(book.CurrentPage, totalPages) : book.CurrentPage;
            var percentage = totalPages > 0 ? Math.Min(currentPage * 100 / totalPages, 100) : 0;

            return new CurrentBookWidgetData(
                BookId: book.Id,
                Title: book.Title,
                Author: book.Author,
                CurrentPage: currentPage,
                TotalPages: totalPages,
                ProgressPercentage: percentage,
                CoverImagePath: fullCoverPath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calculates the current reading streak (consecutive days with qualifying sessions).
    /// Mirrors the logic in ProgressService.GetCurrentStreakAsync().
    /// </summary>
    public static async Task<StreakWidgetData> GetStreakDataAsync()
    {
        try
        {
            using var context = CreateDbContext();

            var today = DateTime.UtcNow.Date;
            var cutoff = today.AddDays(-365);

            var recentSessions = await context.ReadingSessions
                .AsNoTracking()
                .Where(s => s.StartedAt >= cutoff)
                .ToListAsync();

            var streak = ReadingStreakHelper.CalculateCurrentStreak(recentSessions, today);
            var readToday = recentSessions
                .Where(ReadingStreakHelper.CountsTowardStreak)
                .Any(session => session.StartedAt.Date == today);

            if (streak == 0 && !readToday)
                return new StreakWidgetData(0, false);

            return new StreakWidgetData(streak, readToday);
        }
        catch
        {
            return new StreakWidgetData(0, false);
        }
    }

    /// <summary>
    /// Gets all active reading goals with dynamically calculated progress.
    /// Mirrors the calculation logic from GoalService.CalculateGoalProgressAsync()
    /// because ReadingGoal.Current in the DB is rarely persisted — the app
    /// recalculates it on every display.
    /// </summary>
    public static async Task<List<GoalWidgetData>> GetActiveGoalDataAsync()
    {
        try
        {
            using var context = CreateDbContext();

            var now = DateTime.UtcNow;

            var goals = await context.ReadingGoals
                .AsNoTracking()
                .Where(g => !g.IsCompleted && g.EndDate >= now)
                .OrderBy(g => g.EndDate)
                .ToListAsync();

            if (goals.Count == 0)
                return new List<GoalWidgetData>();

            // Load data needed for progress calculation
            var books = await context.Books.AsNoTracking().ToListAsync();
            var sessions = await context.ReadingSessions.AsNoTracking().ToListAsync();
            var exclusions = await context.GoalExcludedBooks.AsNoTracking().ToListAsync();
            var goalGenres = await context.GoalGenres.AsNoTracking().ToListAsync();
            var bookGenres = await context.BookGenres.AsNoTracking().ToListAsync();

            var result = new List<GoalWidgetData>();

            foreach (var goal in goals)
            {
                var excludedBookIds = exclusions
                    .Where(e => e.ReadingGoalId == goal.Id)
                    .Select(e => e.BookId)
                    .ToHashSet();

                // Genre filter: null means no filter (all books count)
                HashSet<Guid>? genreMatchingBookIds = null;
                var goalGenreIds = goalGenres
                    .Where(gg => gg.ReadingGoalId == goal.Id)
                    .Select(gg => gg.GenreId)
                    .ToHashSet();

                if (goalGenreIds.Count > 0)
                {
                    // OR-logic: book matches if it has ANY of the goal's genres
                    genreMatchingBookIds = bookGenres
                        .Where(bg => goalGenreIds.Contains(bg.GenreId))
                        .Select(bg => bg.BookId)
                        .ToHashSet();
                }

                // Use the shared helper so the widget's goal window matches the app's
                // (ToUniversalTime() is essential — goal dates come from the UI as
                // Kind=Unspecified local dates, and the DateCompleted/EndedAt values
                // are stored as UTC. Skipping the conversion was showing different
                // progress in the widget vs. the main Goals page for non-UTC users.)
                var (startDate, endDate) = BookLoggerApp.Core.Helpers.GoalDateRangeHelper.GetGoalRangeUtc(goal);

                int current = goal.Type switch
                {
                    GoalType.Books => books.Count(b =>
                        !excludedBookIds.Contains(b.Id) &&
                        (genreMatchingBookIds is null || genreMatchingBookIds.Contains(b.Id)) &&
                        b.Status == ReadingStatus.Completed &&
                        b.DateCompleted.HasValue &&
                        b.DateCompleted.Value >= startDate &&
                        b.DateCompleted.Value <= endDate),

                    GoalType.Pages => sessions
                        .Where(s =>
                            !excludedBookIds.Contains(s.BookId) &&
                            (genreMatchingBookIds is null || genreMatchingBookIds.Contains(s.BookId)) &&
                            s.EndedAt.HasValue && s.EndedAt.Value >= startDate && s.EndedAt.Value <= endDate)
                        .Sum(s => s.PagesRead ?? 0),

                    GoalType.Minutes => sessions
                        .Where(s =>
                            !excludedBookIds.Contains(s.BookId) &&
                            (genreMatchingBookIds is null || genreMatchingBookIds.Contains(s.BookId)) &&
                            s.EndedAt.HasValue && s.EndedAt.Value >= startDate && s.EndedAt.Value <= endDate)
                        .Sum(s => s.Minutes),

                    _ => goal.Current
                };

                int percentage = goal.Target > 0 ? (current * 100 / goal.Target) : 0;

                result.Add(new GoalWidgetData(
                    GoalId: goal.Id,
                    Title: goal.Title,
                    GoalType: goal.Type.ToString(),
                    Current: current,
                    Target: goal.Target,
                    ProgressPercentage: percentage,
                    EndDate: goal.EndDate));
            }

            return result;
        }
        catch
        {
            return new List<GoalWidgetData>();
        }
    }

    /// <summary>
    /// Gets a specific goal by ID, falling back to the first active goal if not found.
    /// Used by the goal widget when a specific goal was configured.
    /// </summary>
    public static async Task<GoalWidgetData?> GetGoalDataByIdAsync(Guid? goalId)
    {
        var goals = await GetActiveGoalDataAsync();

        if (goals.Count == 0)
            return null;

        if (goalId.HasValue)
        {
            var specific = goals.FirstOrDefault(g => g.GoalId == goalId.Value);
            if (specific is not null)
                return specific;
        }

        // Fall back to first active goal
        return goals[0];
    }
}
