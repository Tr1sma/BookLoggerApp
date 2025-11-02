# Phase M4: UI Implementation

**Zeitrahmen:** 2 Wochen (80 Stunden)
**Status:** Planned
**Dependencies:** M3 (Services müssen implementiert sein)
**Parallel zu:** M5 (Plant UI kann parallel laufen)

---

## Überblick

In dieser Phase implementieren wir die komplette Benutzeroberfläche mit Blazor Components, ViewModels und Navigation. Der Fokus liegt auf der **2D Grid Bookshelf** als zentrale UI und allen Kern-Pages.

### Ziele

1. ✅ 8 ViewModels implementieren
2. ✅ 7 Blazor Pages erstellen
3. ✅ 2D Grid Bookshelf als Hauptansicht
4. ✅ Shared Components (BookCard, StatCard, etc.)
5. ✅ Navigation & Routing
6. ✅ CSS Styling (Modern, Responsive)
7. ✅ UI Tests (Optional: bUnit oder Manual)

### Deliverables

- 8 ViewModels (Dashboard, Bookshelf, BookDetail, Reading, Goals, Stats, Settings, BookEdit)
- 7 Blazor Pages (.razor files)
- 10+ Shared Components
- CSS Stylesheets (app.css, bookshelf.css, components.css)
- JavaScript Interop (für Animationen)
- Navigation Menu aktualisiert

---

## UI-Pages Übersicht

| Page | Route | ViewModel | Priorität | Aufwand |
|------|-------|-----------|-----------|---------|
| **Dashboard** | `/` | DashboardViewModel | P0 | 10h |
| **Bookshelf** | `/bookshelf` | BookshelfViewModel | P0 | 12h |
| **Book Detail** | `/books/{id}` | BookDetailViewModel | P0 | 10h |
| **Book Edit** | `/books/{id}/edit` | BookEditViewModel | P1 | 8h |
| **Reading View** | `/reading/{sessionId}` | ReadingViewModel | P1 | 10h |
| **Goals** | `/goals` | GoalsViewModel | P1 | 8h |
| **Stats** | `/stats` | StatsViewModel | P1 | 10h |
| **Settings** | `/settings` | SettingsViewModel | P1 | 8h |

**Gesamt:** ~80 Stunden

---

## Arbeitspaket 1: ViewModels (Basis-Infrastruktur)

**Aufwand:** 16 Stunden
**Priorität:** P0 (Blocker für UI)

### 1.1 Basis-ViewModel (abstrakt)

**Location:** `BookLoggerApp.Core/ViewModels/ViewModelBase.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    protected void ClearError()
    {
        ErrorMessage = null;
    }

    protected void SetError(string message)
    {
        ErrorMessage = message;
        IsBusy = false;
    }

    protected async Task ExecuteSafelyAsync(Func<Task> action, string? errorPrefix = null)
    {
        try
        {
            IsBusy = true;
            ClearError();
            await action();
        }
        catch (Exception ex)
        {
            var prefix = errorPrefix ?? "An error occurred";
            SetError($"{prefix}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### 1.2 DashboardViewModel

**Location:** `BookLoggerApp.Core/ViewModels/DashboardViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IGoalService _goalService;
    private readonly IPlantService _plantService;
    private readonly IStatsService _statsService;

    public DashboardViewModel(
        IBookService bookService,
        IProgressService progressService,
        IGoalService goalService,
        IPlantService plantService,
        IStatsService statsService)
    {
        _bookService = bookService;
        _progressService = progressService;
        _goalService = goalService;
        _plantService = plantService;
        _statsService = statsService;
    }

    [ObservableProperty]
    private Book? _currentlyReading;

    [ObservableProperty]
    private int _booksReadThisWeek;

    [ObservableProperty]
    private int _minutesReadThisWeek;

    [ObservableProperty]
    private int _pagesReadThisWeek;

    [ObservableProperty]
    private int _xpEarnedThisWeek;

    [ObservableProperty]
    private List<ReadingGoal> _activeGoals = new();

    [ObservableProperty]
    private UserPlant? _activePlant;

    [ObservableProperty]
    private List<ReadingSession> _recentActivity = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            // Currently Reading Book
            var readingBooks = await _bookService.GetByStatusAsync(ReadingStatus.Reading);
            CurrentlyReading = readingBooks.FirstOrDefault();

            // This Week Stats
            var weekStart = DateTime.UtcNow.Date.AddDays(-7);
            var weekEnd = DateTime.UtcNow.Date;

            BooksReadThisWeek = await _statsService.GetBooksReadInPeriodAsync(weekStart, weekEnd);
            MinutesReadThisWeek = await _statsService.GetMinutesReadInPeriodAsync(weekStart, weekEnd);

            var weekSessions = await _progressService.GetSessionsInRangeAsync(weekStart, weekEnd);
            PagesReadThisWeek = weekSessions.Sum(s => s.PagesRead ?? 0);
            XpEarnedThisWeek = weekSessions.Sum(s => s.XpEarned);

            // Active Goals
            ActiveGoals = (await _goalService.GetActiveGoalsAsync()).ToList();

            // Active Plant
            ActivePlant = await _plantService.GetActivePlantAsync();

            // Recent Activity
            RecentActivity = (await _progressService.GetRecentSessionsAsync(5)).ToList();
        }, "Failed to load dashboard");
    }

    [RelayCommand]
    public async Task WaterPlantAsync()
    {
        if (ActivePlant == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            ActivePlant = await _plantService.WaterPlantAsync(ActivePlant.Id);
        }, "Failed to water plant");
    }
}
```

### 1.3 BookshelfViewModel

**Location:** `BookLoggerApp.Core/ViewModels/BookshelfViewModel.cs`

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookshelfViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;

    public BookshelfViewModel(IBookService bookService, IGenreService genreService)
    {
        _bookService = bookService;
        _genreService = genreService;
    }

    [ObservableProperty]
    private ObservableCollection<Book> _books = new();

    [ObservableProperty]
    private List<Genre> _genres = new();

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private ReadingStatus? _filterStatus;

    [ObservableProperty]
    private Guid? _filterGenreId;

    [ObservableProperty]
    private string _sortBy = "Title"; // Title, Author, DateAdded, Status

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var allBooks = await _bookService.GetAllAsync();
            Books = new ObservableCollection<Book>(allBooks);

            Genres = (await _genreService.GetAllAsync()).ToList();
        }, "Failed to load books");
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            IEnumerable<Book> filtered;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = await _bookService.GetAllAsync();
            }
            else
            {
                filtered = await _bookService.SearchAsync(SearchQuery);
            }

            // Apply Filters
            if (FilterStatus.HasValue)
            {
                filtered = filtered.Where(b => b.Status == FilterStatus.Value);
            }

            if (FilterGenreId.HasValue)
            {
                var booksInGenre = await _bookService.GetByGenreAsync(FilterGenreId.Value);
                var genreBookIds = booksInGenre.Select(b => b.Id).ToHashSet();
                filtered = filtered.Where(b => genreBookIds.Contains(b.Id));
            }

            // Apply Sorting
            filtered = SortBy switch
            {
                "Author" => filtered.OrderBy(b => b.Author),
                "DateAdded" => filtered.OrderByDescending(b => b.DateAdded),
                "Status" => filtered.OrderBy(b => b.Status),
                _ => filtered.OrderBy(b => b.Title)
            };

            Books = new ObservableCollection<Book>(filtered);
        }, "Failed to search books");
    }

    [RelayCommand]
    public async Task DeleteBookAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.DeleteAsync(bookId);
            await LoadAsync();
        }, "Failed to delete book");
    }

    [RelayCommand]
    public void ClearFilters()
    {
        SearchQuery = "";
        FilterStatus = null;
        FilterGenreId = null;
    }
}
```

### 1.4 BookDetailViewModel (aktualisiert)

**Location:** `BookLoggerApp.Core/ViewModels/BookDetailViewModel.cs`

**ERWEITERN VON M1:**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookDetailViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IQuoteService _quoteService;
    private readonly IAnnotationService _annotationService;
    private readonly IGenreService _genreService;

    public BookDetailViewModel(
        IBookService bookService,
        IProgressService progressService,
        IQuoteService quoteService,
        IAnnotationService annotationService,
        IGenreService genreService)
    {
        _bookService = bookService;
        _progressService = progressService;
        _quoteService = quoteService;
        _annotationService = annotationService;
        _genreService = genreService;
    }

    [ObservableProperty]
    private Book? _book;

    [ObservableProperty]
    private int _totalMinutes;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private ObservableCollection<ReadingSession> _sessions = new();

    [ObservableProperty]
    private ObservableCollection<Quote> _quotes = new();

    [ObservableProperty]
    private ObservableCollection<Annotation> _annotations = new();

    [ObservableProperty]
    private List<Genre> _bookGenres = new();

    [RelayCommand]
    public async Task LoadAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Book = await _bookService.GetWithDetailsAsync(bookId);
            if (Book == null)
            {
                SetError("Book not found");
                return;
            }

            TotalMinutes = await _progressService.GetTotalMinutesAsync(bookId);
            TotalPages = await _progressService.GetTotalPagesAsync(bookId);

            var sessions = await _progressService.GetSessionsByBookAsync(bookId);
            Sessions = new ObservableCollection<ReadingSession>(sessions);

            var quotes = await _quoteService.GetQuotesByBookAsync(bookId);
            Quotes = new ObservableCollection<Quote>(quotes);

            var annotations = await _annotationService.GetAnnotationsByBookAsync(bookId);
            Annotations = new ObservableCollection<Annotation>(annotations);

            BookGenres = (await _genreService.GetGenresByBookAsync(bookId)).ToList();
        }, "Failed to load book details");
    }

    [RelayCommand]
    public async Task StartReadingAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.StartReadingAsync(Book.Id);
            await LoadAsync(Book.Id); // Reload
        }, "Failed to start reading");
    }

    [RelayCommand]
    public async Task CompleteBookAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.CompleteBookAsync(Book.Id);
            await LoadAsync(Book.Id); // Reload
        }, "Failed to complete book");
    }

    [RelayCommand]
    public async Task AddQuoteAsync(string text, int? pageNumber)
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            var quote = new Quote
            {
                BookId = Book.Id,
                Text = text,
                PageNumber = pageNumber
            };
            await _quoteService.AddQuoteAsync(quote);
            await LoadAsync(Book.Id); // Reload
        }, "Failed to add quote");
    }
}
```

**Weitere ViewModels:** BookEditViewModel, ReadingViewModel, GoalsViewModel, StatsViewModel, SettingsViewModel
(Analog zu den obigen, Details in separaten Dateien)

### Acceptance Criteria

- [ ] ViewModelBase mit IsBusy, ErrorMessage, ExecuteSafelyAsync
- [ ] 8 ViewModels implementiert
- [ ] Alle ViewModels nutzen Services aus M3
- [ ] Unit Tests für ViewModels (mit Mock Services)

---

## Arbeitspaket 2: Dashboard Page

**Aufwand:** 10 Stunden
**Priorität:** P0

### 2.1 Dashboard.razor

**Location:** `BookLoggerApp/Components/Pages/Dashboard.razor`

```razor
@page "/"
@using BookLoggerApp.Core.ViewModels
@inject DashboardViewModel ViewModel
@implements IDisposable

<PageTitle>Dashboard - BookLogger</PageTitle>

<div class="dashboard-container">
    @if (ViewModel.IsBusy)
    {
        <div class="loading-spinner">
            <p>Loading...</p>
        </div>
    }
    else if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
    {
        <div class="alert alert-danger">
            @ViewModel.ErrorMessage
        </div>
    }
    else
    {
        <!-- Currently Reading Section -->
        <section class="currently-reading">
            <h2>Currently Reading</h2>
            @if (ViewModel.CurrentlyReading != null)
            {
                <div class="book-card-large">
                    <img src="@GetCoverImageUrl(ViewModel.CurrentlyReading)" alt="Book Cover" class="book-cover" />
                    <div class="book-info">
                        <h3>@ViewModel.CurrentlyReading.Title</h3>
                        <p class="author">by @ViewModel.CurrentlyReading.Author</p>
                        <div class="progress-bar">
                            <div class="progress-fill" style="width: @ViewModel.CurrentlyReading.ProgressPercentage%"></div>
                        </div>
                        <p class="progress-text">Page @ViewModel.CurrentlyReading.CurrentPage / @ViewModel.CurrentlyReading.PageCount (@ViewModel.CurrentlyReading.ProgressPercentage%)</p>
                        <button class="btn btn-primary" @onclick="NavigateToContinueReading">Continue Reading →</button>
                    </div>
                </div>
            }
            else
            {
                <p>No book currently reading. <a href="/bookshelf">Browse your bookshelf</a></p>
            }
        </section>

        <!-- This Week Stats -->
        <section class="stats-grid">
            <StatCard Icon="📚" Title="Books Read" Value="@ViewModel.BooksReadThisWeek" Subtitle="this week" />
            <StatCard Icon="⏱️" Title="Time Read" Value="@FormatMinutes(ViewModel.MinutesReadThisWeek)" Subtitle="this week" />
            <StatCard Icon="📄" Title="Pages Read" Value="@ViewModel.PagesReadThisWeek" Subtitle="this week" />
            <StatCard Icon="⭐" Title="XP Earned" Value="@ViewModel.XpEarnedThisWeek" Subtitle="this week" />
        </section>

        <!-- Active Goals -->
        <section class="goals-section">
            <h2>Active Goals</h2>
            @if (ViewModel.ActiveGoals.Any())
            {
                <div class="goals-list">
                    @foreach (var goal in ViewModel.ActiveGoals)
                    {
                        <GoalCard Goal="@goal" />
                    }
                </div>
            }
            else
            {
                <p>No active goals. <a href="/goals">Create a goal</a></p>
            }
        </section>

        <!-- Plant Widget -->
        @if (ViewModel.ActivePlant != null)
        {
            <section class="plant-widget">
                <PlantWidget Plant="@ViewModel.ActivePlant" OnWater="HandleWaterPlant" />
            </section>
        }

        <!-- Recent Activity -->
        <section class="recent-activity">
            <h2>Recent Activity</h2>
            @if (ViewModel.RecentActivity.Any())
            {
                <ul class="activity-list">
                    @foreach (var session in ViewModel.RecentActivity)
                    {
                        <li>
                            <span class="activity-icon">📖</span>
                            <span class="activity-text">Read for @session.Minutes minutes</span>
                            <span class="activity-time">@FormatRelativeTime(session.StartedAt)</span>
                        </li>
                    }
                </ul>
            }
        </section>
    }
</div>

@code {
    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private string GetCoverImageUrl(Book book)
    {
        return string.IsNullOrEmpty(book.CoverImagePath)
            ? "/images/placeholder-cover.png"
            : book.CoverImagePath;
    }

    private string FormatMinutes(int minutes)
    {
        if (minutes < 60) return $"{minutes}m";
        var hours = minutes / 60;
        var remainingMinutes = minutes % 60;
        return $"{hours}h {remainingMinutes}m";
    }

    private string FormatRelativeTime(DateTime dateTime)
    {
        var diff = DateTime.UtcNow - dateTime;
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        return $"{(int)diff.TotalDays} days ago";
    }

    private void NavigateToContinueReading()
    {
        // TODO: Navigate to Reading View
    }

    private async Task HandleWaterPlant()
    {
        await ViewModel.WaterPlantCommand.ExecuteAsync(null);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

### 2.2 Shared Components

#### StatCard.razor

**Location:** `BookLoggerApp/Components/Shared/StatCard.razor`

```razor
<div class="stat-card">
    <div class="stat-icon">@Icon</div>
    <div class="stat-content">
        <h3 class="stat-value">@Value</h3>
        <p class="stat-title">@Title</p>
        @if (!string.IsNullOrEmpty(Subtitle))
        {
            <p class="stat-subtitle">@Subtitle</p>
        }
    </div>
</div>

@code {
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public object Value { get; set; } = 0;
    [Parameter] public string? Subtitle { get; set; }
}
```

#### GoalCard.razor

```razor
<div class="goal-card">
    <h4>@Goal.Title</h4>
    <div class="progress-bar">
        <div class="progress-fill" style="width: @Goal.ProgressPercentage%"></div>
    </div>
    <p>@Goal.Current / @Goal.Target @GetGoalTypeUnit(Goal.Type) (@Goal.ProgressPercentage%)</p>
</div>

@code {
    [Parameter] public ReadingGoal Goal { get; set; } = null!;

    private string GetGoalTypeUnit(GoalType type)
    {
        return type switch
        {
            GoalType.Books => "books",
            GoalType.Pages => "pages",
            GoalType.Minutes => "minutes",
            _ => ""
        };
    }
}
```

### Acceptance Criteria

- [ ] Dashboard.razor erstellt mit allen Sections
- [ ] StatCard, GoalCard Shared Components
- [ ] Dashboard lädt Daten beim Start
- [ ] Responsive Design (Mobile + Desktop)

---

## Arbeitspaket 3: Bookshelf Page (2D Grid)

**Aufwand:** 12 Stunden
**Priorität:** P0 (Zentrale UI)

### 3.1 Bookshelf.razor

**Location:** `BookLoggerApp/Components/Pages/Bookshelf.razor`

```razor
@page "/bookshelf"
@using BookLoggerApp.Core.ViewModels
@inject BookshelfViewModel ViewModel
@inject NavigationManager Navigation

<PageTitle>Bookshelf - BookLogger</PageTitle>

<div class="bookshelf-container">
    <!-- Header & Controls -->
    <div class="bookshelf-header">
        <h1>📚 My Bookshelf</h1>
        <div class="bookshelf-controls">
            <input type="text" class="search-input" placeholder="🔍 Search books..."
                   @bind="ViewModel.SearchQuery" @bind:event="oninput" @onchange="HandleSearch" />
            <button class="btn btn-primary" @onclick="NavigateToAddBook">+ Add Book</button>
        </div>
    </div>

    <!-- Filters & Sort -->
    <div class="bookshelf-filters">
        <div class="filter-group">
            <label>Sort by:</label>
            <select @bind="ViewModel.SortBy" @onchange="HandleSearch">
                <option value="Title">Title</option>
                <option value="Author">Author</option>
                <option value="DateAdded">Date Added</option>
                <option value="Status">Status</option>
            </select>
        </div>

        <div class="filter-group">
            <label>Status:</label>
            <select @bind="ViewModel.FilterStatus" @onchange="HandleSearch">
                <option value="">All</option>
                <option value="@ReadingStatus.Planned">Planned</option>
                <option value="@ReadingStatus.Reading">Reading</option>
                <option value="@ReadingStatus.Completed">Completed</option>
                <option value="@ReadingStatus.Abandoned">Abandoned</option>
            </select>
        </div>

        <div class="filter-group">
            <label>Genre:</label>
            <select @bind="ViewModel.FilterGenreId" @onchange="HandleSearch">
                <option value="">All Genres</option>
                @foreach (var genre in ViewModel.Genres)
                {
                    <option value="@genre.Id">@genre.Icon @genre.Name</option>
                }
            </select>
        </div>

        <button class="btn btn-secondary" @onclick="HandleClearFilters">Clear Filters</button>
    </div>

    <!-- Book Grid -->
    @if (ViewModel.IsBusy)
    {
        <div class="loading">Loading your books...</div>
    }
    else if (!ViewModel.Books.Any())
    {
        <div class="empty-state">
            <h2>No books found</h2>
            <p>Start building your library by adding your first book!</p>
            <button class="btn btn-primary" @onclick="NavigateToAddBook">+ Add Your First Book</button>
        </div>
    }
    else
    {
        <div class="book-grid">
            @foreach (var book in ViewModel.Books)
            {
                <BookCard Book="@book"
                          OnClick="() => NavigateToBookDetail(book.Id)"
                          OnDelete="() => HandleDeleteBook(book.Id)" />
            }
        </div>

        <div class="bookshelf-footer">
            <p>Showing @ViewModel.Books.Count book(s)</p>
        </div>
    }
</div>

@code {
    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async Task HandleSearch()
    {
        await ViewModel.SearchCommand.ExecuteAsync(null);
    }

    private async Task HandleClearFilters()
    {
        ViewModel.ClearFiltersCommand.Execute(null);
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async Task HandleDeleteBook(Guid bookId)
    {
        // TODO: Add confirmation dialog
        await ViewModel.DeleteBookCommand.ExecuteAsync(bookId);
    }

    private void NavigateToBookDetail(Guid bookId)
    {
        Navigation.NavigateTo($"/books/{bookId}");
    }

    private void NavigateToAddBook()
    {
        Navigation.NavigateTo("/books/new");
    }
}
```

### 3.2 BookCard Component

**Location:** `BookLoggerApp/Components/Shared/BookCard.razor`

```razor
<div class="book-card" @onclick="HandleClick">
    <div class="book-cover-wrapper">
        <img src="@GetCoverUrl()" alt="@Book.Title" class="book-cover" />
        <div class="book-status-badge status-@Book.Status.ToString().ToLower()">
            @GetStatusIcon()
        </div>
    </div>
    <div class="book-card-body">
        <h3 class="book-title">@Book.Title</h3>
        <p class="book-author">@Book.Author</p>
        @if (Book.Rating.HasValue)
        {
            <div class="book-rating">
                @for (int i = 1; i <= 5; i++)
                {
                    <span class="star @(i <= Book.Rating.Value ? "filled" : "empty")">★</span>
                }
            </div>
        }
        @if (Book.Status == ReadingStatus.Reading || Book.Status == ReadingStatus.Completed)
        {
            <div class="book-progress">
                <div class="progress-bar">
                    <div class="progress-fill" style="width: @Book.ProgressPercentage%"></div>
                </div>
                <p class="progress-text">@Book.ProgressPercentage%</p>
            </div>
        }
    </div>
    <div class="book-card-actions">
        <button class="btn-icon" @onclick:stopPropagation="true" @onclick="HandleDelete" title="Delete">
            🗑️
        </button>
    </div>
</div>

@code {
    [Parameter] public Book Book { get; set; } = null!;
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public EventCallback OnDelete { get; set; }

    private string GetCoverUrl()
    {
        return string.IsNullOrEmpty(Book.CoverImagePath)
            ? "/images/placeholder-cover.png"
            : Book.CoverImagePath;
    }

    private string GetStatusIcon()
    {
        return Book.Status switch
        {
            ReadingStatus.Planned => "📋",
            ReadingStatus.Reading => "📖",
            ReadingStatus.Completed => "✅",
            ReadingStatus.Abandoned => "❌",
            _ => ""
        };
    }

    private async Task HandleClick()
    {
        await OnClick.InvokeAsync();
    }

    private async Task HandleDelete()
    {
        await OnDelete.InvokeAsync();
    }
}
```

### 3.3 bookshelf.css

**Location:** `BookLoggerApp/wwwroot/css/bookshelf.css`

```css
.bookshelf-container {
    padding: 2rem;
    max-width: 1400px;
    margin: 0 auto;
}

.bookshelf-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 2rem;
}

.bookshelf-controls {
    display: flex;
    gap: 1rem;
}

.search-input {
    padding: 0.5rem 1rem;
    border: 1px solid #ddd;
    border-radius: 8px;
    font-size: 1rem;
    min-width: 300px;
}

.bookshelf-filters {
    display: flex;
    gap: 1rem;
    margin-bottom: 2rem;
    flex-wrap: wrap;
}

.filter-group {
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

.filter-group select {
    padding: 0.5rem;
    border: 1px solid #ddd;
    border-radius: 4px;
}

/* Book Grid (2D Grid Layout) */
.book-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
    gap: 2rem;
    margin-bottom: 2rem;
}

@media (max-width: 768px) {
    .book-grid {
        grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
        gap: 1rem;
    }
}

/* Book Card */
.book-card {
    background: white;
    border-radius: 12px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    overflow: hidden;
    cursor: pointer;
    transition: transform 0.2s, box-shadow 0.2s;
    position: relative;
}

.book-card:hover {
    transform: translateY(-4px);
    box-shadow: 0 4px 16px rgba(0,0,0,0.15);
}

.book-cover-wrapper {
    position: relative;
    aspect-ratio: 2/3;
    overflow: hidden;
}

.book-cover {
    width: 100%;
    height: 100%;
    object-fit: cover;
}

.book-status-badge {
    position: absolute;
    top: 8px;
    right: 8px;
    background: rgba(255,255,255,0.9);
    padding: 4px 8px;
    border-radius: 12px;
    font-size: 1.2rem;
}

.book-card-body {
    padding: 1rem;
}

.book-title {
    font-size: 1rem;
    font-weight: 600;
    margin: 0 0 0.5rem 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.book-author {
    font-size: 0.875rem;
    color: #666;
    margin: 0 0 0.5rem 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.book-rating {
    margin-bottom: 0.5rem;
}

.star {
    font-size: 1rem;
}

.star.filled {
    color: #ffc107;
}

.star.empty {
    color: #ddd;
}

.book-progress {
    margin-top: 0.5rem;
}

.progress-bar {
    height: 6px;
    background: #eee;
    border-radius: 3px;
    overflow: hidden;
    margin-bottom: 0.25rem;
}

.progress-fill {
    height: 100%;
    background: linear-gradient(90deg, #4CAF50, #8BC34A);
    transition: width 0.3s;
}

.progress-text {
    font-size: 0.75rem;
    color: #666;
    text-align: right;
}

.book-card-actions {
    padding: 0.5rem 1rem;
    border-top: 1px solid #eee;
    display: flex;
    justify-content: flex-end;
}

.btn-icon {
    background: none;
    border: none;
    font-size: 1.2rem;
    cursor: pointer;
    padding: 0.25rem;
    opacity: 0.6;
    transition: opacity 0.2s;
}

.btn-icon:hover {
    opacity: 1;
}

.empty-state {
    text-align: center;
    padding: 4rem 2rem;
    color: #666;
}

.bookshelf-footer {
    text-align: center;
    padding: 1rem;
    color: #666;
}
```

### Acceptance Criteria

- [ ] Bookshelf.razor mit 2D Grid Layout
- [ ] BookCard Component mit Cover, Title, Author, Rating, Progress
- [ ] Search, Filter & Sort funktionieren
- [ ] Responsive Design (2-6 Spalten je nach Bildschirmgröße)
- [ ] Loading & Empty States
- [ ] Navigation zu Book Detail

---

## Arbeitspaket 4-8: Weitere Pages (Kompakt)

**Aufgrund der Länge kompakt beschrieben. Analog zu Dashboard/Bookshelf.**

### 4. Book Detail Page (Erweitert)

**Aufwand:** 10 Stunden

**Features:**
- Cover Image (groß)
- Title, Author, ISBN, Genre-Tags
- Rating (Edit inline)
- Progress Bar
- "Start Reading", "Complete" Buttons
- Tabs: Sessions, Quotes, Annotations
- Add Quote/Annotation Forms

**Wireframe siehe Plan.md (Hauptdokument)**

### 5. Reading View Page

**Aufwand:** 10 Stunden

**Features:**
- Live-Timer (Start/Stop/Pause)
- Page Input (Current Page)
- XP Display (Live-Update)
- "End Session" Button
- Fullscreen-Option (fokussiertes Lesen)

**Route:** `/reading/{sessionId}`

### 6. Goals Page

**Aufwand:** 8 Stunden

**Features:**
- List Active Goals (Cards mit Progress Bar)
- Create Goal Form (Type, Target, Date Range)
- Completed Goals (History)
- Delete/Edit Goals

**Route:** `/goals`

### 7. Stats Page

**Aufwand:** 10 Stunden

**Features:**
- Overview Stats (Total Books, Minutes, Pages, Streak)
- Charts (Minutes per Day, Books per Month, Genre Breakdown)
- Filter by Date Range
- Export Stats (CSV)

**Technology:** Chart.js via JS Interop (oder Blazor Chart Library)

**Route:** `/stats`

### 8. Settings Page

**Aufwand:** 8 Stunden

**Features:**
- Theme Selection (Light/Dark/Auto)
- Language Selection (en/de/etc.)
- Notification Settings (Toggle, Reminder Time)
- Backup & Export (Buttons)
- About & Version Info
- Delete All Data (Confirmation)

**Route:** `/settings`

---

## Arbeitspaket 9: Navigation & Routing

**Aufwand:** 4 Stunden

### 9.1 NavMenu.razor aktualisieren

**Location:** `BookLoggerApp/Components/Layout/NavMenu.razor`

```razor
<nav class="navbar">
    <ul class="nav-menu">
        <li class="nav-item">
            <NavLink class="nav-link" href="/" Match="NavLinkMatch.All">
                <span class="nav-icon">🏠</span>
                <span class="nav-text">Dashboard</span>
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link" href="/bookshelf">
                <span class="nav-icon">📚</span>
                <span class="nav-text">Bookshelf</span>
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link" href="/reading">
                <span class="nav-icon">📖</span>
                <span class="nav-text">Reading</span>
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link" href="/goals">
                <span class="nav-icon">🎯</span>
                <span class="nav-text">Goals</span>
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link" href="/shop">
                <span class="nav-icon">🌱</span>
                <span class="nav-text">Shop</span>
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link" href="/stats">
                <span class="nav-icon">📊</span>
                <span class="nav-text">Stats</span>
            </NavLink>
        </li>
        <li class="nav-item">
            <NavLink class="nav-link" href="/settings">
                <span class="nav-icon">⚙️</span>
                <span class="nav-text">Settings</span>
            </NavLink>
        </li>
    </ul>
</nav>
```

### 9.2 Routes.razor aktualisieren

**Location:** `BookLoggerApp/Components/Routes.razor`

```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <div class="not-found">
                <h1>404 - Page Not Found</h1>
                <p>The page you're looking for doesn't exist.</p>
                <a href="/">Go to Dashboard</a>
            </div>
        </LayoutView>
    </NotFound>
</Router>
```

### Acceptance Criteria

- [ ] NavMenu mit allen Pages aktualisiert
- [ ] NavLink Active States funktionieren
- [ ] 404 Page vorhanden
- [ ] Mobile Navigation (Hamburger Menu, optional)

---

## Arbeitspaket 10: CSS Styling

**Aufwand:** 8 Stunden

### 10.1 app.css (Global Styles)

**Location:** `BookLoggerApp/wwwroot/css/app.css`

**Themes:**
- CSS Variables für Farben
- Light/Dark Theme Support
- Responsive Breakpoints
- Typography
- Buttons, Forms, Cards

**Beispiel:**
```css
:root {
    --primary-color: #512BD4;
    --secondary-color: #4CAF50;
    --text-color: #333;
    --bg-color: #f5f5f5;
    --card-bg: white;
    --border-radius: 12px;
}

[data-theme="dark"] {
    --text-color: #f5f5f5;
    --bg-color: #1a1a1a;
    --card-bg: #2a2a2a;
}

body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    color: var(--text-color);
    background: var(--bg-color);
}

.btn {
    padding: 0.75rem 1.5rem;
    border: none;
    border-radius: var(--border-radius);
    font-size: 1rem;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-primary {
    background: var(--primary-color);
    color: white;
}

.btn-primary:hover {
    background: darken(var(--primary-color), 10%);
}
```

### Acceptance Criteria

- [ ] app.css mit global Styles
- [ ] bookshelf.css, components.css
- [ ] Light/Dark Theme Support
- [ ] Responsive Design (Mobile, Tablet, Desktop)
- [ ] Accessibility (Kontraste AA-konform)

---

## Definition of Done (M4)

- [x] 8 ViewModels implementiert
- [x] 7 Blazor Pages erstellt
- [x] 2D Grid Bookshelf als zentrale UI
- [x] 10+ Shared Components (BookCard, StatCard, GoalCard, PlantWidget, etc.)
- [x] Navigation & Routing funktioniert
- [x] CSS Styling (Modern, Responsive, Accessible)
- [x] UI Tests (Manual Testing Checklist)
- [x] Performance: Bookshelf Render <500ms für 20 Bücher
- [x] Mobile-responsive (320px - 1920px Breakpoints)
- [x] No Console Errors
- [x] CI-Pipeline grün

---

**Ende Phase M4 Plan**
