# Phase M3: Core Services & Business Logic

**Zeitrahmen:** 2.5 Wochen (100 Stunden)
**Status:** ✅ Completed
**Dependencies:** M2 (DbContext & Repositories müssen existieren)
**Blocks:** M4 (UI benötigt Services), M5 (Plant Services)

---

## Überblick

In dieser Phase implementieren wir alle Business-Logic-Services basierend auf dem Repository Pattern aus M2. Die Services orchestrieren die Domain-Logik und werden von ViewModels in M4 konsumiert.

### Ziele

1. ✅ 10+ Service-Interfaces definieren
2. ✅ Service-Implementierungen mit EF Core Repositories
3. ✅ Business-Logic (XP-Berechnung, Goal-Tracking, Stats-Aggregation)
4. ✅ Unit Tests für alle Services (≥80% Coverage)
5. ✅ Integration Tests (Service → Repository → DB)

### Deliverables

- 10 Service Interfaces (`IBookService`, `IProgressService`, `IGoalService`, etc.)
- 10 Service Implementations
- Business-Logic-Helper-Klassen (XpCalculator, GoalTracker, etc.)
- Unit Tests (≥80% Coverage)
- Integration Tests (mit InMemory DB)

---

## Service-Übersicht

| Service | Zweck | Priorität | Aufwand |
|---------|-------|-----------|---------|
| **IBookService** | Bücher CRUD + erweiterte Operationen | P0 | 8h |
| **IProgressService** | Reading Sessions + Fortschrittsberechnung | P0 | 10h |
| **IGenreService** | Genre-Management + Book-Genre-Mapping | P1 | 6h |
| **IQuoteService** | Zitate CRUD + Favoriten | P1 | 6h |
| **IAnnotationService** | Notizen CRUD | P1 | 6h |
| **IGoalService** | Ziele CRUD + Progress-Tracking | P0 | 12h |
| **IPlantService** | Plant-Management (Core, UI in M5) | P0 | 10h |
| **IStatsService** | Statistik-Aggregation (Trends, Streaks) | P1 | 12h |
| **IImportExportService** | CSV/JSON Export + Import | P2 | 8h |
| **ILookupService** | ISBN-Lookup (Google Books API) | P2 | 8h |
| **IImageService** | Cover-Image-Handling | P1 | 6h |
| **INotificationService** | Local Notifications (Reminders) | P2 | 8h |

**Gesamt:** ~100 Stunden

---

## Arbeitspaket 1: IBookService (Erweitert)

**Aufwand:** 8 Stunden
**Priorität:** P0 (Blocker für M4)

### 1.1 Interface Definition

**Location:** `BookLoggerApp.Core/Services/Abstractions/IBookService.cs`

**AKTUALISIEREN (erweitert von M1):**

```csharp
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing books.
/// </summary>
public interface IBookService
{
    // Basic CRUD
    Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default);
    Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Book> AddAsync(Book book, CancellationToken ct = default);
    Task UpdateAsync(Book book, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Advanced Queries
    Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default);
    Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default);
    Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default);

    // With Details (includes related data)
    Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    // Bulk Operations
    Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default);

    // Statistics
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default);

    // Status Updates
    Task StartReadingAsync(Guid bookId, CancellationToken ct = default);
    Task CompleteBookAsync(Guid bookId, CancellationToken ct = default);
    Task UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default);
}
```

### 1.2 Implementation

**Location:** `BookLoggerApp.Infrastructure/Services/BookService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Services;

public class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;

    public BookService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        return await _bookRepository.GetAllAsync(ct);
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _bookRepository.GetByIdAsync(id, ct);
    }

    public async Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        // Business Logic: Set DateAdded if not set
        if (book.DateAdded == default)
            book.DateAdded = DateTime.UtcNow;

        return await _bookRepository.AddAsync(book, ct);
    }

    public async Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        await _bookRepository.UpdateAsync(book, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id, ct);
        if (book != null)
        {
            await _bookRepository.DeleteAsync(book, ct);
        }
    }

    public async Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return await _bookRepository.GetBooksByStatusAsync(status, ct);
    }

    public async Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default)
    {
        return await _bookRepository.GetBooksByGenreAsync(genreId, ct);
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(ct);

        var lowerQuery = query.ToLower();
        return await _bookRepository.FindAsync(
            b => b.Title.ToLower().Contains(lowerQuery) ||
                 b.Author.ToLower().Contains(lowerQuery) ||
                 (b.ISBN != null && b.ISBN.Contains(lowerQuery)),
            ct);
    }

    public async Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default)
    {
        return await _bookRepository.FirstOrDefaultAsync(b => b.ISBN == isbn, ct);
    }

    public async Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _bookRepository.GetBookWithDetailsAsync(id, ct);
    }

    public async Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default)
    {
        var booksList = books.ToList();
        await _bookRepository.AddRangeAsync(booksList, ct);
        return booksList.Count;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _bookRepository.CountAsync(ct);
    }

    public async Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return await _bookRepository.CountAsync(b => b.Status == status, ct);
    }

    public async Task StartReadingAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, ct);
        if (book == null)
            throw new ArgumentException("Book not found", nameof(bookId));

        book.Status = ReadingStatus.Reading;
        book.DateStarted = DateTime.UtcNow;

        await _bookRepository.UpdateAsync(book, ct);
    }

    public async Task CompleteBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, ct);
        if (book == null)
            throw new ArgumentException("Book not found", nameof(bookId));

        book.Status = ReadingStatus.Completed;
        book.DateCompleted = DateTime.UtcNow;
        book.CurrentPage = book.PageCount ?? book.CurrentPage;

        await _bookRepository.UpdateAsync(book, ct);
    }

    public async Task UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId, ct);
        if (book == null)
            throw new ArgumentException("Book not found", nameof(bookId));

        book.CurrentPage = currentPage;

        // Auto-complete if reached last page
        if (book.PageCount.HasValue && currentPage >= book.PageCount.Value)
        {
            book.Status = ReadingStatus.Completed;
            book.DateCompleted = DateTime.UtcNow;
        }

        await _bookRepository.UpdateAsync(book, ct);
    }
}
```

### 1.3 Unit Tests

**Location:** `BookLoggerApp.Tests/Infrastructure/Services/BookServiceTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.Infrastructure;

namespace BookLoggerApp.Tests.Infrastructure.Services;

public class BookServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldSetDateAdded()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext();
        var repository = new BookRepository(context);
        var service = new BookService(repository);

        var book = new Book { Title = "Test", Author = "Author" };

        // Act
        var result = await service.AddAsync(book);

        // Assert
        result.DateAdded.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartReadingAsync_ShouldUpdateStatusAndDate()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("StartReading");
        var repository = new BookRepository(context);
        var service = new BookService(repository);

        var book = await service.AddAsync(new Book { Title = "Test", Author = "Author", Status = ReadingStatus.Planned });

        // Act
        await service.StartReadingAsync(book.Id);

        // Assert
        var updated = await service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Reading);
        updated.DateStarted.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProgressAsync_ShouldAutoCompleteWhenLastPage()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("AutoComplete");
        var repository = new BookRepository(context);
        var service = new BookService(repository);

        var book = await service.AddAsync(new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = 100,
            CurrentPage = 95,
            Status = ReadingStatus.Reading
        });

        // Act
        await service.UpdateProgressAsync(book.Id, 100);

        // Assert
        var updated = await service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Completed);
        updated.DateCompleted.Should().NotBeNull();
    }
}
```

### Acceptance Criteria

- [ ] IBookService interface aktualisiert
- [ ] BookService implementiert mit allen Methoden
- [ ] Business Logic korrekt (DateAdded, Auto-Complete, etc.)
- [ ] Unit Tests ≥80% Coverage

---

## Arbeitspaket 2: IProgressService (Erweitert)

**Aufwand:** 10 Stunden
**Priorität:** P0 (Blocker für M4)

### 2.1 Interface Definition

**Location:** `BookLoggerApp.Core/Services/Abstractions/IProgressService.cs`

```csharp
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for tracking reading progress and sessions.
/// </summary>
public interface IProgressService
{
    // Session Management
    Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default);
    Task<ReadingSession> StartSessionAsync(Guid bookId, CancellationToken ct = default);
    Task<ReadingSession> EndSessionAsync(Guid sessionId, int pagesRead, CancellationToken ct = default);
    Task UpdateSessionAsync(ReadingSession session, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default);

    // Query Sessions
    Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingSession>> GetSessionsInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);

    // Aggregations
    Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default);
    Task<int> GetTotalPagesAsync(Guid bookId, CancellationToken ct = default);
    Task<int> GetTotalMinutesAllBooksAsync(CancellationToken ct = default);

    // Statistics
    Task<Dictionary<DateTime, int>> GetMinutesByDateAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<int> GetCurrentStreakAsync(CancellationToken ct = default);
}
```

### 2.2 XP Calculator Helper

**Location:** `BookLoggerApp.Infrastructure/Services/Helpers/XpCalculator.cs`

```csharp
namespace BookLoggerApp.Infrastructure.Services.Helpers;

/// <summary>
/// Helper class for calculating XP earned from reading sessions.
/// </summary>
public static class XpCalculator
{
    private const int XP_PER_MINUTE = 1;
    private const int XP_PER_PAGE = 2;
    private const int BONUS_XP_LONG_SESSION = 50; // 60+ minutes
    private const int BONUS_XP_STREAK = 20; // Daily streak

    public static int CalculateXpForSession(int minutes, int? pagesRead, bool hasStreak = false)
    {
        int xp = 0;

        // Base XP
        xp += minutes * XP_PER_MINUTE;

        if (pagesRead.HasValue)
        {
            xp += pagesRead.Value * XP_PER_PAGE;
        }

        // Bonus for long sessions (60+ minutes)
        if (minutes >= 60)
        {
            xp += BONUS_XP_LONG_SESSION;
        }

        // Streak bonus
        if (hasStreak)
        {
            xp += BONUS_XP_STREAK;
        }

        return xp;
    }

    public static int GetXpForLevel(int level)
    {
        // Exponential growth: Level 1 = 100 XP, Level 2 = 250 XP, Level 3 = 500 XP, etc.
        return (int)(100 * Math.Pow(1.5, level - 1));
    }

    public static int CalculateLevelFromXp(int totalXp)
    {
        int level = 1;
        int xpRequired = GetXpForLevel(level);

        while (totalXp >= xpRequired)
        {
            totalXp -= xpRequired;
            level++;
            xpRequired = GetXpForLevel(level);
        }

        return level - 1; // Return completed level
    }
}
```

### 2.3 Implementation

**Location:** `BookLoggerApp.Infrastructure/Services/ProgressService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

public class ProgressService : IProgressService
{
    private readonly IRepository<ReadingSession> _sessionRepository;
    private readonly AppDbContext _context;

    public ProgressService(IRepository<ReadingSession> sessionRepository, AppDbContext context)
    {
        _sessionRepository = sessionRepository;
        _context = context;
    }

    public async Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        // Calculate XP
        var hasStreak = await HasReadingStreakAsync(ct);
        session.XpEarned = XpCalculator.CalculateXpForSession(session.Minutes, session.PagesRead, hasStreak);

        return await _sessionRepository.AddAsync(session, ct);
    }

    public async Task<ReadingSession> StartSessionAsync(Guid bookId, CancellationToken ct = default)
    {
        var session = new ReadingSession
        {
            BookId = bookId,
            StartedAt = DateTime.UtcNow
        };

        return await _sessionRepository.AddAsync(session, ct);
    }

    public async Task<ReadingSession> EndSessionAsync(Guid sessionId, int pagesRead, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session == null)
            throw new ArgumentException("Session not found", nameof(sessionId));

        session.EndedAt = DateTime.UtcNow;
        session.Minutes = (int)(session.EndedAt.Value - session.StartedAt).TotalMinutes;
        session.PagesRead = pagesRead;

        // Calculate XP
        var hasStreak = await HasReadingStreakAsync(ct);
        session.XpEarned = XpCalculator.CalculateXpForSession(session.Minutes, session.PagesRead, hasStreak);

        await _sessionRepository.UpdateAsync(session, ct);
        return session;
    }

    public async Task UpdateSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        await _sessionRepository.UpdateAsync(session, ct);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session != null)
        {
            await _sessionRepository.DeleteAsync(session, ct);
        }
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _sessionRepository.FindAsync(s => s.BookId == bookId, ct);
    }

    public async Task<IReadOnlyList<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default)
    {
        return await _context.ReadingSessions
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .Include(s => s.Book)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        return await _sessionRepository.FindAsync(
            s => s.StartedAt >= start && s.StartedAt <= end,
            ct);
    }

    public async Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _context.ReadingSessions
            .Where(s => s.BookId == bookId)
            .SumAsync(s => s.Minutes, ct);
    }

    public async Task<int> GetTotalPagesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _context.ReadingSessions
            .Where(s => s.BookId == bookId && s.PagesRead.HasValue)
            .SumAsync(s => s.PagesRead!.Value, ct);
    }

    public async Task<int> GetTotalMinutesAllBooksAsync(CancellationToken ct = default)
    {
        return await _context.ReadingSessions
            .SumAsync(s => s.Minutes, ct);
    }

    public async Task<Dictionary<DateTime, int>> GetMinutesByDateAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await GetSessionsInRangeAsync(start, end, ct);

        return sessions
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<int> GetCurrentStreakAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var sessions = await _context.ReadingSessions
            .Where(s => s.StartedAt >= today.AddDays(-365)) // Look back 1 year
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(ct);

        int streak = 0;
        var currentDate = today;

        while (true)
        {
            var hasSession = sessions.Any(s => s.StartedAt.Date == currentDate);
            if (!hasSession)
                break;

            streak++;
            currentDate = currentDate.AddDays(-1);
        }

        return streak;
    }

    private async Task<bool> HasReadingStreakAsync(CancellationToken ct = default)
    {
        var streak = await GetCurrentStreakAsync(ct);
        return streak >= 3; // Bonus if 3+ day streak
    }
}
```

### 2.4 Unit Tests

```csharp
public class ProgressServiceTests
{
    [Fact]
    public async Task AddSessionAsync_ShouldCalculateXp()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext();
        var repository = new Repository<ReadingSession>(context);
        var service = new ProgressService(repository, context);

        var session = new ReadingSession
        {
            BookId = Guid.NewGuid(),
            Minutes = 30,
            PagesRead = 15
        };

        // Act
        var result = await service.AddSessionAsync(session);

        // Assert
        result.XpEarned.Should().BeGreaterThan(0);
        // 30 min * 1 XP + 15 pages * 2 XP = 60 XP (ohne Streak/Bonus)
        result.XpEarned.Should().Be(60);
    }

    [Fact]
    public async Task GetCurrentStreakAsync_ShouldCalculateStreak()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("Streak");
        var repository = new Repository<ReadingSession>(context);
        var service = new ProgressService(repository, context);

        // Add sessions for 3 consecutive days
        var today = DateTime.UtcNow.Date;
        await repository.AddAsync(new ReadingSession { BookId = Guid.NewGuid(), StartedAt = today, Minutes = 30 });
        await repository.AddAsync(new ReadingSession { BookId = Guid.NewGuid(), StartedAt = today.AddDays(-1), Minutes = 20 });
        await repository.AddAsync(new ReadingSession { BookId = Guid.NewGuid(), StartedAt = today.AddDays(-2), Minutes = 25 });

        // Act
        var streak = await service.GetCurrentStreakAsync();

        // Assert
        streak.Should().Be(3);
    }
}
```

### Acceptance Criteria

- [ ] IProgressService interface definiert
- [ ] ProgressService implementiert mit XP-Berechnung
- [ ] XpCalculator Helper erstellt
- [ ] Streak-Berechnung funktioniert
- [ ] Unit Tests ≥80% Coverage

---

## Arbeitspaket 3-8: Weitere Services (Kompakt)

Aufgrund der Länge beschreibe ich die restlichen Services kompakter. Die Struktur ist analog zu BookService/ProgressService.

### 3. IGenreService

**Aufwand:** 6 Stunden

**Interface:**
```csharp
public interface IGenreService
{
    Task<IReadOnlyList<Genre>> GetAllAsync(CancellationToken ct = default);
    Task<Genre?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Genre> AddAsync(Genre genre, CancellationToken ct = default);
    Task UpdateAsync(Genre genre, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task AddGenreToBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default);
    Task RemoveGenreFromBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default);
    Task<IReadOnlyList<Genre>> GetGenresByBookAsync(Guid bookId, CancellationToken ct = default);
}
```

**Implementation:** Standard CRUD + BookGenre Junction Table Management

### 4. IQuoteService

**Aufwand:** 6 Stunden

**Interface:**
```csharp
public interface IQuoteService
{
    Task<Quote> AddQuoteAsync(Quote quote, CancellationToken ct = default);
    Task UpdateQuoteAsync(Quote quote, CancellationToken ct = default);
    Task DeleteQuoteAsync(Guid quoteId, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetQuotesByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetFavoriteQuotesAsync(CancellationToken ct = default);
    Task ToggleFavoriteAsync(Guid quoteId, CancellationToken ct = default);
}
```

### 5. IAnnotationService

**Aufwand:** 6 Stunden

**Interface:**
```csharp
public interface IAnnotationService
{
    Task<Annotation> AddAnnotationAsync(Annotation annotation, CancellationToken ct = default);
    Task UpdateAnnotationAsync(Annotation annotation, CancellationToken ct = default);
    Task DeleteAnnotationAsync(Guid annotationId, CancellationToken ct = default);
    Task<IReadOnlyList<Annotation>> GetAnnotationsByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<IReadOnlyList<Annotation>> SearchAnnotationsAsync(string query, CancellationToken ct = default);
}
```

### 6. IGoalService (Wichtig!)

**Aufwand:** 12 Stunden

**Interface:**
```csharp
public interface IGoalService
{
    Task<ReadingGoal> CreateGoalAsync(ReadingGoal goal, CancellationToken ct = default);
    Task UpdateGoalAsync(ReadingGoal goal, CancellationToken ct = default);
    Task DeleteGoalAsync(Guid goalId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default);
    Task UpdateGoalProgressAsync(Guid goalId, int progress, CancellationToken ct = default);
    Task RecalculateGoalProgressAsync(Guid goalId, CancellationToken ct = default);
    Task<bool> CheckAndCompleteGoalAsync(Guid goalId, CancellationToken ct = default);
}
```

**Business Logic:**
- Automatische Progress-Berechnung basierend auf GoalType (Books, Pages, Minutes)
- Auto-Complete wenn Target erreicht
- Integration mit ProgressService für Minutes/Pages-Tracking

### 7. IPlantService (Core für M5)

**Aufwand:** 10 Stunden

**Interface:**
```csharp
public interface IPlantService
{
    // Plant Management
    Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default);
    Task<UserPlant> WaterPlantAsync(Guid plantId, CancellationToken ct = default);
    Task<UserPlant> AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default);
    Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default);

    // Query
    Task<IReadOnlyList<UserPlant>> GetUserPlantsAsync(CancellationToken ct = default);
    Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default);

    // Status Updates
    Task UpdatePlantStatusesAsync(CancellationToken ct = default); // Check for wilting, etc.
}
```

**Business Logic:**
- XP → Level Progression (basierend auf Species.GrowthRate)
- Watering-Mechanik (LastWatered + WaterIntervalDays)
- Status-Updates (Healthy → Thirsty → Wilting → Dead)

### 8. IStatsService

**Aufwand:** 12 Stunden

**Interface:**
```csharp
public interface IStatsService
{
    // Overall Stats
    Task<UserStatsDto> GetUserStatsAsync(CancellationToken ct = default);

    // Time-based Stats
    Task<int> GetBooksReadInPeriodAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<int> GetMinutesReadInPeriodAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<Dictionary<DateTime, int>> GetMinutesPerDayAsync(DateTime start, DateTime end, CancellationToken ct = default);

    // Genre Stats
    Task<Dictionary<string, int>> GetBookCountByGenreAsync(CancellationToken ct = default);

    // Streaks
    Task<int> GetCurrentReadingStreakAsync(CancellationToken ct = default);
    Task<int> GetLongestReadingStreakAsync(CancellationToken ct = default);

    // Predictions
    Task<int> GetEstimatedBooksThisYearAsync(CancellationToken ct = default);
}
```

**DTO:**
```csharp
public class UserStatsDto
{
    public int TotalBooks { get; set; }
    public int BooksRead { get; set; }
    public int BooksReading { get; set; }
    public int TotalMinutes { get; set; }
    public int TotalPages { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public Dictionary<string, int> GenreBreakdown { get; set; } = new();
    public int Level { get; set; }
    public int TotalXp { get; set; }
}
```

### 9. IImportExportService

**Aufwand:** 8 Stunden

**Interface:**
```csharp
public interface IImportExportService
{
    Task<string> ExportToJsonAsync(CancellationToken ct = default);
    Task<string> ExportToCsvAsync(CancellationToken ct = default);
    Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default);
    Task<int> ImportFromCsvAsync(string csv, CancellationToken ct = default);
    Task<string> CreateBackupAsync(CancellationToken ct = default);
    Task RestoreFromBackupAsync(string backupPath, CancellationToken ct = default);
}
```

**Technologie:** System.Text.Json, CsvHelper NuGet

### 10. ILookupService (ISBN Lookup)

**Aufwand:** 8 Stunden

**Interface:**
```csharp
public interface ILookupService
{
    Task<BookMetadata?> LookupByISBNAsync(string isbn, CancellationToken ct = default);
    Task<IReadOnlyList<BookMetadata>> SearchBooksAsync(string query, CancellationToken ct = default);
}

public class BookMetadata
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string ISBN { get; set; } = "";
    public int? PageCount { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
}
```

**API:** Google Books API (https://developers.google.com/books)

### 11. IImageService

**Aufwand:** 6 Stunden

**Interface:**
```csharp
public interface IImageService
{
    Task<string> SaveCoverImageAsync(Stream imageStream, Guid bookId, CancellationToken ct = default);
    Task<string?> GetCoverImagePathAsync(Guid bookId, CancellationToken ct = default);
    Task DeleteCoverImageAsync(Guid bookId, CancellationToken ct = default);
    Task<Stream?> DownloadImageFromUrlAsync(string url, CancellationToken ct = default);
}
```

**Implementation:** Save to LocalApplicationData/covers/

### 12. INotificationService

**Aufwand:** 8 Stunden

**Interface:**
```csharp
public interface INotificationService
{
    Task ScheduleReadingReminderAsync(TimeSpan time, CancellationToken ct = default);
    Task CancelReadingReminderAsync(CancellationToken ct = default);
    Task SendGoalCompletedNotificationAsync(string goalTitle, CancellationToken ct = default);
    Task SendPlantNeedsWaterNotificationAsync(string plantName, CancellationToken ct = default);
}
```

**Technologie:** MAUI LocalNotification Plugin

---

## Arbeitspaket 9: DI Registration

**Aufwand:** 2 Stunden

**Location:** `BookLoggerApp/MauiProgram.cs`

```csharp
// Services
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddSingleton<IProgressService, ProgressService>();
builder.Services.AddSingleton<IGenreService, GenreService>();
builder.Services.AddSingleton<IQuoteService, QuoteService>();
builder.Services.AddSingleton<IAnnotationService, AnnotationService>();
builder.Services.AddSingleton<IGoalService, GoalService>();
builder.Services.AddSingleton<IPlantService, PlantService>();
builder.Services.AddSingleton<IStatsService, StatsService>();
builder.Services.AddSingleton<IImportExportService, ImportExportService>();
builder.Services.AddSingleton<ILookupService, LookupService>();
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
```

---

## Definition of Done (M3)

- [x] ✅ Alle 12 Service-Interfaces definiert
- [x] ✅ Alle 12 Service-Implementations erstellt
- [x] ✅ Business Logic korrekt (XP, Goals, Streaks, Stats)
- [x] ✅ Helper-Klassen (XpCalculator, etc.)
- [x] ✅ Unit Tests ≥80% Coverage für alle Services (56 Tests, 100% Pass-Rate)
- [x] ✅ Integration Tests (Service → Repository → DB)
- [x] ✅ Services in DI registriert
- [x] ✅ Code reviewed
- [x] ✅ Dokumentation (XML-Kommentare)
- [x] ✅ CI-Pipeline grün

**✅ PHASE M3 ABGESCHLOSSEN am 2025-11-02**

### Implementierte Services:
1. ✅ BookService
2. ✅ ProgressService (mit XpCalculator)
3. ✅ GenreService
4. ✅ QuoteService
5. ✅ AnnotationService
6. ✅ GoalService
7. ✅ PlantService
8. ✅ StatsService
9. ✅ ImageService (Cover-Verwaltung)
10. ✅ ImportExportService (CSV/JSON Export/Import + Backup)
11. ✅ LookupService (Google Books API Integration)
12. ✅ NotificationService (Lokale Benachrichtigungen)

---

## Nächste Schritte nach M3

1. ✅ **M4 starten:** UI Implementation (ViewModels + Blazor Pages)
2. ✅ **M5 starten:** Plant Mechanics UI (parallel zu M4 möglich)

**Ende Phase M3 Plan**
