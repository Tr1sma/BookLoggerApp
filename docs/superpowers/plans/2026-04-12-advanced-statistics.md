# Erweiterte Statistiken — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 12 new statistics organized in a 3-tab system (Übersicht | Trends | Analysen) to the Stats page, powered by Blazor-ApexCharts.

**Architecture:** New `IAdvancedStatsService` with dedicated implementation, two new ViewModels (`StatsTrendsViewModel`, `StatsAnalysesViewModel`), and two new Blazor components (`StatsTrends.razor`, `StatsAnalyses.razor`). Stats.razor becomes a tab container. Existing code stays untouched except for the tab wrapper.

**Tech Stack:** .NET 10, MAUI Blazor Hybrid, Blazor-ApexCharts (NuGet), EF Core SQLite, CommunityToolkit.Mvvm

**Spec:** `docs/superpowers/specs/2026-04-12-advanced-statistics-design.md`

---

## File Map

### New Files
| File | Responsibility |
|---|---|
| `Core/Models/YearStats.cs` | Record for year comparison data |
| `Core/Models/AuthorStats.cs` | Record for top authors data |
| `Core/Services/Abstractions/IAdvancedStatsService.cs` | Interface for all 12 new statistics queries |
| `Infrastructure/Services/AdvancedStatsService.cs` | Implementation using IUnitOfWork |
| `Core/ViewModels/StatsTrendsViewModel.cs` | ViewModel for Trends tab (7 statistics) |
| `Core/ViewModels/StatsAnalysesViewModel.cs` | ViewModel for Analysen tab (5 statistics) |
| `BookLoggerApp/Components/Shared/StatsTrends.razor` | Blazor component for Trends tab |
| `BookLoggerApp/Components/Shared/StatsAnalyses.razor` | Blazor component for Analysen tab |
| `BookLoggerApp/wwwroot/css/stats-advanced.css` | CSS for tab bar and new tab content |
| `Tests/Unit/ViewModels/StatsTrendsViewModelTests.cs` | ViewModel unit tests |
| `Tests/Unit/ViewModels/StatsAnalysesViewModelTests.cs` | ViewModel unit tests |
| `Tests/Services/AdvancedStatsServiceTests.cs` | Service integration tests with InMemory DB |

### Modified Files
| File | Change |
|---|---|
| `BookLoggerApp/BookLoggerApp.csproj` | Add Blazor-ApexCharts NuGet |
| `BookLoggerApp/wwwroot/index.html` | Add CSS link for stats-advanced.css |
| `BookLoggerApp/MauiProgram.cs` | Register IAdvancedStatsService + 2 ViewModels |
| `BookLoggerApp/Components/Pages/Stats.razor` | Wrap existing content in tab system |

---

## Task 1: Infrastructure Setup

**Files:**
- Modify: `BookLoggerApp/BookLoggerApp.csproj`
- Modify: `BookLoggerApp/wwwroot/index.html`
- Create: `BookLoggerApp/wwwroot/css/stats-advanced.css`

- [ ] **Step 1: Add Blazor-ApexCharts NuGet package**

```bash
cd /c/Users/Tristan/source/repos/Tr1sma/BookLoggerApp
dotnet add BookLoggerApp/BookLoggerApp.csproj package Blazor-ApexCharts
```

- [ ] **Step 2: Create stats-advanced.css stub**

Create `BookLoggerApp/wwwroot/css/stats-advanced.css`:
```css
/* ============================================
   Advanced Statistics — Tab System & Charts
   ============================================ */

/* Tab Bar */
.stats-tab-bar {
    display: flex;
    background: var(--bg-secondary);
    border-bottom: 1px solid var(--border-light);
    padding: 0 8px;
    position: sticky;
    top: 0;
    z-index: 10;
}

.stats-tab-btn {
    flex: 1;
    text-align: center;
    padding: 12px 4px;
    color: var(--text-muted);
    font-size: 13px;
    font-weight: 500;
    background: none;
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    transition: color 0.2s ease, border-color 0.2s ease;
}

.stats-tab-btn.active {
    color: var(--primary-color);
    font-weight: 600;
    border-bottom-color: var(--primary-color);
}

/* Tab Content */
.stats-tab-content {
    animation: fadeIn 0.3s ease;
}
```

- [ ] **Step 3: Add CSS link in index.html**

In `BookLoggerApp/wwwroot/index.html`, after the `css/stats.css` link, add:
```html
<link rel="stylesheet" href="css/stats-advanced.css" />
```

- [ ] **Step 4: Verify build**

```bash
dotnet build BookLoggerApp.sln
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add BookLoggerApp/BookLoggerApp.csproj BookLoggerApp/wwwroot/css/stats-advanced.css BookLoggerApp/wwwroot/index.html
git commit -m "chore: add Blazor-ApexCharts and stats-advanced.css infrastructure"
```

---

## Task 2: Helper Types and Service Interface

**Files:**
- Create: `BookLoggerApp.Core/Models/YearStats.cs`
- Create: `BookLoggerApp.Core/Models/AuthorStats.cs`
- Create: `BookLoggerApp.Core/Services/Abstractions/IAdvancedStatsService.cs`

- [ ] **Step 1: Create YearStats record**

Create `BookLoggerApp.Core/Models/YearStats.cs`:
```csharp
namespace BookLoggerApp.Core.Models;

public record YearStats(
    int Year,
    int BooksCompleted,
    int PagesRead,
    int MinutesRead,
    double AverageRating);
```

- [ ] **Step 2: Create AuthorStats record**

Create `BookLoggerApp.Core/Models/AuthorStats.cs`:
```csharp
namespace BookLoggerApp.Core.Models;

public record AuthorStats(
    string Author,
    int BookCount,
    int TotalPages);
```

- [ ] **Step 3: Create IAdvancedStatsService interface**

Create `BookLoggerApp.Core/Services/Abstractions/IAdvancedStatsService.cs`:
```csharp
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAdvancedStatsService
{
    // Trends tab
    Task<Dictionary<DateTime, int>> GetReadingHeatmapAsync(int year, CancellationToken ct = default);
    Task<Dictionary<DayOfWeek, int>> GetWeekdayDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetTimeOfDayDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetSessionLengthDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<int, int>> GetMonthlyVolumeAsync(int year, CancellationToken ct = default);
    Task<(double Current, double Previous)> GetReadingSpeedTrendAsync(CancellationToken ct = default);
    Task<(double CurrentAvg, double PreviousAvg)> GetAverageFinishTimeTrendAsync(CancellationToken ct = default);

    // Analysen tab
    Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default);
    Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default);
    Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default);
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add BookLoggerApp.Core/Models/YearStats.cs BookLoggerApp.Core/Models/AuthorStats.cs BookLoggerApp.Core/Services/Abstractions/IAdvancedStatsService.cs
git commit -m "feat: add IAdvancedStatsService interface and helper types"
```

---

## Task 3: AdvancedStatsService — Trends Methods

**Files:**
- Create: `BookLoggerApp.Infrastructure/Services/AdvancedStatsService.cs`
- Create: `BookLoggerApp.Tests/Services/AdvancedStatsServiceTests.cs`

- [ ] **Step 1: Write failing tests for Trends methods**

Create `BookLoggerApp.Tests/Services/AdvancedStatsServiceTests.cs`:
```csharp
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class AdvancedStatsServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly AdvancedStatsService _service;

    public AdvancedStatsServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new AdvancedStatsService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // --- Heatmap ---

    [Fact]
    public async Task GetReadingHeatmapAsync_ReturnsMinutesPerDay()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "A" });
        await _context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today, Minutes = 30
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today, Minutes = 20
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today.AddDays(-1), Minutes = 45
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetReadingHeatmapAsync(today.Year);

        // Assert
        result[today].Should().Be(50);
        result[today.AddDays(-1)].Should().Be(45);
    }

    [Fact]
    public async Task GetReadingHeatmapAsync_EmptyData_ReturnsEmptyDictionary()
    {
        var result = await _service.GetReadingHeatmapAsync(2026);
        result.Should().BeEmpty();
    }

    // --- Weekday Distribution ---

    [Fact]
    public async Task GetWeekdayDistributionAsync_GroupsByDayOfWeek()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "A" });
        await _context.SaveChangesAsync();

        // Find a known Monday
        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = monday, Minutes = 60
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = monday.AddDays(2), Minutes = 30 // Wednesday
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetWeekdayDistributionAsync();

        // Assert
        result[DayOfWeek.Monday].Should().Be(60);
        result[DayOfWeek.Wednesday].Should().Be(30);
    }

    // --- Time of Day ---

    [Fact]
    public async Task GetTimeOfDayDistributionAsync_CategorizesByHour()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "A" });
        await _context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today.AddHours(7), Minutes = 30 // Morning
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today.AddHours(20), Minutes = 60 // Evening
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTimeOfDayDistributionAsync();

        // Assert
        result["Morning"].Should().Be(30);
        result["Evening"].Should().Be(60);
    }

    // --- Session Length Distribution ---

    [Fact]
    public async Task GetSessionLengthDistributionAsync_BucketsCorrectly()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "A" });
        await _context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today, Minutes = 10 // <15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today, Minutes = 25 // 15-30
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today, Minutes = 45 // 30-60
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSessionLengthDistributionAsync();

        // Assert
        result["<15"].Should().Be(1);
        result["15-30"].Should().Be(1);
        result["30-60"].Should().Be(1);
    }

    // --- Monthly Volume ---

    [Fact]
    public async Task GetMonthlyVolumeAsync_CountsCompletedBooksPerMonth()
    {
        // Arrange
        var book1 = new Book
        {
            Title = "Book1", Author = "A",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };
        var book2 = new Book
        {
            Title = "Book2", Author = "B",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        var book3 = new Book
        {
            Title = "Book3", Author = "C",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.Books.AddAsync(book3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMonthlyVolumeAsync(2026);

        // Assert
        result[1].Should().Be(2); // January
        result[3].Should().Be(1); // March
    }

    // --- Reading Speed ---

    [Fact]
    public async Task GetReadingSpeedTrendAsync_CalculatesPagesPerHour()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "A" });
        await _context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        // Current month: 60 pages in 120 minutes = 30 pages/hour
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today, Minutes = 120, PagesRead = 60
        });
        // Previous month: 40 pages in 80 minutes = 30 pages/hour
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id, StartedAt = today.AddDays(-35), Minutes = 80, PagesRead = 40
        });
        await _context.SaveChangesAsync();

        // Act
        var (current, previous) = await _service.GetReadingSpeedTrendAsync();

        // Assert
        current.Should().Be(30);
        previous.Should().Be(30);
    }

    // --- Average Finish Time ---

    [Fact]
    public async Task GetAverageFinishTimeTrendAsync_CalculatesDaysPerBook()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var book1 = new Book
        {
            Title = "Book1", Author = "A",
            Status = ReadingStatus.Completed,
            DateStarted = today.AddDays(-10),
            DateCompleted = today
        };
        var book2 = new Book
        {
            Title = "Book2", Author = "B",
            Status = ReadingStatus.Completed,
            DateStarted = today.AddDays(-6),
            DateCompleted = today
        };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _context.SaveChangesAsync();

        // Act
        var (currentAvg, _) = await _service.GetAverageFinishTimeTrendAsync();

        // Assert
        currentAvg.Should().Be(8); // (10 + 6) / 2
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~AdvancedStatsService" -v n
```
Expected: FAIL — `AdvancedStatsService` class not found.

- [ ] **Step 3: Implement AdvancedStatsService — Trends methods**

Create `BookLoggerApp.Infrastructure/Services/AdvancedStatsService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

public class AdvancedStatsService : IAdvancedStatsService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdvancedStatsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Dictionary<DateTime, int>> GetReadingHeatmapAsync(int year, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var sessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(startDate, endDate);

        return sessions
            .Where(s => s.Minutes > 0)
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));
    }

    public async Task<Dictionary<DayOfWeek, int>> GetWeekdayDistributionAsync(CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(DateTime.MinValue, DateTime.UtcNow);

        var grouped = sessions
            .Where(s => s.Minutes > 0)
            .GroupBy(s => s.StartedAt.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));

        return grouped;
    }

    public async Task<Dictionary<string, int>> GetTimeOfDayDistributionAsync(CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(DateTime.MinValue, DateTime.UtcNow);

        var result = new Dictionary<string, int>
        {
            ["Morning"] = 0,
            ["Afternoon"] = 0,
            ["Evening"] = 0,
            ["Night"] = 0
        };

        foreach (var session in sessions.Where(s => s.Minutes > 0))
        {
            int hour = session.StartedAt.Hour;
            string bucket = hour switch
            {
                >= 5 and < 12 => "Morning",
                >= 12 and < 17 => "Afternoon",
                >= 17 and < 22 => "Evening",
                _ => "Night"
            };
            result[bucket] += session.Minutes;
        }

        return result;
    }

    public async Task<Dictionary<string, int>> GetSessionLengthDistributionAsync(CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(DateTime.MinValue, DateTime.UtcNow);

        var result = new Dictionary<string, int>
        {
            ["<15"] = 0,
            ["15-30"] = 0,
            ["30-60"] = 0,
            ["1-2h"] = 0,
            [">2h"] = 0
        };

        foreach (var session in sessions.Where(s => s.Minutes > 0))
        {
            string bucket = session.Minutes switch
            {
                < 15 => "<15",
                < 30 => "15-30",
                < 60 => "30-60",
                < 120 => "1-2h",
                _ => ">2h"
            };
            result[bucket]++;
        }

        return result;
    }

    public async Task<Dictionary<int, int>> GetMonthlyVolumeAsync(int year, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();

        return books
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateCompleted.HasValue
                     && b.DateCompleted.Value.Year == year)
            .GroupBy(b => b.DateCompleted!.Value.Month)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<(double Current, double Previous)> GetReadingSpeedTrendAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var currentStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousStart = currentStart.AddMonths(-1);

        var allSessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(previousStart, now);

        var currentSessions = allSessions
            .Where(s => s.StartedAt >= currentStart && s.Minutes > 0 && s.PagesRead.HasValue && s.PagesRead > 0);
        var previousSessions = allSessions
            .Where(s => s.StartedAt >= previousStart && s.StartedAt < currentStart && s.Minutes > 0 && s.PagesRead.HasValue && s.PagesRead > 0);

        double current = CalculateSpeed(currentSessions);
        double previous = CalculateSpeed(previousSessions);

        return (current, previous);
    }

    private static double CalculateSpeed(IEnumerable<ReadingSession> sessions)
    {
        int totalPages = sessions.Sum(s => s.PagesRead ?? 0);
        int totalMinutes = sessions.Sum(s => s.Minutes);
        if (totalMinutes == 0) return 0;
        return Math.Round((double)totalPages / totalMinutes * 60, 0);
    }

    public async Task<(double CurrentAvg, double PreviousAvg)> GetAverageFinishTimeTrendAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        var books = await _unitOfWork.Books.GetAllAsync();
        var completed = books
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateStarted.HasValue
                     && b.DateCompleted.HasValue)
            .ToList();

        var currentBooks = completed.Where(b => b.DateCompleted >= thirtyDaysAgo);
        var previousBooks = completed.Where(b => b.DateCompleted >= sixtyDaysAgo && b.DateCompleted < thirtyDaysAgo);

        double currentAvg = CalculateAvgDays(currentBooks);
        double previousAvg = CalculateAvgDays(previousBooks);

        return (currentAvg, previousAvg);
    }

    private static double CalculateAvgDays(IEnumerable<Book> books)
    {
        var list = books.ToList();
        if (list.Count == 0) return 0;
        return Math.Round(list.Average(b => (b.DateCompleted!.Value - b.DateStarted!.Value).TotalDays), 1);
    }

    // Analysen methods — implemented in Task 4
    public Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default)
        => throw new NotImplementedException();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~AdvancedStatsService" -v n
```
Expected: All Trends tests PASS.

- [ ] **Step 5: Commit**

```bash
git add BookLoggerApp.Infrastructure/Services/AdvancedStatsService.cs BookLoggerApp.Tests/Services/AdvancedStatsServiceTests.cs
git commit -m "feat: implement AdvancedStatsService trends methods with tests"
```

---

## Task 4: AdvancedStatsService — Analysen Methods

**Files:**
- Modify: `BookLoggerApp.Infrastructure/Services/AdvancedStatsService.cs`
- Modify: `BookLoggerApp.Tests/Services/AdvancedStatsServiceTests.cs`

- [ ] **Step 1: Add Analysen tests to AdvancedStatsServiceTests.cs**

Append to test file:
```csharp
    // --- Year Comparison ---

    [Fact]
    public async Task GetYearComparisonAsync_ReturnsStatsForBothYears()
    {
        // Arrange
        var book1 = new Book
        {
            Title = "2025 Book", Author = "A", PageCount = 300,
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var book2 = new Book
        {
            Title = "2026 Book", Author = "B", PageCount = 200,
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CharactersRating = 4, PlotRating = 4
        };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _context.SaveChangesAsync();

        // Add a reading session for 2026 book
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book2.Id,
            StartedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Minutes = 120
        });
        await _context.SaveChangesAsync();

        // Act
        var (y1, y2) = await _service.GetYearComparisonAsync(2025, 2026);

        // Assert
        y1.Year.Should().Be(2025);
        y1.BooksCompleted.Should().Be(1);
        y2.Year.Should().Be(2026);
        y2.BooksCompleted.Should().Be(1);
        y2.MinutesRead.Should().Be(120);
    }

    // --- Genre Radar ---

    [Fact]
    public async Task GetGenreRadarDataAsync_ReturnsTopGenres()
    {
        // Arrange
        var genre1 = new Genre { Name = "Fantasy", Icon = "🧙" };
        var genre2 = new Genre { Name = "Thriller", Icon = "🔍" };
        await _unitOfWork.Genres.AddAsync(genre1);
        await _unitOfWork.Genres.AddAsync(genre2);

        var book1 = new Book { Title = "B1", Author = "A", Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "B2", Author = "B", Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _context.SaveChangesAsync();

        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book1.Id, GenreId = genre1.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book2.Id, GenreId = genre1.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book2.Id, GenreId = genre2.Id });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetGenreRadarDataAsync();

        // Assert
        result["Fantasy"].Should().Be(2);
        result["Thriller"].Should().Be(1);
    }

    // --- Completion Rate ---

    [Fact]
    public async Task GetCompletionRateAsync_CountsCorrectly()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book { Title = "Done1", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Done2", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Quit", Author = "C", Status = ReadingStatus.Abandoned });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Reading", Author = "D", Status = ReadingStatus.Reading });
        await _context.SaveChangesAsync();

        // Act
        var (completed, abandoned) = await _service.GetCompletionRateAsync();

        // Assert
        completed.Should().Be(2);
        abandoned.Should().Be(1);
    }

    // --- Page Count Distribution ---

    [Fact]
    public async Task GetPageCountDistributionAsync_BucketsCorrectly()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book { Title = "Short", Author = "A", PageCount = 150, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Medium", Author = "B", PageCount = 350, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Long", Author = "C", PageCount = 500, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Epic", Author = "D", PageCount = 800, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPageCountDistributionAsync();

        // Assert
        result["<200"].Should().Be(1);
        result["200-400"].Should().Be(1);
        result["400-600"].Should().Be(1);
        result[">600"].Should().Be(1);
    }

    // --- Top Authors ---

    [Fact]
    public async Task GetTopAuthorsAsync_RanksByBookCount()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book { Title = "B1", Author = "Sanderson", PageCount = 400, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "B2", Author = "Sanderson", PageCount = 600, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _unitOfWork.Books.AddAsync(new Book { Title = "B3", Author = "King", PageCount = 300, Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopAuthorsAsync(5);

        // Assert
        result.Should().HaveCount(2);
        result[0].Author.Should().Be("Sanderson");
        result[0].BookCount.Should().Be(2);
        result[0].TotalPages.Should().Be(1000);
        result[1].Author.Should().Be("King");
    }
```

- [ ] **Step 2: Run tests to verify Analysen tests fail**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~AdvancedStatsService" -v n
```
Expected: Trends tests PASS, Analysen tests FAIL with `NotImplementedException`.

- [ ] **Step 3: Replace NotImplementedException stubs with real implementations**

In `AdvancedStatsService.cs`, replace the 5 `throw new NotImplementedException()` stubs:

```csharp
    public async Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default)
    {
        var stats1 = await BuildYearStatsAsync(year1);
        var stats2 = await BuildYearStatsAsync(year2);
        return (stats1, stats2);
    }

    private async Task<YearStats> BuildYearStatsAsync(int year)
    {
        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var books = await _unitOfWork.Books.GetAllAsync();
        var completedInYear = books
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateCompleted.HasValue
                     && b.DateCompleted.Value.Year == year)
            .ToList();

        int booksCompleted = completedInYear.Count;
        int pagesRead = completedInYear.Sum(b => b.PageCount ?? 0);

        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(startDate, endDate);
        int minutesRead = sessions.Sum(s => s.Minutes);

        double avgRating = completedInYear
            .Where(b => b.AverageRating.HasValue)
            .Select(b => b.AverageRating!.Value)
            .DefaultIfEmpty(0)
            .Average();

        return new YearStats(year, booksCompleted, pagesRead, minutesRead, Math.Round(avgRating, 1));
    }

    public async Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();
        var completedBookIds = books
            .Where(b => b.Status == ReadingStatus.Completed)
            .Select(b => b.Id)
            .ToHashSet();

        var bookGenres = await _unitOfWork.BookGenres.GetAllAsync();
        var genres = await _unitOfWork.Genres.GetAllAsync();
        var genreLookup = genres.ToDictionary(g => g.Id, g => g.Name);

        var genreCounts = bookGenres
            .Where(bg => completedBookIds.Contains(bg.BookId) && genreLookup.ContainsKey(bg.GenreId))
            .GroupBy(bg => genreLookup[bg.GenreId])
            .ToDictionary(g => g.Key, g => g.Count())
            .OrderByDescending(kv => kv.Value)
            .Take(maxGenres)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return genreCounts;
    }

    public async Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();
        int completed = books.Count(b => b.Status == ReadingStatus.Completed);
        int abandoned = books.Count(b => b.Status == ReadingStatus.Abandoned);
        return (completed, abandoned);
    }

    public async Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();
        var withPages = books
            .Where(b => b.Status == ReadingStatus.Completed && b.PageCount.HasValue);

        var result = new Dictionary<string, int>
        {
            ["<200"] = 0,
            ["200-400"] = 0,
            ["400-600"] = 0,
            [">600"] = 0
        };

        foreach (var book in withPages)
        {
            string bucket = book.PageCount!.Value switch
            {
                < 200 => "<200",
                < 400 => "200-400",
                < 600 => "400-600",
                _ => ">600"
            };
            result[bucket]++;
        }

        return result;
    }

    public async Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();

        return books
            .Where(b => b.Status == ReadingStatus.Completed && !string.IsNullOrWhiteSpace(b.Author))
            .GroupBy(b => b.Author)
            .Select(g => new AuthorStats(
                g.Key,
                g.Count(),
                g.Sum(b => b.PageCount ?? 0)))
            .OrderByDescending(a => a.BookCount)
            .ThenByDescending(a => a.TotalPages)
            .Take(count)
            .ToList();
    }
```

- [ ] **Step 4: Run all AdvancedStatsService tests**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~AdvancedStatsService" -v n
```
Expected: All tests PASS.

- [ ] **Step 5: Run full test suite to check for regressions**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
```
Expected: All existing tests still PASS.

- [ ] **Step 6: Commit**

```bash
git add BookLoggerApp.Infrastructure/Services/AdvancedStatsService.cs BookLoggerApp.Tests/Services/AdvancedStatsServiceTests.cs
git commit -m "feat: implement AdvancedStatsService analysen methods with tests"
```

---

## Task 5: StatsTrendsViewModel

**Files:**
- Create: `BookLoggerApp.Core/ViewModels/StatsTrendsViewModel.cs`
- Create: `BookLoggerApp.Tests/Unit/ViewModels/StatsTrendsViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel tests**

Create `BookLoggerApp.Tests/Unit/ViewModels/StatsTrendsViewModelTests.cs`:
```csharp
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class StatsTrendsViewModelTests
{
    private readonly IAdvancedStatsService _service;
    private readonly StatsTrendsViewModel _viewModel;

    public StatsTrendsViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _service = Substitute.For<IAdvancedStatsService>();

        // Default returns
        _service.GetReadingHeatmapAsync(Arg.Any<int>()).Returns(new Dictionary<DateTime, int>());
        _service.GetWeekdayDistributionAsync().Returns(new Dictionary<DayOfWeek, int>());
        _service.GetTimeOfDayDistributionAsync().Returns(new Dictionary<string, int>
        {
            ["Morning"] = 0, ["Afternoon"] = 0, ["Evening"] = 0, ["Night"] = 0
        });
        _service.GetSessionLengthDistributionAsync().Returns(new Dictionary<string, int>());
        _service.GetMonthlyVolumeAsync(Arg.Any<int>()).Returns(new Dictionary<int, int>());
        _service.GetReadingSpeedTrendAsync().Returns((0.0, 0.0));
        _service.GetAverageFinishTimeTrendAsync().Returns((0.0, 0.0));

        _viewModel = new StatsTrendsViewModel(_service);
    }

    [Fact]
    public async Task LoadAsync_PopulatesHeatmapData()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        _service.GetReadingHeatmapAsync(today.Year)
            .Returns(new Dictionary<DateTime, int> { [today] = 45 });

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.HeatmapData.Should().ContainKey(today);
        _viewModel.HeatmapData[today].Should().Be(45);
    }

    [Fact]
    public async Task LoadAsync_PopulatesTimeOfDayLabel()
    {
        // Arrange
        _service.GetTimeOfDayDistributionAsync().Returns(new Dictionary<string, int>
        {
            ["Morning"] = 10, ["Afternoon"] = 5, ["Evening"] = 80, ["Night"] = 30
        });

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.TimeOfDayLabel.Should().Be("Abendleser 🌙");
    }

    [Fact]
    public async Task LoadAsync_PopulatesReadingSpeed()
    {
        // Arrange
        _service.GetReadingSpeedTrendAsync().Returns((32.0, 28.0));

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.CurrentSpeed.Should().Be(32);
        _viewModel.SpeedDifference.Should().Be(4);
    }

    [Fact]
    public async Task ChangeHeatmapYear_ReloadsHeatmapData()
    {
        // Arrange
        _service.GetReadingHeatmapAsync(2025)
            .Returns(new Dictionary<DateTime, int> { [new DateTime(2025, 6, 1)] = 30 });

        // Act
        await _viewModel.ChangeHeatmapYearCommand.ExecuteAsync(2025);

        // Assert
        _viewModel.HeatmapYear.Should().Be(2025);
        _viewModel.HeatmapData.Should().ContainKey(new DateTime(2025, 6, 1));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~StatsTrendsViewModel" -v n
```
Expected: FAIL — `StatsTrendsViewModel` not found.

- [ ] **Step 3: Implement StatsTrendsViewModel**

Create `BookLoggerApp.Core/ViewModels/StatsTrendsViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsTrendsViewModel : ViewModelBase
{
    private readonly IAdvancedStatsService _advancedStatsService;

    public StatsTrendsViewModel(IAdvancedStatsService advancedStatsService)
    {
        _advancedStatsService = advancedStatsService;
        _heatmapYear = DateTime.UtcNow.Year;
    }

    // Heatmap
    [ObservableProperty]
    private int _heatmapYear;

    [ObservableProperty]
    private Dictionary<DateTime, int> _heatmapData = new();

    // Weekday distribution
    [ObservableProperty]
    private Dictionary<DayOfWeek, int> _weekdayData = new();

    // Time of day
    [ObservableProperty]
    private Dictionary<string, int> _timeOfDayData = new();

    [ObservableProperty]
    private string _timeOfDayLabel = "";

    // Session lengths
    [ObservableProperty]
    private Dictionary<string, int> _sessionLengthData = new();

    [ObservableProperty]
    private double _averageSessionMinutes;

    // Monthly volume
    [ObservableProperty]
    private Dictionary<int, int> _monthlyVolumeData = new();

    // Reading speed
    [ObservableProperty]
    private double _currentSpeed;

    [ObservableProperty]
    private double _speedDifference;

    // Average finish time
    [ObservableProperty]
    private double _currentFinishDays;

    [ObservableProperty]
    private double _finishDaysDifference;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            var heatmapTask = _advancedStatsService.GetReadingHeatmapAsync(HeatmapYear);
            var weekdayTask = _advancedStatsService.GetWeekdayDistributionAsync();
            var timeOfDayTask = _advancedStatsService.GetTimeOfDayDistributionAsync();
            var sessionLengthTask = _advancedStatsService.GetSessionLengthDistributionAsync();
            var monthlyTask = _advancedStatsService.GetMonthlyVolumeAsync(DateTime.UtcNow.Year);
            var speedTask = _advancedStatsService.GetReadingSpeedTrendAsync();
            var finishTask = _advancedStatsService.GetAverageFinishTimeTrendAsync();

            await Task.WhenAll(heatmapTask, weekdayTask, timeOfDayTask, sessionLengthTask, monthlyTask, speedTask, finishTask);

            HeatmapData = heatmapTask.Result;
            WeekdayData = weekdayTask.Result;
            TimeOfDayData = timeOfDayTask.Result;
            TimeOfDayLabel = DetermineTimeOfDayLabel(TimeOfDayData);
            SessionLengthData = sessionLengthTask.Result;
            AverageSessionMinutes = CalculateAverageSession(SessionLengthData);
            MonthlyVolumeData = monthlyTask.Result;

            var (currentSpeed, previousSpeed) = speedTask.Result;
            CurrentSpeed = currentSpeed;
            SpeedDifference = Math.Round(currentSpeed - previousSpeed, 0);

            var (currentFinish, previousFinish) = finishTask.Result;
            CurrentFinishDays = currentFinish;
            FinishDaysDifference = Math.Round(currentFinish - previousFinish, 1);
        }, "Fehler beim Laden der Trend-Statistiken");
    }

    [RelayCommand]
    public async Task ChangeHeatmapYearAsync(int year)
    {
        HeatmapYear = year;
        HeatmapData = await _advancedStatsService.GetReadingHeatmapAsync(year);
    }

    private static string DetermineTimeOfDayLabel(Dictionary<string, int> data)
    {
        if (data.Count == 0 || data.Values.All(v => v == 0))
            return "";

        string dominant = data.MaxBy(kv => kv.Value).Key;
        return dominant switch
        {
            "Morning" => "Frühleser 🌅",
            "Afternoon" => "Tagträumer ☀️",
            "Evening" => "Abendleser 🌙",
            "Night" => "Nachteule 🦉",
            _ => ""
        };
    }

    private static double CalculateAverageSession(Dictionary<string, int> data)
    {
        // Weighted average using bucket midpoints
        var bucketMidpoints = new Dictionary<string, double>
        {
            ["<15"] = 7.5, ["15-30"] = 22.5, ["30-60"] = 45, ["1-2h"] = 90, [">2h"] = 150
        };

        double totalMinutes = 0;
        int totalSessions = 0;
        foreach (var (bucket, count) in data)
        {
            if (bucketMidpoints.TryGetValue(bucket, out double midpoint))
            {
                totalMinutes += midpoint * count;
                totalSessions += count;
            }
        }

        return totalSessions > 0 ? Math.Round(totalMinutes / totalSessions, 0) : 0;
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~StatsTrendsViewModel" -v n
```
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add BookLoggerApp.Core/ViewModels/StatsTrendsViewModel.cs BookLoggerApp.Tests/Unit/ViewModels/StatsTrendsViewModelTests.cs
git commit -m "feat: add StatsTrendsViewModel with tests"
```

---

## Task 6: StatsAnalysesViewModel

**Files:**
- Create: `BookLoggerApp.Core/ViewModels/StatsAnalysesViewModel.cs`
- Create: `BookLoggerApp.Tests/Unit/ViewModels/StatsAnalysesViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel tests**

Create `BookLoggerApp.Tests/Unit/ViewModels/StatsAnalysesViewModelTests.cs`:
```csharp
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class StatsAnalysesViewModelTests
{
    private readonly IAdvancedStatsService _service;
    private readonly IStatsService _statsService;
    private readonly StatsAnalysesViewModel _viewModel;

    public StatsAnalysesViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _service = Substitute.For<IAdvancedStatsService>();
        _statsService = Substitute.For<IStatsService>();

        // Default returns
        _service.GetYearComparisonAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns((new YearStats(2025, 0, 0, 0, 0), new YearStats(2026, 0, 0, 0, 0)));
        _service.GetGenreRadarDataAsync(Arg.Any<int>()).Returns(new Dictionary<string, int>());
        _service.GetCompletionRateAsync().Returns((0, 0));
        _service.GetPageCountDistributionAsync().Returns(new Dictionary<string, int>());
        _service.GetTopAuthorsAsync(Arg.Any<int>()).Returns(new List<AuthorStats>());
        _statsService.GetActiveReadingPeriodsAsync().Returns(new List<(int, int)>());

        _viewModel = new StatsAnalysesViewModel(_service, _statsService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesCompletionRate()
    {
        // Arrange
        _service.GetCompletionRateAsync().Returns((24, 5));

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.CompletedCount.Should().Be(24);
        _viewModel.AbandonedCount.Should().Be(5);
        _viewModel.CompletionPercentage.Should().BeApproximately(82.8, 0.1);
    }

    [Fact]
    public async Task LoadAsync_PopulatesTopAuthors()
    {
        // Arrange
        _service.GetTopAuthorsAsync(5).Returns(new List<AuthorStats>
        {
            new("Sanderson", 5, 4200),
            new("King", 3, 1800)
        });

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.TopAuthors.Should().HaveCount(2);
        _viewModel.TopAuthors[0].Author.Should().Be("Sanderson");
    }

    [Fact]
    public async Task ChangeComparisonYearsCommand_UpdatesYearComparison()
    {
        // Arrange
        _service.GetYearComparisonAsync(2024, 2025)
            .Returns((new YearStats(2024, 10, 3000, 5000, 4.0), new YearStats(2025, 12, 3400, 5200, 3.8)));

        // Act
        await _viewModel.ChangeComparisonYearsCommand.ExecuteAsync((2024, 2025));

        // Assert
        _viewModel.Year1Stats.BooksCompleted.Should().Be(10);
        _viewModel.Year2Stats.BooksCompleted.Should().Be(12);
    }

    [Fact]
    public async Task LoadAsync_PopulatesAvailableYears()
    {
        // Arrange
        _statsService.GetActiveReadingPeriodsAsync().Returns(new List<(int, int)>
        {
            (2025, 1), (2025, 3), (2026, 1), (2026, 2)
        });

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.AvailableYears.Should().Contain(2025);
        _viewModel.AvailableYears.Should().Contain(2026);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~StatsAnalysesViewModel" -v n
```
Expected: FAIL — `StatsAnalysesViewModel` not found.

- [ ] **Step 3: Implement StatsAnalysesViewModel**

Create `BookLoggerApp.Core/ViewModels/StatsAnalysesViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsAnalysesViewModel : ViewModelBase
{
    private readonly IAdvancedStatsService _advancedStatsService;
    private readonly IStatsService _statsService;

    public StatsAnalysesViewModel(IAdvancedStatsService advancedStatsService, IStatsService statsService)
    {
        _advancedStatsService = advancedStatsService;
        _statsService = statsService;
    }

    // Year comparison
    [ObservableProperty]
    private YearStats _year1Stats = new(0, 0, 0, 0, 0);

    [ObservableProperty]
    private YearStats _year2Stats = new(0, 0, 0, 0, 0);

    [ObservableProperty]
    private List<int> _availableYears = new();

    [ObservableProperty]
    private int _selectedYear1;

    [ObservableProperty]
    private int _selectedYear2;

    // Genre radar
    [ObservableProperty]
    private Dictionary<string, int> _genreRadarData = new();

    // Completion rate
    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _abandonedCount;

    [ObservableProperty]
    private double _completionPercentage;

    // Page count distribution
    [ObservableProperty]
    private Dictionary<string, int> _pageCountData = new();

    // Top authors
    [ObservableProperty]
    private List<AuthorStats> _topAuthors = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // Load available years first
            var periods = await _statsService.GetActiveReadingPeriodsAsync();
            AvailableYears = periods.Select(p => p.Year).Distinct().OrderByDescending(y => y).ToList();

            if (AvailableYears.Count >= 2)
            {
                SelectedYear1 = AvailableYears[1]; // second most recent
                SelectedYear2 = AvailableYears[0]; // most recent
            }
            else if (AvailableYears.Count == 1)
            {
                SelectedYear1 = AvailableYears[0] - 1;
                SelectedYear2 = AvailableYears[0];
            }
            else
            {
                SelectedYear1 = DateTime.UtcNow.Year - 1;
                SelectedYear2 = DateTime.UtcNow.Year;
            }

            var yearTask = _advancedStatsService.GetYearComparisonAsync(SelectedYear1, SelectedYear2);
            var genreTask = _advancedStatsService.GetGenreRadarDataAsync();
            var completionTask = _advancedStatsService.GetCompletionRateAsync();
            var pageCountTask = _advancedStatsService.GetPageCountDistributionAsync();
            var authorsTask = _advancedStatsService.GetTopAuthorsAsync(5);

            await Task.WhenAll(yearTask, genreTask, completionTask, pageCountTask, authorsTask);

            var (y1, y2) = yearTask.Result;
            Year1Stats = y1;
            Year2Stats = y2;

            GenreRadarData = genreTask.Result;

            var (completed, abandoned) = completionTask.Result;
            CompletedCount = completed;
            AbandonedCount = abandoned;
            int total = completed + abandoned;
            CompletionPercentage = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0;

            PageCountData = pageCountTask.Result;
            TopAuthors = authorsTask.Result;
        }, "Fehler beim Laden der Analyse-Statistiken");
    }

    [RelayCommand]
    public async Task ChangeComparisonYearsAsync((int year1, int year2) years)
    {
        SelectedYear1 = years.year1;
        SelectedYear2 = years.year2;
        var (y1, y2) = await _advancedStatsService.GetYearComparisonAsync(years.year1, years.year2);
        Year1Stats = y1;
        Year2Stats = y2;
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~StatsAnalysesViewModel" -v n
```
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add BookLoggerApp.Core/ViewModels/StatsAnalysesViewModel.cs BookLoggerApp.Tests/Unit/ViewModels/StatsAnalysesViewModelTests.cs
git commit -m "feat: add StatsAnalysesViewModel with tests"
```

---

## Task 7: DI Registration

**Files:**
- Modify: `BookLoggerApp/MauiProgram.cs`

- [ ] **Step 1: Register IAdvancedStatsService in RegisterBusinessServices()**

In `MauiProgram.cs`, in the `RegisterBusinessServices()` method (around line 128, after `IStatsService` registration), add:
```csharp
builder.Services.AddTransient<IAdvancedStatsService, AdvancedStatsService>();
```

Add the using directive at the top of the file if not already present:
```csharp
using BookLoggerApp.Infrastructure.Services;
```

- [ ] **Step 2: Register ViewModels in RegisterViewModels()**

In `MauiProgram.cs`, in the `RegisterViewModels()` method (around line 173, after `StatsViewModel` registration), add:
```csharp
builder.Services.AddTransient<StatsTrendsViewModel>();
builder.Services.AddTransient<StatsAnalysesViewModel>();
```

- [ ] **Step 3: Verify build**

```bash
dotnet build BookLoggerApp.sln
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add BookLoggerApp/MauiProgram.cs
git commit -m "feat: register IAdvancedStatsService and new ViewModels in DI"
```

---

## Task 8: Tab System in Stats.razor

**Files:**
- Modify: `BookLoggerApp/Components/Pages/Stats.razor`

- [ ] **Step 1: Add tab state variable to @code block**

In `Stats.razor`, add at the top of the `@code` block (after existing field declarations around line 371):
```csharp
private int activeTab = 0;

private string TabClass(int tab) => tab == activeTab ? "stats-tab-btn active" : "stats-tab-btn";

private void SetTab(int tab)
{
    activeTab = tab;
    StateHasChanged();
}
```

- [ ] **Step 2: Insert tab bar HTML after the CSS link and before the container**

After `<HeadContent>...</HeadContent>` and before the `<div class="level-overview-container">`, insert:
```razor
<div class="stats-tab-bar">
    <button class="@TabClass(0)" @onclick="() => SetTab(0)">Übersicht</button>
    <button class="@TabClass(1)" @onclick="() => SetTab(1)">Trends</button>
    <button class="@TabClass(2)" @onclick="() => SetTab(2)">Analysen</button>
</div>
```

- [ ] **Step 3: Wrap existing content in tab conditional**

Wrap the existing `<div class="level-overview-container">...</div>` (everything from the container to before the share modal) in:
```razor
@if (activeTab == 0)
{
    <div class="level-overview-container">
        @* ... all existing content unchanged ... *@
    </div>
}
else if (activeTab == 1)
{
    <StatsTrends />
}
else if (activeTab == 2)
{
    <StatsAnalyses />
}
```

The share modal stays outside the tab conditional (it's an overlay).

- [ ] **Step 4: Verify build** (will have warnings about missing StatsTrends/StatsAnalyses components — OK for now)

```bash
dotnet build BookLoggerApp/BookLoggerApp.csproj
```

- [ ] **Step 5: Commit**

```bash
git add BookLoggerApp/Components/Pages/Stats.razor
git commit -m "feat: add tab system to Stats page"
```

---

## Task 9: StatsTrends.razor Component

**Files:**
- Create: `BookLoggerApp/Components/Shared/StatsTrends.razor`

- [ ] **Step 1: Create StatsTrends.razor**

Create `BookLoggerApp/Components/Shared/StatsTrends.razor`:
```razor
@inject BookLoggerApp.Core.ViewModels.StatsTrendsViewModel ViewModel

<div class="stats-tab-content level-overview-container">
    @if (ViewModel.IsBusy)
    {
        <div class="loading-state">
            <div class="loading-spinner"></div>
        </div>
    }
    else if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
    {
        <div class="error-state">
            <p>@ViewModel.ErrorMessage</p>
        </div>
    }
    else
    {
        @* 1. Heatmap Calendar *@
        <div class="advanced-stats-section">
            <div class="advanced-stats-card">
                <div class="advanced-stats-header">
                    <h3>Lese-Kalender</h3>
                    <div class="heatmap-year-nav">
                        <button class="year-nav-btn" @onclick="() => ChangeYear(-1)">&lt;</button>
                        <span>@ViewModel.HeatmapYear</span>
                        <button class="year-nav-btn" @onclick="() => ChangeYear(1)">&gt;</button>
                    </div>
                </div>
                <ApexChart TItem="HeatmapPoint"
                           Title=""
                           Options="heatmapOptions">
                    @foreach (var weekGroup in GetHeatmapSeries())
                    {
                        <ApexPointSeries TItem="HeatmapPoint"
                                         Items="weekGroup.Points"
                                         SeriesType="SeriesType.Heatmap"
                                         Name="@weekGroup.Name"
                                         XValue="@(p => p.Week)"
                                         YAggregate="@(p => p.Sum(x => x.Minutes))" />
                    }
                </ApexChart>
            </div>
        </div>

        @* 2. Weekday Distribution *@
        <div class="advanced-stats-section">
            <div class="advanced-stats-card">
                <h3>Wochentag-Verteilung</h3>
                <ApexChart TItem="WeekdayPoint"
                           Title=""
                           Options="barOptions">
                    <ApexPointSeries TItem="WeekdayPoint"
                                     Items="GetWeekdayPoints()"
                                     SeriesType="SeriesType.Bar"
                                     Name="Minuten"
                                     XValue="@(p => p.Day)"
                                     YValue="@(p => p.Minutes)" />
                </ApexChart>
            </div>
        </div>

        @* 3. Time of Day *@
        <div class="advanced-stats-section">
            <div class="advanced-stats-card">
                <div class="advanced-stats-header">
                    <h3>Tageszeit</h3>
                    @if (!string.IsNullOrEmpty(ViewModel.TimeOfDayLabel))
                    {
                        <span class="time-of-day-badge">@ViewModel.TimeOfDayLabel</span>
                    }
                </div>
                <div class="time-of-day-grid">
                    @foreach (var (key, label, emoji) in timeOfDayBuckets)
                    {
                        int total = ViewModel.TimeOfDayData.Values.Sum();
                        int minutes = ViewModel.TimeOfDayData.GetValueOrDefault(key, 0);
                        int percent = total > 0 ? (int)Math.Round((double)minutes / total * 100) : 0;
                        bool isDominant = ViewModel.TimeOfDayData.Count > 0
                            && ViewModel.TimeOfDayData.MaxBy(kv => kv.Value).Key == key;

                        <div class="time-of-day-card @(isDominant ? "dominant" : "")">
                            <div class="time-of-day-emoji">@emoji</div>
                            <div class="time-of-day-label">@label</div>
                            <div class="time-of-day-value">@percent%</div>
                        </div>
                    }
                </div>
            </div>
        </div>

        @* 4. Session Lengths *@
        <div class="advanced-stats-section">
            <div class="advanced-stats-card">
                <h3>Session-Längen</h3>
                <p class="advanced-stats-subtitle">Durchschnitt: @ViewModel.AverageSessionMinutes Min.</p>
                <ApexChart TItem="SessionBucketPoint"
                           Title=""
                           Options="barOptions">
                    <ApexPointSeries TItem="SessionBucketPoint"
                                     Items="GetSessionBucketPoints()"
                                     SeriesType="SeriesType.Bar"
                                     Name="Sessions"
                                     XValue="@(p => p.Bucket)"
                                     YValue="@(p => p.Count)" />
                </ApexChart>
            </div>
        </div>

        @* 5. Monthly Volume *@
        <div class="advanced-stats-section">
            <div class="advanced-stats-card">
                <h3>Monatlicher Verlauf</h3>
                <p class="advanced-stats-subtitle">Bücher abgeschlossen pro Monat</p>
                <ApexChart TItem="MonthlyPoint"
                           Title=""
                           Options="barOptions">
                    <ApexPointSeries TItem="MonthlyPoint"
                                     Items="GetMonthlyPoints()"
                                     SeriesType="SeriesType.Bar"
                                     Name="Bücher"
                                     XValue="@(p => p.Month)"
                                     YValue="@(p => p.Count)" />
                </ApexChart>
            </div>
        </div>

        @* 6+7. Speed + Finish Time *@
        <div class="advanced-stats-section">
            <div class="compact-stats-row">
                <div class="compact-stat-card">
                    <div class="compact-stat-title">Geschwindigkeit</div>
                    <div class="compact-stat-value">@ViewModel.CurrentSpeed</div>
                    <div class="compact-stat-unit">Seiten/Stunde</div>
                    @if (ViewModel.SpeedDifference != 0)
                    {
                        <div class="compact-stat-trend @(ViewModel.SpeedDifference > 0 ? "positive" : "negative")">
                            @(ViewModel.SpeedDifference > 0 ? "+" : "")@ViewModel.SpeedDifference vs. letzter Monat
                        </div>
                    }
                </div>
                <div class="compact-stat-card">
                    <div class="compact-stat-title">Lesedauer</div>
                    <div class="compact-stat-value">@ViewModel.CurrentFinishDays</div>
                    <div class="compact-stat-unit">Tage pro Buch</div>
                    @if (ViewModel.FinishDaysDifference != 0)
                    {
                        <div class="compact-stat-trend @(ViewModel.FinishDaysDifference < 0 ? "positive" : "negative")">
                            @(ViewModel.FinishDaysDifference > 0 ? "+" : "")@ViewModel.FinishDaysDifference vs. letzter Monat
                        </div>
                    }
                </div>
            </div>
        </div>
    }
</div>

@code {
    // Chart options — configured once, reused
    private ApexChartOptions<HeatmapPoint> heatmapOptions = default!;
    private ApexChartOptions<WeekdayPoint> barOptions = default!;

    private static readonly (string Key, string Label, string Emoji)[] timeOfDayBuckets =
    {
        ("Morning", "Morgens", "🌅"),
        ("Afternoon", "Mittags", "☀️"),
        ("Evening", "Abends", "🌙"),
        ("Night", "Nachts", "🦉")
    };

    protected override async Task OnInitializedAsync()
    {
        InitChartOptions();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void InitChartOptions()
    {
        // Base theme matching BookHeart dark mode
        heatmapOptions = new ApexChartOptions<HeatmapPoint>
        {
            Chart = new Chart { Background = "transparent", ForeColor = "#C9B5A0" },
            Colors = new List<string> { "#D4A574" },
            PlotOptions = new PlotOptions
            {
                Heatmap = new PlotOptionsHeatmap
                {
                    ColorScale = new HeatmapColorScale
                    {
                        Ranges = new List<HeatmapColorScaleRange>
                        {
                            new() { From = 0, To = 0, Color = "#251E15", Name = "Keine" },
                            new() { From = 1, To = 15, Color = "#3D3126", Name = "Wenig" },
                            new() { From = 16, To = 30, Color = "#8B7355", Name = "Etwas" },
                            new() { From = 31, To = 60, Color = "#C9A97F", Name = "Gut" },
                            new() { From = 61, To = 1440, Color = "#D4A574", Name = "Viel" },
                        }
                    }
                }
            },
            Grid = new Grid { Show = false },
            Tooltip = new Tooltip { Theme = "dark" }
        };

        barOptions = new ApexChartOptions<WeekdayPoint>
        {
            Chart = new Chart { Background = "transparent", ForeColor = "#C9B5A0" },
            Colors = new List<string> { "#D4A574" },
            Grid = new Grid { BorderColor = "#3D3126" },
            Tooltip = new Tooltip { Theme = "dark" },
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { BorderRadius = 4 }
            }
        };
    }

    private async Task ChangeYear(int delta)
    {
        await ViewModel.ChangeHeatmapYearCommand.ExecuteAsync(ViewModel.HeatmapYear + delta);
    }

    // Data mapping helpers for ApexCharts
    private List<WeekdayPoint> GetWeekdayPoints()
    {
        string[] dayNames = { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        DayOfWeek[] order = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                              DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        return order.Select((d, i) => new WeekdayPoint(dayNames[i], ViewModel.WeekdayData.GetValueOrDefault(d, 0))).ToList();
    }

    private List<SessionBucketPoint> GetSessionBucketPoints()
    {
        string[] buckets = { "<15", "15-30", "30-60", "1-2h", ">2h" };
        return buckets.Select(b => new SessionBucketPoint(b, ViewModel.SessionLengthData.GetValueOrDefault(b, 0))).ToList();
    }

    private List<MonthlyPoint> GetMonthlyPoints()
    {
        string[] monthNames = { "Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" };
        return Enumerable.Range(1, 12).Select(m =>
            new MonthlyPoint(monthNames[m - 1], ViewModel.MonthlyVolumeData.GetValueOrDefault(m, 0))).ToList();
    }

    private List<HeatmapWeekGroup> GetHeatmapSeries()
    {
        // Group heatmap data into 7 rows (Mon-Sun) with week columns
        string[] dayNames = { "So", "Sa", "Fr", "Do", "Mi", "Di", "Mo" }; // reversed for heatmap display
        var groups = new List<HeatmapWeekGroup>();

        foreach (int dayIndex in Enumerable.Range(0, 7))
        {
            var dayOfWeek = dayIndex switch { 0 => DayOfWeek.Sunday, 1 => DayOfWeek.Saturday, 2 => DayOfWeek.Friday,
                3 => DayOfWeek.Thursday, 4 => DayOfWeek.Wednesday, 5 => DayOfWeek.Tuesday, _ => DayOfWeek.Monday };

            var points = ViewModel.HeatmapData
                .Where(kv => kv.Key.DayOfWeek == dayOfWeek)
                .Select(kv =>
                {
                    var jan1 = new DateTime(ViewModel.HeatmapYear, 1, 1);
                    int weekNum = (kv.Key.DayOfYear - 1) / 7 + 1;
                    return new HeatmapPoint(weekNum.ToString(), kv.Value);
                })
                .OrderBy(p => p.Week)
                .ToList();

            // Fill missing weeks with 0
            for (int w = 1; w <= 53; w++)
            {
                if (!points.Any(p => p.Week == w.ToString()))
                    points.Add(new HeatmapPoint(w.ToString(), 0));
            }

            groups.Add(new HeatmapWeekGroup(dayNames[dayIndex], points.OrderBy(p => int.Parse(p.Week)).ToList()));
        }

        return groups;
    }

    // Chart data point records
    private record WeekdayPoint(string Day, int Minutes);
    private record SessionBucketPoint(string Bucket, int Count);
    private record MonthlyPoint(string Month, int Count);
    private record HeatmapPoint(string Week, int Minutes);
    private record HeatmapWeekGroup(string Name, List<HeatmapPoint> Points);
}
```

**Note:** The exact ApexCharts Blazor API may need minor adjustments during implementation based on the installed version. Check the Blazor-ApexCharts documentation (`context7` MCP) for the exact `ApexPointSeries` generic parameters if there are compile errors.

- [ ] **Step 2: Verify build**

```bash
dotnet build BookLoggerApp/BookLoggerApp.csproj
```

- [ ] **Step 3: Commit**

```bash
git add BookLoggerApp/Components/Shared/StatsTrends.razor
git commit -m "feat: add StatsTrends.razor component with ApexCharts"
```

---

## Task 10: StatsAnalyses.razor Component

**Files:**
- Create: `BookLoggerApp/Components/Shared/StatsAnalyses.razor`

- [ ] **Step 1: Create StatsAnalyses.razor**

Create `BookLoggerApp/Components/Shared/StatsAnalyses.razor`:
```razor
@inject BookLoggerApp.Core.ViewModels.StatsAnalysesViewModel ViewModel

<div class="stats-tab-content level-overview-container">
    @if (ViewModel.IsBusy)
    {
        <div class="loading-state">
            <div class="loading-spinner"></div>
        </div>
    }
    else if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
    {
        <div class="error-state">
            <p>@ViewModel.ErrorMessage</p>
        </div>
    }
    else
    {
        @* 1. Year Comparison *@
        <div class="advanced-stats-section">
            <div class="advanced-stats-card">
                <h3>Jahresvergleich</h3>
                <div class="year-selector">
                    @foreach (var year in ViewModel.AvailableYears.Take(4))
                    {
                        <button class="year-chip @(year == ViewModel.SelectedYear1 ? "active-year1" : "") @(year == ViewModel.SelectedYear2 ? "active-year2" : "")"
                                @onclick="() => SelectYear(year)">@year</button>
                    }
                </div>
                <div class="year-comparison-rows">
                    @foreach (var (label, val1, val2) in GetComparisonRows())
                    {
                        int max = Math.Max(1, Math.Max(val1, val2));
                        <div class="comparison-row">
                            <div class="comparison-label">@label</div>
                            <div class="comparison-bars">
                                <div class="comparison-bar year1" style="width: @((int)((double)val1 / max * 100))%">
                                    <span>@FormatValue(label, val1)</span>
                                </div>
                                <div class="comparison-bar year2" style="width: @((int)((double)val2 / max * 100))%">
                                    <span>@FormatValue(label, val2)</span>
                                </div>
                            </div>
                        </div>
                    }
                </div>
            </div>
        </div>

        @* 2. Genre Radar *@
        @if (ViewModel.GenreRadarData.Count > 0)
        {
            <div class="advanced-stats-section">
                <div class="advanced-stats-card">
                    <h3>Genre-Radar</h3>
                    <ApexChart TItem="GenrePoint"
                               Title=""
                               Options="radarOptions">
                        <ApexPointSeries TItem="GenrePoint"
                                         Items="GetGenrePoints()"
                                         SeriesType="SeriesType.Radar"
                                         Name="Bücher"
                                         XValue="@(p => p.Genre)"
                                         YAggregate="@(p => p.Sum(x => x.Count))" />
                    </ApexChart>
                </div>
            </div>
        }

        @* 3. Completion Rate *@
        @if (ViewModel.CompletedCount + ViewModel.AbandonedCount > 0)
        {
            <div class="advanced-stats-section">
                <div class="advanced-stats-card">
                    <h3>Abschlussquote</h3>
                    <div class="completion-rate-container">
                        <ApexChart TItem="CompletionPoint"
                                   Title=""
                                   Options="donutOptions">
                            <ApexPointSeries TItem="CompletionPoint"
                                             Items="GetCompletionPoints()"
                                             SeriesType="SeriesType.Donut"
                                             Name="Status"
                                             XValue="@(p => p.Label)"
                                             YAggregate="@(p => p.Sum(x => x.Value))" />
                        </ApexChart>
                    </div>
                </div>
            </div>
        }

        @* 4. Page Count Distribution *@
        @if (ViewModel.PageCountData.Values.Any(v => v > 0))
        {
            <div class="advanced-stats-section">
                <div class="advanced-stats-card">
                    <h3>Buchlängen-Vorliebe</h3>
                    <div class="page-count-bars">
                        @{
                            int maxCount = ViewModel.PageCountData.Values.DefaultIfEmpty(1).Max();
                            var bucketLabels = new Dictionary<string, string>
                            {
                                ["<200"] = "Kurz (<200 S.)",
                                ["200-400"] = "Mittel (200–400 S.)",
                                ["400-600"] = "Lang (400–600 S.)",
                                [">600"] = "Episch (>600 S.)"
                            };
                        }
                        @foreach (var bucket in new[] { "<200", "200-400", "400-600", ">600" })
                        {
                            int count = ViewModel.PageCountData.GetValueOrDefault(bucket, 0);
                            int widthPercent = maxCount > 0 ? (int)((double)count / maxCount * 100) : 0;
                            <div class="page-count-row">
                                <div class="page-count-row-header">
                                    <span class="page-count-label">@bucketLabels[bucket]</span>
                                    <span class="page-count-count">@count Bücher</span>
                                </div>
                                <div class="page-count-bar-bg">
                                    <div class="page-count-bar-fill" style="width: @widthPercent%"></div>
                                </div>
                            </div>
                        }
                    </div>
                </div>
            </div>
        }

        @* 5. Top Authors *@
        @if (ViewModel.TopAuthors.Count > 0)
        {
            <div class="advanced-stats-section">
                <div class="advanced-stats-card">
                    <h3>Meistgelesene Autoren</h3>
                    <div class="top-authors-list">
                        @for (int i = 0; i < ViewModel.TopAuthors.Count; i++)
                        {
                            var author = ViewModel.TopAuthors[i];
                            <div class="top-author-item">
                                <div class="author-rank @(i == 0 ? "rank-first" : "")">@(i + 1)</div>
                                <div class="author-info">
                                    <div class="author-name">@author.Author</div>
                                    <div class="author-detail">@author.BookCount Bücher · @author.TotalPages.ToString("N0") Seiten</div>
                                </div>
                                <div class="author-count">@author.BookCount</div>
                            </div>
                            @if (i < ViewModel.TopAuthors.Count - 1)
                            {
                                <div class="author-divider"></div>
                            }
                        }
                    </div>
                </div>
            </div>
        }
    }
</div>

@code {
    private ApexChartOptions<GenrePoint> radarOptions = default!;
    private ApexChartOptions<CompletionPoint> donutOptions = default!;
    private int? yearSelectState; // null=none, tracks which year slot is being selected

    protected override async Task OnInitializedAsync()
    {
        InitChartOptions();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void InitChartOptions()
    {
        radarOptions = new ApexChartOptions<GenrePoint>
        {
            Chart = new Chart { Background = "transparent", ForeColor = "#C9B5A0" },
            Colors = new List<string> { "#D4A574" },
            Fill = new Fill { Opacity = new List<double> { 0.15 } },
            Stroke = new Stroke { Colors = new List<string> { "#D4A574" }, Width = new List<int> { 2 } },
            Grid = new Grid { Show = false },
            Tooltip = new Tooltip { Theme = "dark" }
        };

        donutOptions = new ApexChartOptions<CompletionPoint>
        {
            Chart = new Chart { Background = "transparent", ForeColor = "#C9B5A0" },
            Colors = new List<string> { "#D4A574", "#A67874" },
            Labels = new List<string> { "Abgeschlossen", "Abgebrochen" },
            PlotOptions = new PlotOptions
            {
                Pie = new PlotOptionsPie
                {
                    Donut = new PlotOptionsDonut
                    {
                        Labels = new DonutLabels
                        {
                            Show = true,
                            Total = new DonutLabelTotal
                            {
                                Show = true,
                                Label = "Gesamt",
                                Color = "#C9B5A0"
                            }
                        }
                    }
                }
            },
            Tooltip = new Tooltip { Theme = "dark" }
        };
    }

    private async Task SelectYear(int year)
    {
        // Toggle between selecting year1 and year2
        if (year == ViewModel.SelectedYear1 || year == ViewModel.SelectedYear2)
            return;

        // Replace the year that is further from the clicked one
        int newYear1 = ViewModel.SelectedYear1;
        int newYear2 = ViewModel.SelectedYear2;

        if (yearSelectState == null)
        {
            newYear1 = year;
            yearSelectState = 1;
        }
        else
        {
            newYear2 = year;
            yearSelectState = null;
        }

        if (newYear1 > newYear2)
            (newYear1, newYear2) = (newYear2, newYear1);

        await ViewModel.ChangeComparisonYearsCommand.ExecuteAsync((newYear1, newYear2));
    }

    private List<(string Label, int Val1, int Val2)> GetComparisonRows()
    {
        var y1 = ViewModel.Year1Stats;
        var y2 = ViewModel.Year2Stats;
        return new()
        {
            ("Bücher", y1.BooksCompleted, y2.BooksCompleted),
            ("Seiten", y1.PagesRead, y2.PagesRead),
            ("Stunden", y1.MinutesRead / 60, y2.MinutesRead / 60),
        };
    }

    private static string FormatValue(string label, int value) => label switch
    {
        "Seiten" when value >= 1000 => $"{value / 1000.0:F1}k",
        "Stunden" => $"{value}h",
        _ => value.ToString()
    };

    private List<GenrePoint> GetGenrePoints() =>
        ViewModel.GenreRadarData.Select(kv => new GenrePoint(kv.Key, kv.Value)).ToList();

    private List<CompletionPoint> GetCompletionPoints() => new()
    {
        new("Abgeschlossen", ViewModel.CompletedCount),
        new("Abgebrochen", ViewModel.AbandonedCount)
    };

    private record GenrePoint(string Genre, int Count);
    private record CompletionPoint(string Label, int Value);
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build BookLoggerApp/BookLoggerApp.csproj
```

- [ ] **Step 3: Commit**

```bash
git add BookLoggerApp/Components/Shared/StatsAnalyses.razor
git commit -m "feat: add StatsAnalyses.razor component with ApexCharts"
```

---

## Task 11: Complete stats-advanced.css

**Files:**
- Modify: `BookLoggerApp/wwwroot/css/stats-advanced.css`

- [ ] **Step 1: Add full CSS for all advanced stats components**

Replace the stub CSS from Task 1 with the complete stylesheet. The full CSS should include styles for:
- `.advanced-stats-section` — section spacing and animation
- `.advanced-stats-card` — card styling matching existing `var(--card-bg)`
- `.advanced-stats-header` — flex row for title + controls
- `.advanced-stats-subtitle` — muted subtitle text
- `.heatmap-year-nav` / `.year-nav-btn` — heatmap year navigation
- `.time-of-day-grid` / `.time-of-day-card` / `.dominant` — tageszeit grid
- `.compact-stats-row` / `.compact-stat-card` — speed + finish time cards
- `.compact-stat-trend` / `.positive` / `.negative` — trend indicators
- `.year-selector` / `.year-chip` / `.active-year1` / `.active-year2` — year comparison chips
- `.year-comparison-rows` / `.comparison-row` / `.comparison-bar` / `.year1` / `.year2` — comparison bars
- `.completion-rate-container` — donut chart sizing
- `.page-count-bars` / `.page-count-row` / `.page-count-bar-bg` / `.page-count-bar-fill` — page distribution
- `.top-authors-list` / `.top-author-item` / `.author-rank` / `.rank-first` / `.author-divider` — author ranking
- Mobile responsive breakpoints (640px, 400px)

All colors MUST use CSS variables from `app.css` (`--primary-color`, `--accent-color`, `--card-bg`, `--bg-primary`, `--text-primary`, `--text-secondary`, `--text-muted`, `--border-light`, `--status-completed`, `--status-abandoned`).

Reference the approved mockups in `.superpowers/brainstorm/367-1776016350/content/` for visual targets.

- [ ] **Step 2: Verify build**

```bash
dotnet build BookLoggerApp/BookLoggerApp.csproj
```

- [ ] **Step 3: Commit**

```bash
git add BookLoggerApp/wwwroot/css/stats-advanced.css
git commit -m "feat: add complete stats-advanced.css styles"
```

---

## Task 12: Final Verification

- [ ] **Step 1: Run full test suite**

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -v n
```
Expected: All tests PASS, no regressions.

- [ ] **Step 2: Build entire solution**

```bash
dotnet build BookLoggerApp.sln
```
Expected: Build succeeded, no warnings related to new code.

- [ ] **Step 3: Manual testing on Android**

Deploy to Android emulator or device and verify:
1. Stats page opens normally → Tab bar visible at top
2. "Übersicht" tab → all existing stats render correctly (no regressions)
3. "Trends" tab → all 7 stats load and display:
   - Heatmap renders with warm brown color scale
   - Year navigation works
   - Weekday bars render
   - Time of day cards show percentages + fun label
   - Session length histogram renders
   - Monthly volume shows current year
   - Speed and finish time cards show values + trends
4. "Analysen" tab → all 5 stats load and display:
   - Year comparison shows two years with bars
   - Year selector chips work
   - Genre radar renders as spider chart
   - Donut chart shows completion ratio
   - Page count bars proportional
   - Author ranking ordered correctly
5. Tab switching is smooth, no loading flash on return
6. Colors match the rest of the app (no yellow, only beige/brown palette)
7. Empty state: with no data, tabs show gracefully (no crashes)

- [ ] **Step 4: Commit any fixes from manual testing**

```bash
git add -A
git commit -m "fix: adjustments from manual testing"
```

- [ ] **Step 5: Update Obsidian vault**

Create vault entries for:
- `Services/Implementations/AdvancedStatsService.md`
- `ViewModels/StatsTrendsViewModel.md`
- `ViewModels/StatsAnalysesViewModel.md`
- `Components/Shared/StatsTrends.md`
- `Components/Shared/StatsAnalyses.md`
- `Models/YearStats.md`
- `Models/AuthorStats.md`

Update `Index.md` and existing entries that now depend on new files.

- [ ] **Step 6: Update CHANGELOG.md**

Under `## [Unveröffentlicht]` → `### Hinzugefügt`:
```markdown
- Erweiterte Statistiken: 3-Tab-System (Übersicht | Trends | Analysen) auf der Statistik-Seite
  - Trends-Tab: Lese-Kalender (Heatmap), Wochentag-Verteilung, Tageszeit-Analyse, Session-Längen, Monatlicher Verlauf, Lesegeschwindigkeit, Lesedauer pro Buch
  - Analysen-Tab: Jahresvergleich, Genre-Radar, Abschlussquote, Buchlängen-Vorliebe, Meistgelesene Autoren
```

- [ ] **Step 7: Final commit**

```bash
git add CHANGELOG.md
cd C:\Users\Tristan\Documents\Obsidian\codebase-map && git add . && git commit -m "Update graph" && git push
```
