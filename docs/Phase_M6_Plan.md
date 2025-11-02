# Phase M6: Polish, Testing & Performance

**Zeitrahmen:** 1.5 Wochen (60 Stunden)
**Status:** Planned
**Dependencies:** M2-M5 (alle Features implementiert)
**Blocks:** Release v1.0

---

## Überblick

Die finale Phase vor dem Release. Fokus liegt auf Qualitätssicherung, Performance-Optimierung, umfassenden Tests, CI/CD-Finalisierung und Polish der User Experience.

### Ziele

1. ✅ End-to-End Tests für kritische User Flows
2. ✅ Performance-Optimierungen (DB, UI, Rendering)
3. ✅ Accessibility Verbesserungen (AA-Standard)
4. ✅ Error Handling & User Feedback
5. ✅ CI/CD Pipeline erweitern (Android APK Build, Signing)
6. ✅ Release Preparation (Changelogs, Store Assets)

### Deliverables

- E2E Test Suite (Manual oder Automated)
- Performance Benchmarks & Optimierungen
- Accessibility Audit & Fixes
- Error Handling Framework
- GitHub Actions Workflow (erweitert)
- Release Build (signiertes APK)
- Store Assets (Screenshots, Beschreibung)
- Release Notes (CHANGELOG.md)

---

## Arbeitspaket 1: End-to-End Tests

**Aufwand:** 12 Stunden
**Priorität:** P0

### 1.1 Critical User Flows

**Manual Testing Checklist:**

#### Flow 1: Onboarding & First Book
- [ ] App startet erfolgreich (Cold Start <2s)
- [ ] Dashboard zeigt leeren State
- [ ] Navigiere zu Bookshelf
- [ ] Klicke "+ Add Book"
- [ ] Fülle Formular aus (Title, Author, PageCount, Cover Upload)
- [ ] Speichere Buch
- [ ] Buch erscheint in Bookshelf Grid
- [ ] Klicke auf Buch → Book Detail öffnet sich

#### Flow 2: Reading Session
- [ ] Öffne Book Detail
- [ ] Klicke "Start Reading"
- [ ] Reading View öffnet sich mit Timer
- [ ] Timer läuft korrekt
- [ ] Gib Seitenzahl ein (z.B. 20 Pages)
- [ ] Klicke "End Session"
- [ ] Session wird gespeichert
- [ ] XP wird berechnet und angezeigt
- [ ] Pflanze erhält XP (Level/Progress aktualisiert)
- [ ] Zurück zu Book Detail → Session erscheint in Liste

#### Flow 3: Goal Tracking
- [ ] Navigiere zu Goals Page
- [ ] Klicke "Create Goal"
- [ ] Wähle Goal Type (z.B. Read 5 Books)
- [ ] Setze Target & Date Range
- [ ] Speichere Goal
- [ ] Goal erscheint in Active Goals
- [ ] Komplettiere ein Buch
- [ ] Goal Progress aktualisiert sich (Current +1)
- [ ] Bei Erreichen des Targets: Goal als "Completed" markiert
- [ ] Notification: "Goal Completed" (wenn aktiviert)

#### Flow 4: Plant Lifecycle
- [ ] Dashboard zeigt Starter Plant (gratis)
- [ ] Pflanze ist "Healthy"
- [ ] Warte 3+ Tage (oder manipuliere LastWatered Date)
- [ ] Pflanze Status ändert sich zu "Thirsty"
- [ ] Notification: "Plant needs water"
- [ ] Klicke "Water Plant"
- [ ] Status zurück zu "Healthy"
- [ ] Sammle XP durch Lesen
- [ ] Pflanze levelt up (Level 1 → 2)
- [ ] Coins als Belohnung erhalten
- [ ] Navigiere zu Shop
- [ ] Kaufe neue Pflanze (z.B. Bookworm Fern für 500 Coins)
- [ ] Neue Pflanze ist aktiv

#### Flow 5: Stats & Export
- [ ] Navigiere zu Stats Page
- [ ] Überprüfe Stats (Total Books, Minutes, Streaks)
- [ ] Filter Stats nach Zeitraum (This Week, This Month)
- [ ] Charts werden korrekt angezeigt
- [ ] Navigiere zu Settings
- [ ] Klicke "Export Data" → CSV
- [ ] CSV-Datei wird heruntergeladen
- [ ] Öffne CSV → Daten korrekt formatiert

### 1.2 Automated E2E Tests (Optional: Playwright)

**Location:** `BookLoggerApp.UITests/` (neues Projekt)

**Technologie:** Playwright for .NET oder Appium

**Beispiel:**

```csharp
[Test]
public async Task CanAddBookAndStartReadingSession()
{
    // Arrange
    await Page.GotoAsync("/bookshelf");

    // Act: Add Book
    await Page.ClickAsync("text=+ Add Book");
    await Page.FillAsync("input[name='title']", "Test Book");
    await Page.FillAsync("input[name='author']", "Test Author");
    await Page.ClickAsync("button[type='submit']");

    // Assert: Book appears
    await Page.WaitForSelectorAsync("text=Test Book");

    // Act: Start Reading
    await Page.ClickAsync("text=Test Book");
    await Page.ClickAsync("text=Start Reading");

    // Assert: Reading View opens
    await Page.WaitForSelectorAsync("text=End Session");
}
```

**Entscheidung für MVP:** Manual Testing Checklist reicht, Automated E2E Tests optional für v1.1

### Acceptance Criteria

- [ ] Manual Testing Checklist vollständig abgearbeitet
- [ ] Alle kritischen Flows funktionieren ohne Fehler
- [ ] Test-Protokoll dokumentiert (Bugs gefixt)

---

## Arbeitspaket 2: Performance-Optimierung

**Aufwand:** 12 Stunden
**Priorität:** P0

### 2.1 Performance-Budget (Reminder)

| Metrik | Target | Maximum |
|--------|--------|---------|
| Cold Start (Android) | < 1.5s | < 2.0s |
| Bookshelf Render (20 items) | < 300ms | < 500ms |
| Navigation Latency | < 80ms | < 120ms |
| DB Query (Simple) | < 5ms | < 10ms |
| DB Query (Complex) | < 15ms | < 30ms |
| Memory Footprint | < 100MB | < 150MB |

### 2.2 Profiling & Measurement

#### Tool: BenchmarkDotNet

**Location:** `BookLoggerApp.Benchmarks/` (neues Console-Projekt)

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BookLoggerApp.Infrastructure.Services;

[MemoryDiagnoser]
public class ServiceBenchmarks
{
    private IBookService _bookService = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup InMemory DB & Service
    }

    [Benchmark]
    public async Task GetAllBooks()
    {
        await _bookService.GetAllAsync();
    }

    [Benchmark]
    public async Task SearchBooks()
    {
        await _bookService.SearchAsync("test");
    }
}

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<ServiceBenchmarks>();
    }
}
```

**Run:**
```bash
cd BookLoggerApp.Benchmarks
dotnet run -c Release
```

### 2.3 Database Optimizations

#### Indexes Review

Stelle sicher, dass alle Foreign Keys und häufig abgefragte Felder indiziert sind:

```csharp
// BookConfiguration.cs
builder.HasIndex(b => b.Title);
builder.HasIndex(b => b.ISBN);
builder.HasIndex(b => b.Status);
builder.HasIndex(b => b.DateAdded);

// ReadingSessionConfiguration.cs
builder.HasIndex(rs => rs.BookId);
builder.HasIndex(rs => rs.StartedAt);

// BookGenreConfiguration.cs
builder.HasIndex(bg => bg.BookId);
builder.HasIndex(bg => bg.GenreId);
```

#### Eager Loading

Verhindere N+1 Queries durch Eager Loading:

```csharp
// BookService.GetWithDetailsAsync
public async Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
{
    return await _context.Books
        .Include(b => b.ReadingSessions)
        .Include(b => b.Quotes)
        .Include(b => b.Annotations)
        .Include(b => b.BookGenres)
            .ThenInclude(bg => bg.Genre)
        .AsSplitQuery() // Avoid Cartesian explosion
        .FirstOrDefaultAsync(b => b.Id == id, ct);
}
```

### 2.4 UI Performance

#### Virtualization

Für lange Listen (Bookshelf, Sessions):

```razor
<Virtualize Items="@ViewModel.Books" Context="book">
    <BookCard Book="@book" OnClick="() => NavigateToDetail(book.Id)" />
</Virtualize>
```

#### Lazy Loading Images

```razor
<img src="@book.CoverImagePath" loading="lazy" alt="@book.Title" />
```

#### Debounce Search

```csharp
private System.Timers.Timer? _searchDebounceTimer;

private void OnSearchInputChanged(ChangeEventArgs e)
{
    _searchDebounceTimer?.Stop();
    _searchDebounceTimer = new System.Timers.Timer(300); // 300ms debounce
    _searchDebounceTimer.Elapsed += async (sender, args) =>
    {
        await ViewModel.SearchCommand.ExecuteAsync(null);
        await InvokeAsync(StateHasChanged);
    };
    _searchDebounceTimer.AutoReset = false;
    _searchDebounceTimer.Start();
}
```

### 2.5 Memory Profiling

**Tool:** Visual Studio Profiler oder dotMemory

**Check:**
- Memory Leaks (dispose ViewModels, DbContext)
- Large Objects (Image Caching)
- Collection Sizes (ObservableCollection wächst nicht unkontrolliert)

### Acceptance Criteria

- [ ] Performance-Budget für alle Metriken erreicht
- [ ] Benchmarks durchgeführt und dokumentiert
- [ ] DB-Indexes optimiert
- [ ] Eager Loading für Details-Queries
- [ ] UI Virtualization für lange Listen
- [ ] No Memory Leaks (Profiler-Check)

---

## Arbeitspaket 3: Accessibility (AA-Standard)

**Aufwand:** 8 Stunden
**Priorität:** P1

### 3.1 WCAG 2.1 Level AA Checklist

#### Perceivable

**Kontrast:**
- [ ] Text hat min. 4.5:1 Kontrast zu Hintergrund
- [ ] Buttons/Icons haben min. 3:1 Kontrast
- [ ] Dark Mode hat ausreichende Kontraste

**Tool:** https://webaim.org/resources/contrastchecker/

```css
/* Ensure good contrasts */
:root {
    --text-color: #212121; /* on white: 16.1:1 ✅ */
    --primary-color: #512BD4; /* on white: 6.4:1 ✅ */
}

[data-theme="dark"] {
    --text-color: #f5f5f5; /* on #1a1a1a: 14.7:1 ✅ */
}
```

#### Operable

**Keyboard Navigation:**
- [ ] Alle interaktiven Elemente via Tab erreichbar
- [ ] Focus Indicator sichtbar (Outline)
- [ ] Modals können mit Escape geschlossen werden

```css
/* Focus Indicator */
button:focus,
a:focus,
input:focus {
    outline: 2px solid var(--primary-color);
    outline-offset: 2px;
}
```

**Touch Targets:**
- [ ] Buttons min. 44x44px (Mobile)
- [ ] Genügend Spacing zwischen Tap-Zielen

```css
.btn {
    min-height: 44px;
    min-width: 44px;
    padding: 0.75rem 1.5rem;
}
```

#### Understandable

**Labels:**
- [ ] Alle Inputs haben `<label>` oder `aria-label`
- [ ] Buttons haben beschreibenden Text

```razor
<label for="book-title">Book Title:</label>
<input id="book-title" type="text" @bind="Book.Title" />

<button aria-label="Delete book">🗑️</button>
```

**Error Messages:**
- [ ] Validierungsfehler klar angezeigt
- [ ] Error-State visuell unterscheidbar

#### Robust

**Semantic HTML:**
- [ ] `<nav>`, `<main>`, `<article>` statt nur `<div>`
- [ ] `<button>` statt `<div onclick>`

```razor
<nav class="navbar">
    <ul>
        <li><NavLink href="/">Dashboard</NavLink></li>
    </ul>
</nav>

<main>
    <article>
        <!-- Page Content -->
    </article>
</main>
```

### 3.2 Screen Reader Testing

**Tools:**
- Windows: NVDA (kostenlos)
- macOS: VoiceOver (integriert)
- Android: TalkBack

**Test:**
- [ ] NavMenu ist navigierbar
- [ ] Book Cards werden korrekt angesagt
- [ ] Forms sind ausfüllbar
- [ ] Buttons/Links beschreibend benannt

### Acceptance Criteria

- [ ] WCAG 2.1 AA Checklist vollständig abgearbeitet
- [ ] Kontraste geprüft und angepasst
- [ ] Keyboard Navigation funktioniert
- [ ] Screen Reader Test durchgeführt
- [ ] Focus Indicator sichtbar

---

## Arbeitspaket 4: Error Handling & User Feedback

**Aufwand:** 8 Stunden
**Priorität:** P1

### 4.1 Global Error Handling

**Location:** `BookLoggerApp/Components/Layout/ErrorBoundary.razor`

```razor
@inherits ErrorBoundary

<div class="error-boundary">
    @if (CurrentException != null)
    {
        <div class="error-container">
            <h2>😞 Something went wrong</h2>
            <p>We're sorry, but an unexpected error occurred.</p>
            <button class="btn btn-primary" @onclick="Recover">Try Again</button>
            <details>
                <summary>Error Details</summary>
                <pre>@CurrentException.Message</pre>
            </details>
        </div>
    }
    else
    {
        @ChildContent
    }
</div>

@code {
    protected override void OnError(Exception exception)
    {
        // Log error (TODO: Telemetry)
        System.Diagnostics.Debug.WriteLine($"ERROR: {exception}");
    }
}
```

**Wrap Routes:**

```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <ErrorBoundary>
            <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
        </ErrorBoundary>
    </Found>
</Router>
```

### 4.2 Loading States

**Beispiel:** Bookshelf Loading

```razor
@if (ViewModel.IsBusy)
{
    <div class="loading-container">
        <div class="spinner"></div>
        <p>Loading your books...</p>
    </div>
}
else if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
{
    <div class="alert alert-danger">
        <strong>Error:</strong> @ViewModel.ErrorMessage
        <button @onclick="() => ViewModel.LoadCommand.ExecuteAsync(null)">Retry</button>
    </div>
}
else
{
    <!-- Content -->
}
```

**CSS Spinner:**

```css
.spinner {
    border: 4px solid rgba(0,0,0,0.1);
    border-left-color: var(--primary-color);
    border-radius: 50%;
    width: 50px;
    height: 50px;
    animation: spin 1s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}
```

### 4.3 Toast Notifications (Success/Error)

**NuGet:** Blazored.Toast

```bash
dotnet add package Blazored.Toast --version 4.1.0
```

**Usage:**

```csharp
@inject IToastService ToastService

private async Task SaveBook()
{
    try
    {
        await _bookService.AddAsync(book);
        ToastService.ShowSuccess("Book added successfully!");
    }
    catch (Exception ex)
    {
        ToastService.ShowError($"Failed to add book: {ex.Message}");
    }
}
```

### 4.4 Confirmation Dialogs

**Beispiel:** Delete Book Confirmation

```razor
@if (showDeleteConfirm)
{
    <div class="modal-backdrop" @onclick="CancelDelete">
        <div class="modal-dialog" @onclick:stopPropagation="true">
            <h3>Delete Book?</h3>
            <p>Are you sure you want to delete "@bookToDelete?.Title"? This cannot be undone.</p>
            <div class="modal-actions">
                <button class="btn btn-danger" @onclick="ConfirmDelete">Delete</button>
                <button class="btn btn-secondary" @onclick="CancelDelete">Cancel</button>
            </div>
        </div>
    </div>
}

@code {
    private bool showDeleteConfirm = false;
    private Book? bookToDelete;

    private void RequestDelete(Book book)
    {
        bookToDelete = book;
        showDeleteConfirm = true;
    }

    private async Task ConfirmDelete()
    {
        if (bookToDelete != null)
        {
            await ViewModel.DeleteBookCommand.ExecuteAsync(bookToDelete.Id);
        }
        showDeleteConfirm = false;
    }

    private void CancelDelete()
    {
        showDeleteConfirm = false;
        bookToDelete = null;
    }
}
```

### Acceptance Criteria

- [ ] Global Error Boundary implementiert
- [ ] Loading States für alle async Operationen
- [ ] Toast Notifications für Success/Error
- [ ] Confirmation Dialogs für destruktive Actions
- [ ] User Feedback bei allen kritischen Actions

---

## Arbeitspaket 5: CI/CD Pipeline (Erweitert)

**Aufwand:** 12 Stunden
**Priorität:** P0

### 5.1 GitHub Actions Workflow (Final)

**Location:** `.github/workflows/ci-cd.yml`

```yaml
name: CI/CD

on:
  push:
    branches: [ main, dev ]
  pull_request:
    branches: [ main ]
  release:
    types: [ published ]

jobs:
  test:
    name: Build & Test
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: |
          dotnet restore BookLoggerApp.Core/BookLoggerApp.Core.csproj
          dotnet restore BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
          dotnet restore BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

      - name: Build
        run: |
          dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj -c Release --no-restore
          dotnet build BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj -c Release --no-restore
          dotnet build BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --no-restore

      - name: Run Unit Tests
        run: dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --no-build --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage"

      - name: Publish Test Results
        if: always()
        uses: dorny/test-reporter@v1
        with:
          name: xUnit Tests
          path: "**/TestResults/*.trx"
          reporter: dotnet-trx

      - name: Upload Code Coverage
        uses: codecov/codecov-action@v3
        with:
          files: "**/coverage.cobertura.xml"
          fail_ci_if_error: false

  build-android:
    name: Build Android APK
    needs: test
    runs-on: windows-latest
    if: github.ref == 'refs/heads/main' || github.event_name == 'release'
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Setup Android SDK
        uses: android-actions/setup-android@v3

      - name: Restore & Build Android
        run: |
          dotnet restore BookLoggerApp/BookLoggerApp.csproj
          dotnet build BookLoggerApp/BookLoggerApp.csproj -c Release -f net9.0-android --no-restore

      - name: Publish Android APK
        run: dotnet publish BookLoggerApp/BookLoggerApp.csproj -c Release -f net9.0-android -o ./publish

      - name: Sign APK
        if: github.event_name == 'release'
        env:
          KEYSTORE_PASSWORD: ${{ secrets.KEYSTORE_PASSWORD }}
          KEYSTORE_ALIAS: ${{ secrets.KEYSTORE_ALIAS }}
        run: |
          # Sign APK with Release Keystore (stored in GitHub Secrets)
          # TODO: Implement signing script

      - name: Upload APK Artifact
        uses: actions/upload-artifact@v4
        with:
          name: android-apk
          path: ./publish/*.apk

  build-windows:
    name: Build Windows MSIX
    needs: test
    runs-on: windows-latest
    if: github.ref == 'refs/heads/main' || github.event_name == 'release'
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Restore & Build Windows
        run: |
          dotnet restore BookLoggerApp/BookLoggerApp.csproj
          dotnet build BookLoggerApp/BookLoggerApp.csproj -c Release -f net9.0-windows10.0.19041.0 --no-restore

      - name: Publish Windows MSIX
        run: dotnet publish BookLoggerApp/BookLoggerApp.csproj -c Release -f net9.0-windows10.0.19041.0 -o ./publish-windows

      - name: Upload MSIX Artifact
        uses: actions/upload-artifact@v4
        with:
          name: windows-msix
          path: ./publish-windows/**/*.msix

  release:
    name: Create GitHub Release
    needs: [build-android, build-windows]
    runs-on: ubuntu-latest
    if: github.event_name == 'release'
    steps:
      - name: Download Android APK
        uses: actions/download-artifact@v4
        with:
          name: android-apk
          path: ./artifacts/android

      - name: Download Windows MSIX
        uses: actions/download-artifact@v4
        with:
          name: windows-msix
          path: ./artifacts/windows

      - name: Upload Release Assets
        uses: softprops/action-gh-release@v1
        with:
          files: |
            ./artifacts/android/*.apk
            ./artifacts/windows/*.msix
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### 5.2 Code Signing (Android)

**Keystore erstellen:**

```bash
keytool -genkey -v -keystore booklogger-release.keystore -alias booklogger -keyalg RSA -keysize 2048 -validity 10000
```

**GitHub Secrets:**
- `KEYSTORE_PASSWORD`
- `KEYSTORE_ALIAS`
- `KEYSTORE_FILE` (Base64-encoded Keystore)

**Signing in CI:**

```yaml
- name: Decode Keystore
  run: echo "${{ secrets.KEYSTORE_FILE }}" | base64 --decode > booklogger-release.keystore

- name: Sign APK
  run: |
    jarsigner -verbose -sigalg SHA256withRSA -digestalg SHA-256 \
      -keystore booklogger-release.keystore \
      -storepass ${{ secrets.KEYSTORE_PASSWORD }} \
      ./publish/com.companyname.bookloggerapp-Signed.apk ${{ secrets.KEYSTORE_ALIAS }}
```

### 5.3 Versioning

**BookLoggerApp.csproj:**

```xml
<ApplicationDisplayVersion>1.0.0</ApplicationDisplayVersion>
<ApplicationVersion>1</ApplicationVersion>
```

**Automatische Versionierung (Optional):**

```yaml
- name: Set Version
  run: |
    VERSION=$(git describe --tags --always)
    sed -i "s/<ApplicationDisplayVersion>.*<\/ApplicationDisplayVersion>/<ApplicationDisplayVersion>$VERSION<\/ApplicationDisplayVersion>/" BookLoggerApp/BookLoggerApp.csproj
```

### Acceptance Criteria

- [ ] CI/CD Workflow für Test, Build Android, Build Windows
- [ ] Android APK wird gebaut und signiert
- [ ] Windows MSIX wird gebaut (optional)
- [ ] Artifacts werden zu GitHub Releases hochgeladen
- [ ] Versioning automatisiert

---

## Arbeitspaket 6: Release Preparation

**Aufwand:** 8 Stunden
**Priorität:** P0

### 6.1 CHANGELOG.md

**Location:** `CHANGELOG.md`

```markdown
# Changelog

All notable changes to BookLoggerApp will be documented in this file.

## [1.0.0] - 2025-02-XX

### Added
- 📚 Complete book management (CRUD, Search, Filter)
- 📖 Reading session tracking with timer
- ⭐ Book ratings and reviews
- 💬 Quotes and annotations
- 🎯 Reading goals (Books, Pages, Minutes)
- 🌱 Plant mechanics with XP and leveling
- 🪙 Coin system and plant shop
- 📊 Statistics dashboard (Streaks, Trends, Genre breakdown)
- 🔔 Notifications (Watering reminders, Goal completion)
- ⚙️ Settings (Theme, Language, Notifications, Backup)
- 📤 Data export (CSV, JSON)
- 🎨 2D Grid Bookshelf view
- 🌗 Dark mode support

### Technical
- .NET 9 MAUI Blazor Hybrid
- EF Core SQLite database
- MVVM architecture with CommunityToolkit.Mvvm
- Comprehensive unit & integration tests (>80% coverage)
- CI/CD pipeline with GitHub Actions

## [Unreleased]

### Planned for v1.1
- 2.5D/3D Bookshelf toggle
- Barcode scanner for ISBN lookup
- Goodreads import
- Advanced charts & analytics
```

### 6.2 README.md aktualisieren

**Location:** `README.md`

**Abschnitte hinzufügen:**
- Screenshots (Dashboard, Bookshelf, Reading View, Plant Shop)
- Installation Instructions (APK Download, Sideload)
- Features Liste (mit Icons)
- Tech Stack
- Build Instructions (für Entwickler)
- Contributing Guidelines (optional)
- License Info

### 6.3 Play Store Assets

**Benötigt für Play Store Submission:**

1. **Screenshots (8+):**
   - Dashboard (Pixel 6 Pro: 1440x3120)
   - Bookshelf Grid View
   - Book Detail Page
   - Reading View with Timer
   - Goals Page
   - Plant Widget
   - Stats Dashboard
   - Settings Page

2. **Feature Graphic (1024x500):**
   - Banner mit App-Logo, Titel, Key Features

3. **App Icon (512x512):**
   - High-Res Icon für Store

4. **Short Description (80 chars):**
   - "Track your reading, grow virtual plants, and achieve your reading goals! 📚🌱"

5. **Full Description (4000 chars):**
   - Feature-Liste
   - How it works
   - Privacy info (lokal, keine Tracker)
   - Call to Action

### 6.4 Privacy Policy

**Location:** `docs/PRIVACY.md`

**Inhalt:**
- Data Collection: Nur lokal, keine Cloud
- No Third-Party Tracking
- Optional Telemetrie (Opt-In)
- Data Export/Delete
- Contact Info

**Host:** GitHub Pages oder in-app anzeigen

### Acceptance Criteria

- [ ] CHANGELOG.md vollständig
- [ ] README.md aktualisiert mit Screenshots
- [ ] Play Store Assets erstellt (Screenshots, Icon, Descriptions)
- [ ] Privacy Policy geschrieben und gehostet

---

## Arbeitspaket 7: Final QA & Bug Bash

**Aufwand:** 10 Stunden
**Priorität:** P0

### 7.1 Bug Bash

**Team:** Alle Entwickler + Beta-Tester (intern)

**Zeitrahmen:** 2-3 Tage vor Release

**Ziel:** Alle kritischen Bugs finden und fixen

**Vorgehen:**
1. Jeder Tester bekommt Checkliste (siehe E2E Tests)
2. Freies Exploratory Testing (30 Minuten)
3. Bugs in GitHub Issues loggen (Label: `bug`, Priority: `P0/P1/P2`)
4. Entwickler fixen P0/P1 Bugs sofort
5. P2 Bugs gehen ins Backlog für v1.1

### 7.2 Known Issues Tracking

**Location:** `docs/KNOWN_ISSUES.md`

```markdown
# Known Issues (v1.0)

## Critical (Blocker)
- None (all fixed)

## High
- None (all fixed)

## Medium
- [#42] Dark Mode: Plant Widget Kontrast zu niedrig
  - Workaround: Use Light Mode
  - Fix planned for v1.1

## Low
- [#55] Stats Chart: X-Axis labels overlap on small screens
  - Visual only, data correct
  - Fix planned for v1.1
```

### 7.3 Performance Audit

**Checklist:**
- [ ] Cold Start auf Mittelklasse-Android (Pixel 5): < 2s
- [ ] Bookshelf 50 Bücher: Render < 500ms
- [ ] Navigation zwischen Pages: < 120ms
- [ ] DB Queries (P50): < 10ms
- [ ] Memory Footprint: < 150MB
- [ ] APK Size: < 35MB

**Tools:**
- Android Studio Profiler (CPU, Memory, Network)
- Visual Studio Profiler

### Acceptance Criteria

- [ ] Bug Bash durchgeführt
- [ ] Alle P0/P1 Bugs gefixt
- [ ] Known Issues dokumentiert
- [ ] Performance Audit bestanden
- [ ] No Console Errors
- [ ] No Crashes in Standard Flows

---

## Definition of Done (M6)

- [x] End-to-End Tests (Manual Checklist) vollständig
- [x] Performance-Budget erreicht (alle Metriken)
- [x] Accessibility AA-Standard erreicht
- [x] Error Handling & User Feedback implementiert
- [x] CI/CD Pipeline erweitert (Android APK Build, Signing)
- [x] Release Build erstellt und signiert
- [x] CHANGELOG.md vollständig
- [x] README.md aktualisiert mit Screenshots
- [x] Play Store Assets vorbereitet
- [x] Privacy Policy geschrieben
- [x] Bug Bash durchgeführt
- [x] Alle P0/P1 Bugs gefixt
- [x] Known Issues dokumentiert
- [x] Final QA bestanden
- [x] Ready for Release! 🚀

---

## Post-M6: Release Workflow

### Alpha Release (Week 11)
1. Tag `v1.0.0-alpha.1`
2. GitHub Release mit APK
3. Intern testen (5-10 Personen)
4. Bugfixes → `v1.0.0-alpha.2`

### Beta Release (Week 12)
1. Tag `v1.0.0-beta.1`
2. Play Store Internal Testing Track
3. 50+ Tester einladen
4. Feedback sammeln (1 Woche)
5. Bugfixes → `v1.0.0-beta.2`

### Release Candidate (Week 13)
1. Tag `v1.0.0-rc.1`
2. Play Store Closed Testing Track
3. Final Tests (keine neuen Features)
4. Wenn stable → GA

### General Availability (Week 14)
1. Tag `v1.0.0`
2. Play Store Production Release
3. Staged Rollout: 10% → 50% → 100% (über 3 Tage)
4. Monitor Crashes/ANRs
5. Hotfix-Bereitschaft

---

**Ende Phase M6 Plan**

**🎉 BookLoggerApp v1.0 ist bereit für die Welt! 🚀📚🌱**
