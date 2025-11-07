# Book Rating System - Implementation Plan

## Übersicht

Dieses Dokument beschreibt die Implementierung eines erweiterten Multi-Kategorie-Bewertungssystems für die BookLoggerApp. Benutzer sollen Bücher in verschiedenen Kategorien bewerten können, wobei jede Kategorie ihr eigenes Emoji und eine 1-5 Sterne-Bewertung hat.

## Bewertungskategorien

| Kategorie | Emoji | Beschreibung |
|-----------|-------|--------------|
| Characters | 👥 | Qualität und Entwicklung der Charaktere |
| Plot | 📖 | Handlung und Storyline |
| Writing Style | ✍️ | Schreibstil des Autors |
| Spice Level | 🌶️ | Romantik/Spice-Level (für Romance-Bücher) |
| Pacing | ⚡ | Tempo der Geschichte |
| World Building | 🌍 | Weltaufbau (besonders für Fantasy/Sci-Fi) |
| Overall | ⭐ | Gesamtbewertung |

## Phase 1: Datenmodell-Änderungen

### 1.1 Book Model erweitern

**Datei:** `BookLoggerApp.Core/Models/Book.cs`

**Änderungen:**
```csharp
// Alte Property beibehalten für Kompatibilität
[Obsolete("Use OverallRating instead")]
public int? Rating { get; set; }

// Neue Rating-Properties
public int? CharactersRating { get; set; } // 1-5, nullable
public int? PlotRating { get; set; }        // 1-5, nullable
public int? WritingStyleRating { get; set; } // 1-5, nullable
public int? SpiceLevelRating { get; set; }   // 1-5, nullable
public int? PacingRating { get; set; }       // 1-5, nullable
public int? WorldBuildingRating { get; set; } // 1-5, nullable
public int? OverallRating { get; set; }      // 1-5, nullable

// Computed Property: Durchschnitt aller gesetzten Ratings
public double? AverageRating
{
    get
    {
        var ratings = new List<int?>
        {
            CharactersRating,
            PlotRating,
            WritingStyleRating,
            SpiceLevelRating,
            PacingRating,
            WorldBuildingRating
        };

        var validRatings = ratings.Where(r => r.HasValue).Select(r => r.Value).ToList();

        if (!validRatings.Any())
            return OverallRating;

        return validRatings.Average();
    }
}
```

### 1.2 Datenbank-Migration

**Entity Framework Migration erstellen:**
```bash
dotnet ef migrations add AddMultiCategoryRatings --project BookLoggerApp.Core
```

**Migration-Logik:**
- Neue Spalten hinzufügen (alle nullable)
- Bestehende `Rating`-Werte in `OverallRating` migrieren
- `Rating` Spalte als obsolet markieren (aber nicht löschen für Backwards-Kompatibilität)

### 1.3 RatingCategory Enum (Optional)

**Datei:** `BookLoggerApp.Core/Models/RatingCategory.cs`

```csharp
public enum RatingCategory
{
    Characters = 0,
    Plot = 1,
    WritingStyle = 2,
    SpiceLevel = 3,
    Pacing = 4,
    WorldBuilding = 5,
    Overall = 6
}
```

### 1.4 RatingCategoryInfo Helper-Klasse

**Datei:** `BookLoggerApp.Core/Models/RatingCategoryInfo.cs`

```csharp
public class RatingCategoryInfo
{
    public RatingCategory Category { get; set; }
    public string Emoji { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }

    public static List<RatingCategoryInfo> GetAllCategories()
    {
        return new List<RatingCategoryInfo>
        {
            new() { Category = RatingCategory.Characters, Emoji = "👥", DisplayName = "Characters", Description = "Character quality and development" },
            new() { Category = RatingCategory.Plot, Emoji = "📖", DisplayName = "Plot", Description = "Story and storyline" },
            new() { Category = RatingCategory.WritingStyle, Emoji = "✍️", DisplayName = "Writing Style", Description = "Author's writing style" },
            new() { Category = RatingCategory.SpiceLevel, Emoji = "🌶️", DisplayName = "Spice Level", Description = "Romance/Spice level" },
            new() { Category = RatingCategory.Pacing, Emoji = "⚡", DisplayName = "Pacing", Description = "Story tempo" },
            new() { Category = RatingCategory.WorldBuilding, Emoji = "🌍", DisplayName = "World Building", Description = "World building quality" },
            new() { Category = RatingCategory.Overall, Emoji = "⭐", DisplayName = "Overall", Description = "Overall rating" }
        };
    }
}
```

## Phase 2: Service-Layer Änderungen

### 2.1 IStatsService erweitern

**Datei:** `BookLoggerApp.Core/Services/Abstractions/IStatsService.cs`

**Neue Methoden:**
```csharp
// Durchschnittsbewertungen pro Kategorie
Task<double> GetAverageRatingAsync(RatingCategory category, DateTime? startDate = null, DateTime? endDate = null);
Task<Dictionary<RatingCategory, double>> GetAllAverageRatingsAsync(DateTime? startDate = null, DateTime? endDate = null);

// Top bewertete Bücher
Task<List<Book>> GetTopRatedBooksAsync(int count = 10, RatingCategory? category = null);
Task<List<BookRatingSummary>> GetBooksWithRatingsAsync();
```

**Neue DTOs:**
```csharp
public class BookRatingSummary
{
    public Book Book { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<RatingCategory, int?> Ratings { get; set; }
}
```

### 2.2 StatsService implementieren

**Datei:** `BookLoggerApp.Infrastructure/Services/StatsService.cs`

**Implementierung:**
- `GetAverageRatingAsync`: Berechnet Durchschnitt für eine spezifische Kategorie
- `GetAllAverageRatingsAsync`: Berechnet Durchschnitte für alle Kategorien
- `GetTopRatedBooksAsync`: Gibt die bestbewerteten Bücher zurück (sortiert nach AverageRating oder spezifischer Kategorie)
- `GetBooksWithRatingsAsync`: Gibt alle Bücher mit ihren Bewertungen zurück

## Phase 3: ViewModel-Änderungen

### 3.1 StatsViewModel erweitern

**Datei:** `BookLoggerApp.Core/ViewModels/StatsViewModel.cs`

**Neue Properties:**
```csharp
// Durchschnittsbewertungen pro Kategorie
public double AverageCharactersRating { get; set; }
public double AveragePlotRating { get; set; }
public double AverageWritingStyleRating { get; set; }
public double AverageSpiceLevelRating { get; set; }
public double AveragePacingRating { get; set; }
public double AverageWorldBuildingRating { get; set; }
public double AverageOverallRating { get; set; }

// Top bewertete Bücher
public ObservableCollection<BookRatingSummary> TopRatedBooks { get; set; }

// Dictionary für alle Kategorien
public Dictionary<RatingCategory, double> CategoryAverages { get; set; }
```

**Neue Methods in LoadAsync:**
```csharp
private async Task LoadRatingStatisticsAsync()
{
    CategoryAverages = await _statsService.GetAllAverageRatingsAsync(DateRangeStart, DateRangeEnd);

    AverageCharactersRating = CategoryAverages.GetValueOrDefault(RatingCategory.Characters, 0);
    AveragePlotRating = CategoryAverages.GetValueOrDefault(RatingCategory.Plot, 0);
    // ... etc für alle Kategorien

    var topBooks = await _statsService.GetTopRatedBooksAsync(10);
    TopRatedBooks = new ObservableCollection<BookRatingSummary>(topBooks);
}
```

### 3.2 BookDetailViewModel erweitern

**Datei:** `BookLoggerApp.Core/ViewModels/BookDetailViewModel.cs`

**Neue Methods:**
```csharp
public async Task UpdateRatingAsync(RatingCategory category, int rating)
{
    if (Book == null) return;

    switch (category)
    {
        case RatingCategory.Characters:
            Book.CharactersRating = rating;
            break;
        case RatingCategory.Plot:
            Book.PlotRating = rating;
            break;
        // ... etc für alle Kategorien
    }

    await _bookService.UpdateAsync(Book);
    await LoadAsync(Book.Id); // Refresh
}

public int? GetRating(RatingCategory category)
{
    if (Book == null) return null;

    return category switch
    {
        RatingCategory.Characters => Book.CharactersRating,
        RatingCategory.Plot => Book.PlotRating,
        // ... etc
    };
}
```

## Phase 4: UI-Komponenten

### 4.1 Neue Komponente: RatingInput.razor

**Datei:** `BookLoggerApp/Components/Shared/RatingInput.razor`

**Zweck:** Wiederverwendbare Komponente für die Eingabe einer einzelnen Bewertung

**Properties:**
```csharp
[Parameter] public string Emoji { get; set; }
[Parameter] public string Label { get; set; }
[Parameter] public int? Rating { get; set; }
[Parameter] public EventCallback<int?> RatingChanged { get; set; }
[Parameter] public bool ReadOnly { get; set; } = false;
```

**UI:**
```razor
<div class="rating-input">
    <div class="rating-header">
        <span class="rating-emoji">@Emoji</span>
        <span class="rating-label">@Label</span>
    </div>
    <div class="rating-stars">
        @for (int i = 1; i <= 5; i++)
        {
            int starValue = i;
            <span class="star @GetStarClass(starValue)"
                  @onclick="() => OnStarClick(starValue)"
                  style="@(ReadOnly ? "cursor: default;" : "cursor: pointer;")">
                ★
            </span>
        }
        @if (Rating.HasValue && !ReadOnly)
        {
            <button class="clear-rating" @onclick="ClearRating">✕</button>
        }
    </div>
</div>
```

### 4.2 Neue Komponente: BookRatingCard.razor

**Datei:** `BookLoggerApp/Components/Shared/BookRatingCard.razor`

**Zweck:** Zeigt alle Bewertungen für ein Buch in einer Karte an (read-only für Listen)

**Properties:**
```csharp
[Parameter] public Book Book { get; set; }
[Parameter] public bool ShowAverage { get; set; } = true;
```

### 4.3 BookDetail.razor erweitern

**Datei:** `BookLoggerApp/Components/Pages/BookDetail.razor`

**Neue Section nach Line 141 (nach der Description):**
```razor
<!-- Book Ratings Section -->
@if (ViewModel.Book.Status == ReadingStatus.Completed)
{
    <div class="book-ratings-section">
        <h3>📊 Book Ratings</h3>

        <div class="ratings-grid">
            <RatingInput Emoji="👥"
                        Label="Characters"
                        Rating="@ViewModel.Book.CharactersRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.Characters, r)" />

            <RatingInput Emoji="📖"
                        Label="Plot"
                        Rating="@ViewModel.Book.PlotRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.Plot, r)" />

            <RatingInput Emoji="✍️"
                        Label="Writing Style"
                        Rating="@ViewModel.Book.WritingStyleRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.WritingStyle, r)" />

            <RatingInput Emoji="🌶️"
                        Label="Spice Level"
                        Rating="@ViewModel.Book.SpiceLevelRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.SpiceLevel, r)" />

            <RatingInput Emoji="⚡"
                        Label="Pacing"
                        Rating="@ViewModel.Book.PacingRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.Pacing, r)" />

            <RatingInput Emoji="🌍"
                        Label="World Building"
                        Rating="@ViewModel.Book.WorldBuildingRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.WorldBuilding, r)" />

            <RatingInput Emoji="⭐"
                        Label="Overall"
                        Rating="@ViewModel.Book.OverallRating"
                        RatingChanged="async (r) => await UpdateRating(RatingCategory.Overall, r)" />
        </div>

        @if (ViewModel.Book.AverageRating.HasValue)
        {
            <div class="average-rating-display">
                <span class="average-label">Average Rating:</span>
                <span class="average-value">@ViewModel.Book.AverageRating.Value.ToString("F1") / 5.0</span>
            </div>
        }
    </div>
}
```

**Neue Method in @code:**
```csharp
private async Task UpdateRating(RatingCategory category, int? rating)
{
    if (ViewModel.Book == null) return;

    await ViewModel.UpdateRatingAsync(category, rating ?? 0);
    await InvokeAsync(StateHasChanged);
}
```

### 4.4 BookEdit.razor erweitern

**Datei:** `BookLoggerApp/Components/Pages/BookEdit.razor`

**Rating-Section hinzufügen** (ähnlich wie BookDetail, aber als Teil des Edit-Formulars)

### 4.5 Stats.razor erweitern

**Datei:** `BookLoggerApp/Components/Pages/Stats.razor`

**Nach Line 30 (Overview Stats Section):**
```razor
<!-- Rating Category Averages -->
<section class="rating-averages-section">
    <h2>📊 Average Ratings by Category</h2>
    <div class="rating-averages-grid">
        @foreach (var category in RatingCategoryInfo.GetAllCategories())
        {
            var average = ViewModel.CategoryAverages.GetValueOrDefault(category.Category, 0);
            <div class="rating-average-card">
                <div class="rating-category-header">
                    <span class="rating-emoji">@category.Emoji</span>
                    <span class="rating-name">@category.DisplayName</span>
                </div>
                <div class="rating-value">@average.ToString("F1")</div>
                <div class="rating-stars-display">
                    @for (int i = 1; i <= 5; i++)
                    {
                        <span class="star @(i <= Math.Round(average) ? "filled" : "empty")">★</span>
                    }
                </div>
            </div>
        }
    </div>
</section>

<!-- Top Rated Books -->
<section class="top-rated-books-section">
    <h2>🏆 Top Rated Books</h2>

    @if (ViewModel.TopRatedBooks?.Any() == true)
    {
        <div class="top-books-list">
            @foreach (var bookRating in ViewModel.TopRatedBooks.Take(10))
            {
                <div class="top-book-item" @key="bookRating.Book.Id">
                    <div class="book-rank">@(ViewModel.TopRatedBooks.ToList().IndexOf(bookRating) + 1)</div>
                    <div class="book-info">
                        <div class="book-title">@bookRating.Book.Title</div>
                        <div class="book-author">by @bookRating.Book.Author</div>
                    </div>
                    <div class="book-rating-summary">
                        <div class="average-rating">@bookRating.AverageRating.ToString("F1") ⭐</div>
                        <div class="rating-breakdown">
                            @foreach (var rating in bookRating.Ratings.Where(r => r.Value.HasValue))
                            {
                                var categoryInfo = RatingCategoryInfo.GetAllCategories()
                                    .FirstOrDefault(c => c.Category == rating.Key);
                                <span class="mini-rating">@categoryInfo?.Emoji @rating.Value</span>
                            }
                        </div>
                    </div>
                </div>
            }
        </div>
    }
    else
    {
        <p class="empty-message">No rated books yet. Complete books and add ratings to see them here!</p>
    }
</section>

<!-- Filter by Category -->
<section class="filter-by-category-section">
    <h2>🔍 Filter Top Books by Category</h2>
    <div class="category-filter-buttons">
        <button class="btn category-filter-btn @(selectedCategory == null ? "active" : "")"
                @onclick="() => FilterByCategory(null)">
            All
        </button>
        @foreach (var category in RatingCategoryInfo.GetAllCategories())
        {
            <button class="btn category-filter-btn @(selectedCategory == category.Category ? "active" : "")"
                    @onclick="() => FilterByCategory(category.Category)">
                @category.Emoji @category.DisplayName
            </button>
        }
    </div>
</section>
```

**@code Block erweitern:**
```csharp
private RatingCategory? selectedCategory = null;

private async Task FilterByCategory(RatingCategory? category)
{
    selectedCategory = category;
    var topBooks = await ViewModel.StatsService.GetTopRatedBooksAsync(10, category);
    ViewModel.TopRatedBooks = new ObservableCollection<BookRatingSummary>(topBooks);
    StateHasChanged();
}
```

## Phase 5: CSS/Styling

### 5.1 Neue Styles für Rating-Komponenten

**Datei:** `BookLoggerApp/wwwroot/css/app.css` (oder separate CSS-Datei)

```css
/* Rating Input Component */
.rating-input {
    padding: 1rem;
    border: 1px solid #ddd;
    border-radius: 8px;
    background: #fff;
}

.rating-header {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
}

.rating-emoji {
    font-size: 1.5rem;
}

.rating-label {
    font-weight: 600;
    color: #333;
}

.rating-stars {
    display: flex;
    gap: 0.25rem;
    align-items: center;
}

.rating-stars .star {
    font-size: 1.5rem;
    color: #ddd;
    transition: color 0.2s;
}

.rating-stars .star.filled {
    color: #ffc107;
}

.rating-stars .star.hover {
    color: #ffeb3b;
}

.clear-rating {
    margin-left: 0.5rem;
    padding: 0.25rem 0.5rem;
    background: #f44336;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.8rem;
}

/* Ratings Grid on Book Detail */
.book-ratings-section {
    margin: 2rem 0;
    padding: 1.5rem;
    background: #f9f9f9;
    border-radius: 8px;
}

.ratings-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 1rem;
    margin-bottom: 1rem;
}

.average-rating-display {
    padding: 1rem;
    background: #fff;
    border-radius: 8px;
    text-align: center;
    font-size: 1.2rem;
}

/* Rating Averages on Stats Page */
.rating-averages-section {
    margin: 2rem 0;
}

.rating-averages-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 1rem;
}

.rating-average-card {
    padding: 1.5rem;
    background: #fff;
    border: 1px solid #ddd;
    border-radius: 8px;
    text-align: center;
}

.rating-category-header {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
}

.rating-value {
    font-size: 2rem;
    font-weight: bold;
    color: #333;
    margin: 0.5rem 0;
}

.rating-stars-display {
    display: flex;
    justify-content: center;
    gap: 0.25rem;
}

/* Top Rated Books */
.top-rated-books-section {
    margin: 2rem 0;
}

.top-books-list {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}

.top-book-item {
    display: flex;
    align-items: center;
    padding: 1rem;
    background: #fff;
    border: 1px solid #ddd;
    border-radius: 8px;
    gap: 1rem;
}

.book-rank {
    font-size: 2rem;
    font-weight: bold;
    color: #ffc107;
    min-width: 60px;
    text-align: center;
}

.book-info {
    flex: 1;
}

.book-title {
    font-size: 1.1rem;
    font-weight: 600;
    color: #333;
}

.book-author {
    color: #666;
    font-size: 0.9rem;
}

.book-rating-summary {
    text-align: right;
}

.average-rating {
    font-size: 1.5rem;
    font-weight: bold;
    color: #333;
}

.rating-breakdown {
    display: flex;
    gap: 0.5rem;
    flex-wrap: wrap;
    justify-content: flex-end;
    margin-top: 0.5rem;
}

.mini-rating {
    font-size: 0.8rem;
    padding: 0.25rem 0.5rem;
    background: #f0f0f0;
    border-radius: 4px;
}

/* Category Filter Buttons */
.category-filter-buttons {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-top: 1rem;
}

.category-filter-btn {
    padding: 0.5rem 1rem;
    border: 1px solid #ddd;
    background: #fff;
    border-radius: 4px;
    cursor: pointer;
    transition: all 0.2s;
}

.category-filter-btn.active {
    background: #007bff;
    color: white;
    border-color: #007bff;
}

.category-filter-btn:hover {
    background: #f0f0f0;
}

.category-filter-btn.active:hover {
    background: #0056b3;
}
```

## Phase 6: Tests

### 6.1 Unit Tests für Book Model

**Datei:** `BookLoggerApp.Tests/Models/BookTests.cs`

```csharp
[Fact]
public void AverageRating_CalculatesCorrectly()
{
    var book = new Book
    {
        CharactersRating = 5,
        PlotRating = 4,
        WritingStyleRating = 5
    };

    Assert.Equal(4.67, book.AverageRating.Value, 2);
}

[Fact]
public void AverageRating_ReturnsOverallWhenNoCategoryRatings()
{
    var book = new Book
    {
        OverallRating = 4
    };

    Assert.Equal(4, book.AverageRating.Value);
}
```

### 6.2 Unit Tests für StatsService

**Datei:** `BookLoggerApp.Tests/Services/StatsServiceTests.cs`

```csharp
[Fact]
public async Task GetAverageRatingAsync_ReturnsCorrectAverage()
{
    // Arrange
    // ... setup test data

    // Act
    var average = await _statsService.GetAverageRatingAsync(RatingCategory.Characters);

    // Assert
    Assert.Equal(expectedAverage, average, 2);
}

[Fact]
public async Task GetTopRatedBooksAsync_ReturnsBooksSortedByRating()
{
    // Test implementation
}
```

### 6.3 Integration Tests

- Test: Bewertung speichern und laden
- Test: Durchschnitt berechnen mit verschiedenen Kombinationen
- Test: Top-bewertete Bücher abrufen

## Phase 7: Datenmigration & Backwards Compatibility

### 7.1 Migrationsstrategie

1. **Neue Spalten hinzufügen:** Alle neuen Rating-Spalten als nullable hinzufügen
2. **Alte Daten migrieren:** Bestehende `Rating`-Werte nach `OverallRating` kopieren
3. **Backwards Compatibility:** `Rating` Property als obsolet markieren, aber nicht entfernen
4. **Getter/Setter Update:**
   ```csharp
   [Obsolete("Use OverallRating instead")]
   public int? Rating
   {
       get => OverallRating;
       set => OverallRating = value;
   }
   ```

### 7.2 Datenbank-Update Script

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Add new columns
    migrationBuilder.AddColumn<int>(
        name: "CharactersRating",
        table: "Books",
        nullable: true);

    // ... add all other columns

    // Migrate existing data
    migrationBuilder.Sql(
        @"UPDATE Books
          SET OverallRating = Rating
          WHERE Rating IS NOT NULL AND OverallRating IS NULL");
}
```

## Phase 8: Deployment & Rollout

### 8.1 Deployment-Schritte

1. **Database Backup:** Vor der Migration Backup erstellen
2. **Migration ausführen:** `dotnet ef database update`
3. **App deployen:** Neue Version mit erweiterten Features
4. **Monitoring:** Logs überwachen für Fehler bei der Rating-Berechnung

### 8.2 Feature-Flags (Optional)

Möglichkeit, das neue Rating-System über Feature-Flag zu aktivieren:
- Alte Nutzer sehen zunächst nur Overall-Rating
- Neues System schrittweise aktivieren
- Feedback sammeln und iterieren

## Zeitschätzung

| Phase | Geschätzte Zeit |
|-------|----------------|
| Phase 1: Datenmodell | 2-3 Stunden |
| Phase 2: Services | 3-4 Stunden |
| Phase 3: ViewModels | 2-3 Stunden |
| Phase 4: UI-Komponenten | 6-8 Stunden |
| Phase 5: CSS/Styling | 2-3 Stunden |
| Phase 6: Tests | 3-4 Stunden |
| Phase 7: Migration | 1-2 Stunden |
| Phase 8: Deployment | 1-2 Stunden |
| **Gesamt** | **20-29 Stunden** |

## Priorisierung

### Must-Have (MVP)
- Phase 1: Datenmodell-Änderungen
- Phase 2: Basic Service-Implementierung
- Phase 3: ViewModel-Updates
- Phase 4.1-4.3: RatingInput Component + BookDetail Integration
- Phase 5: Basic Styling

### Nice-to-Have (V2)
- Phase 4.5: Erweiterte Stats-Page mit Top-Büchern
- Phase 4.5: Category-Filter
- Detaillierte Visualisierungen (Charts, Graphs)

### Optional
- Feature-Flags
- Erweiterte Analytics
- Export-Funktionalität für Bewertungen

## Offene Fragen

1. **Sollen alle Kategorien immer angezeigt werden, oder nur relevante?**
   - z.B. "World Building" nur für Fantasy/Sci-Fi-Bücher?
   - Lösung: Alle anzeigen, Nutzer kann auslassen

2. **Sollen Bewertungen nur für "Completed" Bücher möglich sein?**
   - Empfehlung: Ja, nur completed books
   - Alternative: Auch "Abandoned" books bewerten lassen

3. **Gewichtung der Kategorien bei AverageRating?**
   - Aktueller Plan: Alle gleichgewichtet
   - Alternative: Overall Rating höher gewichten

4. **UI/UX für mobile Geräte?**
   - Responsive Grid für Rating-Eingabe
   - Touch-optimierte Sterne-Auswahl

## Nächste Schritte

1. Review dieses Plans mit dem Team
2. Offene Fragen klären
3. Mit Phase 1 (Datenmodell) beginnen
4. Nach jeder Phase Review und Testing
5. Schrittweise rollout

---

**Erstellt:** 2025-11-06
**Version:** 1.0
**Autor:** Claude Code
