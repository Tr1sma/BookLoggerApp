# Erweiterte Statistiken — Design Spec

## Kontext

Die bestehende Stats-Seite zeigt grundlegende Kennzahlen (Bücher, Seiten, Streak, Genre-Verteilung, Bewertungen, XP/Level). Nutzer wünschen sich tiefere Einblicke in ihre Lesegewohnheiten — zeitliche Muster, Tempo-Trends und Buchanalysen. Diese Erweiterung fügt 12 neue Statistiken hinzu, organisiert in einem 3-Tab-System.

## Entscheidungen

| Thema | Entscheidung |
|---|---|
| Platzierung | 3-Tab-System auf der Stats-Seite: Übersicht (bestehend) \| Trends \| Analysen |
| Chart-Library | Blazor-ApexCharts (NuGet) — native Razor-Komponenten, eingebaute Heatmap + Radar |
| Architektur | Ansatz B: Separate Komponenten + ViewModels pro Tab, neuer IAdvancedStatsService |
| Farbschema | Ausschließlich bestehende CSS-Variablen (--primary-color #D4A574, --accent-color #C9A97F, etc.) |

## Statistiken

### Tab 2: Trends (7 Statistiken)

1. **Lese-Kalender (Heatmap)** — GitHub-Style Contributions-Kalender. Zeigt pro Tag die Lesedauer als Farbintensität. Jahresmodus mit Pfeilen zum Wechseln. ApexCharts Heatmap-Typ. Farbskala: `#251E15` → `#3D3126` → `#8B7355` → `#C9A97F` → `#D4A574`.

2. **Wochentag-Verteilung** — Balkendiagramm (Mo–So). Y-Achse: Gesamtminuten pro Wochentag. Höchster Tag hervorgehoben mit --primary-color. ApexCharts Bar-Typ.

3. **Tageszeit-Analyse** — 4 Cards (Morgens/Mittags/Abends/Nachts) mit Prozent-Anteil. Höchster Anteil visuell hervorgehoben. Fun-Label basierend auf der dominanten Tageszeit:
   - 05:00–11:59 → Frühleser 🌅
   - 12:00–16:59 → Tagträumer ☀️
   - 17:00–21:59 → Abendleser 🌙
   - 22:00–04:59 → Nachteule 🦉
   
   Berechnung: `ReadingSession.StartedAt` (UTC) → Lokalzeit konvertieren, dann Bucket zuordnen.

4. **Session-Längen Verteilung** — Histogramm mit 5 Buckets: <15 Min, 15–30, 30–60, 1–2h, >2h. Durchschnittliche Session-Länge als Untertitel. ApexCharts Bar-Typ.

5. **Monatlicher Leseverlauf** — Balkendiagramm mit abgeschlossenen Büchern pro Monat. Aktuelles Jahr, Anzahl über jedem Balken. ApexCharts Bar-Typ.

6. **Lese-Geschwindigkeit** — Kompakte Stat-Card. Seiten/Stunde berechnet aus `PagesRead / (Minutes / 60)` über die letzten 30 Tage. Trend vs. vorherigen Monat mit Grün (#88A67E) für Verbesserung.

7. **Durchschnittliche Lesedauer pro Buch** — Kompakte Stat-Card. Berechnet aus `DateCompleted - DateStarted` für abgeschlossene Bücher. Letzter Monat Durchschnitt. Trend vs. vorherigen Monat.

### Tab 3: Analysen (5 Statistiken)

8. **Jahresvergleich** — Zwei wählbare Jahre nebeneinander. Verglichene Metriken: Bücher, Seiten, Stunden, Durchschnittsbewertung. Horizontale Balken mit Year-Selector Chips. Jahr-Auswahl basierend auf `IStatsService.GetActiveReadingPeriodsAsync()`.

9. **Genre-Radar** — Spinnennetz-Diagramm der Genre-Verteilung nach Buchanzahl. ApexCharts Radar-Typ. Maximal Top-8 Genres (Rest als "Sonstige"). Datenfarbe: `#D4A574` mit transparenter Füllung.

10. **Abschlussquote** — Donut-Chart: Abgeschlossen (#D4A574) vs. Abgebrochen (#A67874). Prozentzahl in der Mitte. Legende mit absoluten Zahlen rechts daneben. ApexCharts Donut-Typ. Nur Bücher mit Status Completed oder Abandoned (Planned/Reading/Wishlist ignoriert).

11. **Buchlängen-Vorliebe** — Horizontale Fortschrittsbalken mit 4 Kategorien: Kurz (<200 S.), Mittel (200–400), Lang (400–600), Episch (>600). Absolute Zahlen und Balkenlänge proportional zum Maximum. Nur abgeschlossene Bücher mit PageCount.

12. **Meistgelesene Autoren** — Ranking-Liste der Top 5 Autoren nach Buchanzahl. Platz 1 mit Primary-Gradient Badge, Rest mit Card-BG Badge. Zeigt Bücher-Anzahl und Gesamtseiten. Nur abgeschlossene Bücher.

## Architektur

### Neue Dateien

| Datei | Zweck |
|---|---|
| `Core/Services/Abstractions/IAdvancedStatsService.cs` | Interface für alle neuen Queries |
| `Infrastructure/Services/AdvancedStatsService.cs` | Implementierung, nutzt IUnitOfWork |
| `Core/ViewModels/StatsTrendsViewModel.cs` | ViewModel für Trends-Tab |
| `Core/ViewModels/StatsAnalysesViewModel.cs` | ViewModel für Analysen-Tab |
| `Components/Shared/StatsTrends.razor` | Trends-Tab Blazor-Komponente |
| `Components/Shared/StatsAnalyses.razor` | Analysen-Tab Blazor-Komponente |
| `wwwroot/css/stats-advanced.css` | CSS für beide neuen Tabs |

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Components/Pages/Stats.razor` | Tab-Bar einfügen, bestehender Content → Tab "Übersicht", Lazy-Loading neuer Tabs |
| `MauiProgram.cs` | IAdvancedStatsService + 2 ViewModels als Transient registrieren |
| `BookLoggerApp.csproj` | NuGet Blazor-ApexCharts hinzufügen |
| `wwwroot/index.html` | CSS-Link stats-advanced.css |

### IAdvancedStatsService

```csharp
public interface IAdvancedStatsService
{
    // Trends
    Task<Dictionary<DateTime, int>> GetReadingHeatmapAsync(int year, CancellationToken ct = default);
    Task<Dictionary<DayOfWeek, int>> GetWeekdayDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetTimeOfDayDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetSessionLengthDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<int, int>> GetMonthlyVolumeAsync(int year, CancellationToken ct = default);
    Task<(double Current, double Previous)> GetReadingSpeedTrendAsync(CancellationToken ct = default);
    Task<(double CurrentAvg, double PreviousAvg)> GetAverageFinishTimeTrendAsync(CancellationToken ct = default);

    // Analysen
    Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default);
    Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default);
    Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default);
}
```

### Hilfstypen (in Core/Models/)

```csharp
public record YearStats(int Year, int BooksCompleted, int PagesRead, int MinutesRead, double AverageRating);
public record AuthorStats(string Author, int BookCount, int TotalPages);
```

### Tab-System in Stats.razor

```razor
@* Tab-Bar *@
<div class="stats-tab-bar">
    <button class="@TabClass(0)" @onclick="() => SetTab(0)">Übersicht</button>
    <button class="@TabClass(1)" @onclick="() => SetTab(1)">Trends</button>
    <button class="@TabClass(2)" @onclick="() => SetTab(2)">Analysen</button>
</div>

@switch (activeTab)
{
    case 0:
        @* Bestehender Stats-Content (unverändert) *@
        break;
    case 1:
        <StatsTrends />
        break;
    case 2:
        <StatsAnalyses />
        break;
}
```

Komponenten laden ihre Daten in `OnInitializedAsync`. Da sie nur bei Tab-Wechsel gerendert werden, entsteht automatisch Lazy-Loading.

### ApexCharts Theme-Konfiguration

Globale Options für alle Charts, damit sie zum BookHeart-Design passen:

```csharp
private ApexChartOptions<T> GetBaseOptions<T>() where T : class => new()
{
    Chart = new Chart { Background = "transparent", ForeColor = "#C9B5A0" },
    Colors = new List<string> { "#D4A574", "#C9A97F", "#8B7355" },
    Grid = new Grid { BorderColor = "#3D3126" },
    Tooltip = new Tooltip
    {
        Theme = "dark",
        Style = new TooltipStyle { FontSize = "12px" }
    }
};
```

## Verifizierung

1. **Build:** `dotnet build BookLoggerApp.sln` — keine Errors
2. **Tests:** `dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj` — alle bestehen
3. **Manuelle Tests auf Android:**
   - Stats-Seite öffnen → 3 Tabs sichtbar
   - Tab "Übersicht" → bestehende Stats unverändert
   - Tab "Trends" → alle 7 Statistiken laden korrekt, Charts rendern, Heatmap zeigt echte Daten
   - Tab "Analysen" → alle 5 Statistiken laden, Jahresvergleich wechselt Jahre, Radar zeigt Genres
   - Tab-Wechsel ist flüssig (kein Delay beim ersten Laden)
   - Farben passen zum Rest der App (keine Gelbtöne, nur Beige/Braun-Palette)
4. **Edge Cases:** Leere Daten (neuer User ohne Sessions/Bücher) → sinnvolle Leer-States
