# Bücher-Wunschliste Feature Plan

## Context
Nutzer sollen Bücher auf eine Wunschliste setzen können, die **nicht** in Goals, Stats oder XP einfließen. Da alle Stats/Goals-Queries explizit nach `ReadingStatus.Completed` filtern, wird ein neuer Status `Wishlist = 4` automatisch ausgeschlossen — minimaler Aufwand, maximale Sicherheit.

## Ansatz: ReadingStatus-Erweiterung + WishlistInfo-Entity

**Warum?** Ein neuer Enum-Wert nutzt die gesamte bestehende Infrastruktur (BookService, BookRepository, Shelf-System). Ein leichtgewichtiges `WishlistInfo`-Entity speichert Wunschlisten-Metadaten (Priorität, Empfehlung, Notizen) ohne das Book-Model aufzublähen.

**UI:** Tab-System auf der Bookshelf-Seite ("Regale" | "Wunschliste") — kein neuer Nav-Eintrag nötig, logisch beim Bücher-Management gruppiert.

---

## Phase 1: Domain Layer (BookLoggerApp.Core)

### 1.1 Enum-Erweiterungen
- **`Models/Book.cs`** (Zeile 123-129): `Wishlist = 4` zum `ReadingStatus` enum hinzufügen
- **`Models/Shelf.cs`** (Zeile 38-45): `StatusWishlist = 5` zum `ShelfAutoSortRule` enum hinzufügen

### 1.2 Neues Model: WishlistInfo
- **NEU: `Models/WishlistInfo.cs`**
  - `Guid BookId` (PK + FK, 1:1 mit Book)
  - `WishlistPriority Priority` (Low=0, Medium=1, High=2)
  - `string? RecommendedBy` (max 200)
  - `string? WishlistNotes` (max 1000)
  - `DateTime DateAddedToWishlist`

### 1.3 Neues Enum
- **NEU: `Enums/WishlistPriority.cs`** — `Low = 0, Medium = 1, High = 2`

### 1.4 Navigation Property
- **`Models/Book.cs`**: `public WishlistInfo? WishlistInfo { get; set; }` nach Zeile 81 hinzufügen

### 1.5 Service Interface
- **NEU: `Services/Abstractions/IWishlistService.cs`**
  - `GetWishlistBooksAsync()` — Alle Wunschlisten-Bücher
  - `AddToWishlistAsync(Book book, WishlistInfo? info)` — Buch mit Status=Wishlist erstellen
  - `AddToWishlistByIsbnAsync(string isbn)` — Via Google Books Lookup direkt zur Wunschliste
  - `UpdateWishlistInfoAsync(Guid bookId, WishlistInfo info)` — Metadaten aktualisieren
  - `MoveToLibraryAsync(Guid bookId)` — Wishlist → Planned, WishlistInfo löschen
  - `RemoveFromWishlistAsync(Guid bookId)` — Buch komplett löschen
  - `GetWishlistCountAsync()` — Für Badge-Anzeige
  - `SearchWishlistAsync(string query)` — Suche in Titel/Autor

### 1.6 ViewModel
- **NEU: `ViewModels/WishlistViewModel.cs`**
  - Erbt von `ViewModelBase`, nutzt `[ObservableProperty]` und `[RelayCommand]`
  - Properties: `WishlistBooks`, `WishlistCount`, `SearchQuery`, `SortBy`
  - Add-Formular: `NewTitle`, `NewAuthor`, `NewIsbn`, `NewPriority`, `NewRecommendedBy`, `NewWishlistNotes`
  - Commands: Load, Add, LookupByIsbn, MoveToLibrary, Remove, Search, Sort

---

## Phase 2: Infrastructure Layer (BookLoggerApp.Infrastructure)

### 2.1 EF Core Konfiguration
- **NEU: `Data/Configurations/WishlistInfoConfiguration.cs`**
  - PK auf `BookId`, 1:1 FK zu Book mit `DeleteBehavior.Cascade`
  - Index auf `Priority` und `DateAddedToWishlist`

### 2.2 DbContext
- **`Data/AppDbContext.cs`**: `DbSet<WishlistInfo>` hinzufügen

### 2.3 UnitOfWork
- **`Repositories/IUnitOfWork.cs`**: `IRepository<WishlistInfo> WishlistInfos { get; }` hinzufügen
- **`Repositories/UnitOfWork.cs`**: Lazy-Feld + Property ergänzen

### 2.4 Migration
- `dotnet ef migrations add AddWishlistFeature` — erstellt WishlistInfos-Tabelle

### 2.5 WishlistService
- **NEU: `Services/WishlistService.cs`**
  - Injiziert `IUnitOfWork`, `ILookupService`, `IImageService`
  - `MoveToLibraryAsync`: Status → Planned, DateAdded = Now, WishlistInfo löschen
  - `AddToWishlistByIsbnAsync`: LookupService → Book mit Status=Wishlist + WishlistInfo
  - Queries nutzen `Context.Books.Include(b => b.WishlistInfo).Where(b => b.Status == ReadingStatus.Wishlist)`

### 2.6 ShelfService Update
- **`Services/ShelfService.cs`**: Case `StatusWishlist` im Switch für Auto-Sort-Regeln hinzufügen

### 2.7 ImportExport Update
- **`Services/ImportExportService.cs`**:
  - JSON Export: `.Include(b => b.WishlistInfo)` bei Books-Query
  - JSON Import: WishlistInfo mit-importieren wenn vorhanden
  - CSV: Spalten `WishlistPriority`, `RecommendedBy`, `WishlistNotes` hinzufügen
  - DeleteAll: `WishlistInfos` vor Books entfernen

---

## Phase 3: UI Layer (BookLoggerApp)

### 3.1 Bookshelf.razor — Tab-System
- **`Components/Pages/Bookshelf.razor`**:
  - Tab-Leiste zwischen GoalHeader und bookshelf-header einfügen
  - Zwei Tabs: "📚 Regale" (Standard) | "💝 Wunschliste" (mit Count-Badge)
  - Bestehende Shelves-Logik in `@if (activeTab == "shelves")` wrappen
  - Neuer `@if (activeTab == "wishlist")` Block mit:
    - Suchfeld + Sort-Dropdown (Priorität, Datum, Titel, Autor)
    - Bücherliste als Cards (Cover, Titel, Autor, Priorität-Badge, "Empfohlen von")
    - Aktionsbuttons pro Buch: "📚 Zur Bibliothek" / "🗑️ Entfernen"
    - Empty-State wenn Liste leer
  - FAB ändert sich je nach Tab: "+" → Zur Wunschliste (öffnet Add-Modal)
  - **Add-to-Wishlist Modal**: Titel/Autor/ISBN (mit Scan+Lookup), Priorität, Empfehlung, Notizen
  - `WishlistViewModel` injizieren

### 3.2 BookDetail.razor Updates
- **`Components/Pages/BookDetail.razor`**:
  - Status-Icon: `ReadingStatus.Wishlist => "💝"` im Switch
  - Neuer Abschnitt für Wishlist-Bücher: Priorität, Empfehlung, Notizen anzeigen
  - "📚 Zur Bibliothek verschieben" Button

### 3.3 BookEdit.razor Updates
- **`Components/Pages/BookEdit.razor`**:
  - "Wishlist" Option im Status-Dropdown
  - Bedingte Wishlist-Felder (Priorität, Empfehlung, Notizen) wenn Status=Wishlist
  - Status-Wechsel erkennen: Wishlist→Andere = WishlistInfo löschen, Andere→Wishlist = WishlistInfo erstellen

### 3.4 BookCard.razor
- **`Components/Shared/BookCard.razor`**: `ReadingStatus.Wishlist => "💝"` in GetStatusIcon()

### 3.5 Shelf-Modal Update
- **`Bookshelf.razor`** (Zeile 186-193): "Wishlist" Option im Auto-Sort-Dropdown des Add-Shelf-Modals

### 3.6 CSS
- **NEU: `wwwroot/css/wishlist.css`**:
  - `.bookshelf-tabs` — Flexbox Tab-Leiste
  - `.tab-btn` / `.tab-btn.active` — Tab-Styling mit Primary-Color Unterstrich
  - `.wishlist-count-badge` — Kleine Badge am Tab
  - `.wishlist-book-list` — Card-Liste
  - `.wishlist-card` — Cover-Thumbnail + Infos + Aktionen
  - `.priority-high/medium/low` — Farbige Priorität-Badges
  - `.wishlist-empty` — Empty State
  - `.wishlist-info-section` — Abschnitt in BookDetail
- **`wwwroot/css/app.css`**: CSS-Variable `--status-wishlist: #C9A97F` hinzufügen
- **`wwwroot/index.html`**: `<link rel="stylesheet" href="css/wishlist.css" />` hinzufügen

---

## Phase 4: DI Registration

- **`MauiProgram.cs`**:
  - `builder.Services.AddTransient<IWishlistService, WishlistService>()` in `RegisterBusinessServices()`
  - `builder.Services.AddTransient<WishlistViewModel>()` in `RegisterViewModels()`

---

## Phase 5: Tests (BookLoggerApp.Tests)

### Neue Test-Dateien
- **`Services/WishlistServiceTests.cs`** — Add, MoveToLibrary, Remove, Search, Count, Sort
- **`ViewModels/WishlistViewModelTests.cs`** — Load, Add, Commands
- **`TestHelpers/MockWishlistService.cs`** — Mock für ViewModel-Tests

### Bestehende Tests erweitern
- **`StatsServiceTests.cs`** — Test: Wishlist-Bücher werden nicht gezählt
- **`GoalServiceTests.cs`** — Test: Wishlist-Bücher fließen nicht in Goals ein

---

## Zusatz-Features (im Plan enthalten)

1. **Prioritäts-System** (High/Medium/Low) mit farbigen Badges
2. **"Empfohlen von"** Feld — Wer hat das Buch empfohlen?
3. **Notizen** — Warum will man es lesen?
4. **ISBN-Scan direkt zur Wunschliste** — Barcode scannen, Google Books Lookup, 1-Klick hinzufügen
5. **"Zur Bibliothek verschieben"** — Wishlist → Planned mit einem Klick
6. **Sortierung** — Nach Priorität, Datum, Titel, Autor
7. **Suche** innerhalb der Wunschliste
8. **Count-Badge** am Wunschlisten-Tab
9. **Auto-Sort Shelf** — Neue Shelf mit AutoSortRule=Wishlist möglich

---

## Datei-Übersicht

### Neue Dateien (9)
| Datei | Projekt |
|-------|---------|
| `Models/WishlistInfo.cs` | Core |
| `Enums/WishlistPriority.cs` | Core |
| `Services/Abstractions/IWishlistService.cs` | Core |
| `ViewModels/WishlistViewModel.cs` | Core |
| `Data/Configurations/WishlistInfoConfiguration.cs` | Infrastructure |
| `Services/WishlistService.cs` | Infrastructure |
| `wwwroot/css/wishlist.css` | App |
| `Services/WishlistServiceTests.cs` | Tests |
| `TestHelpers/MockWishlistService.cs` | Tests |

### Zu ändernde Dateien (14)
| Datei | Änderung |
|-------|----------|
| `Core/Models/Book.cs` | Enum + Nav Property |
| `Core/Models/Shelf.cs` | Enum-Erweiterung |
| `Infrastructure/Data/AppDbContext.cs` | DbSet |
| `Infrastructure/Repositories/IUnitOfWork.cs` | Repository Property |
| `Infrastructure/Repositories/UnitOfWork.cs` | Lazy-Feld |
| `Infrastructure/Services/ShelfService.cs` | Switch-Case |
| `Infrastructure/Services/ImportExportService.cs` | Include + Spalten |
| `App/MauiProgram.cs` | DI Registration |
| `App/Components/Pages/Bookshelf.razor` | Tab-System + Wishlist UI |
| `App/Components/Pages/BookDetail.razor` | Wishlist-Section |
| `App/Components/Pages/BookEdit.razor` | Status-Dropdown + Felder |
| `App/Components/Shared/BookCard.razor` | Status-Icon |
| `App/wwwroot/css/app.css` | CSS Variable |
| `App/wwwroot/index.html` | CSS Link |

---

## Verifikation

1. **Build:** `dotnet build BookLoggerApp.sln` — Keine Compile-Fehler
2. **Migration:** `dotnet ef migrations add AddWishlistFeature` + verifizieren
3. **Tests:** `dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj` — Alle grün
4. **Manuell testen:**
   - Wunschlisten-Tab auf Bookshelf öffnen
   - Buch zur Wunschliste hinzufügen (manuell + ISBN-Scan)
   - Priorität/Empfehlung/Notizen setzen
   - "Zur Bibliothek verschieben" — Buch erscheint als Planned
   - Stats/Goals prüfen: Wunschlisten-Bücher dürfen nicht gezählt werden
   - Sortierung + Suche in der Wunschliste
   - Import/Export mit Wunschlisten-Büchern
