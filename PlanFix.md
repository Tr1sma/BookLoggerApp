# Plan: Verbleibende Code-Review-Befunde fixen (V12)

## Context

`CODE_REVIEW_TODO.md` listet 136 Befunde aus einem vollständigen Code-Review. **Alle [hoch] (17) und [mittel] (47) sind behoben** (`[x] BEHOBEN`), der eine offene [mittel]-Punkt ist VERWORFEN (kein Defekt, Widget-Integer-Division, Z.309). Es bleiben **63 offene [niedrig]-Befunde**. Keiner blockiert den Release; es sind Lokalisierungs-Lücken, Off-Theme-CSS, Timezone-Restkanten, Robustheits-/Konsistenz-Nits, Perf und Dead-Code.

Ziel: alle 63 abarbeiten, **priorisiert in Phasen** — erst user-sichtbar (Release-Politur), dann Robustheit, zuletzt reine Code-Qualität (Post-Launch-tauglich, aber im selben Plan). Entscheidungen des Users: Books.razor erst Erreichbarkeit prüfen; `AverageRating`-Spice-Ausschluss und `MoveToLibrary`-DateAdded als reine Bugfixes umsetzen.

Build verifiziert: gesamte Solution baut clean (exit 0).

## Konventionen (projektweit, gelten für alle Phasen)

- **Neue resx-Keys**: Eintrag in BEIDE Dateien (`AppResources.resx` EN + `AppResources.de.resx` DE), gleiche `{0}`-Platzhalter. Sonst failt `AppResourcesCoverageTests`. Key-Präfix-Konvention aus CLAUDE.md (`Error_*`, `Common_*`, `Paywall_*`, …).
- **ViewModel-Strings**: `Tr("Key", args...)` (Ambient-Localizer in `ViewModelBase`).
- **Razor-Strings**: `@inject IStringLocalizer<AppResources> L` → `L["Key"]`.
- **CSS-Farben**: nur Theme-Variablen aus `app.css` (`--text-secondary`, `--status-abandoned`, …). Kein pures Rot/Grün/Grau, kein reines Weiß/Schwarz.
- **CancellationToken**: neue/erweiterte async-Methoden nehmen `CancellationToken ct = default` und reichen an terminalen EF-Call durch.
- **Tests**: pro Verhaltensänderung ein xUnit-Test (FluentAssertions, AAA). Dead-Code-Entfernung braucht keinen neuen Test, aber bestehende Tests müssen grün bleiben.
- **Vault**: bei gelöschten/umbenannten Klassen die Obsidian-`.md` (`codebase-map`) nachziehen.

---

## Phase 0 — Triage: Books.razor (`pages`)

**TODO Z.853.** `BookLoggerApp/Components/Pages/Books.razor` ist totes Debug-Scaffold (raw `<input>`/`<ul>`, `@onclick="Vm.AddAsync"` direkt).

**Erreichbarkeit geprüft:** `@page "/books"` ist die EINZIGE Referenz auf die plain-`/books`-Route. Alle realen Navigationen gehen zu `/bookshelf` (Bibliotheks-Liste) bzw. `/books/{id}`, `/books/new`, `/books/{id}/edit` (= BookDetail/BookEdit). BottomNavBar/NavMenu verlinken `/books` nicht.

→ **Entscheidung: Route entfernen.** `Books.razor` löschen (kein Inbound-Link, `BookListViewModel` bleibt für andere Nutzung erhalten). DI-Registrierung des VM prüfen — bleibt, falls anderswo referenziert.

---

## Phase 1 — Lokalisierungs-Lücken (user-sichtbar, DE/EN)

Alle hardcodierten Strings durch resx-Keys ersetzen. Neue Keys in EN+DE.

| # | Datei | Fix |
|---|---|---|
| Z.232 | `BookLoggerApp/Services/FileSaverService.cs:20` | Share-Title-Parameter statt Literal `'Export Data'`; Caller übergibt lokalisierten Titel (`Common_*` Key). |
| Z.522 | `Infrastructure/Services/PromoCodeService.cs` (33,40,48-54) | Result-Keys statt EN-Literale zurückgeben; Auflösung im `PaywallViewModel` via `Tr(...)`. 4 neue Key-Varianten (`Promo_*`) EN+DE. Tier-Name + Dauer als Args. |
| Z.563 | `Components/Pages/Dashboard.razor` + `Stats.razor` `FormatMinutes` | Geteilten lokalisierten Helper nutzen; bestehende Keys `Common_Time_Minutes`/`Common_Time_HoursMinutes` (wie `BookDetail.razor:486`). Duplikate entfernen. |
| Z.841 | `Core/ViewModels/GoalsViewModel.cs:260` | Literal `"Fehler beim Laden der Bücher"` → `Tr("Error_FailedTo_LoadBooks")` (Key existiert ggf. schon — sonst anlegen). |
| Z.859 | `Infrastructure/Services/ShareCardService.cs` (110,139-159,224,670,895,960-974) | `IStringLocalizer<AppResources>` injizieren **oder** vorlokalisierte Strings via `StatsShareData`/`BookShareData` durchreichen. PNG-Labels ('Reading Recap','Books Read',…,'HIGHLY RECOMMENDED','Track your reading journey', CategoryLabels) über resx. Neue Key-Gruppe `ShareCard_*` EN+DE. **Hinweis:** Service liegt in Infrastructure; sauberer ist Strings vom UI/VM vorlokalisiert zu übergeben, um die Service-Schicht localizer-frei zu halten — Variante prüfen. |

**Verify:** `AppResourcesCoverageTests` grün (deckt fehlende/leere DE-Keys ab).

---

## Phase 2 — Theme / CSS (visuell)

| # | Datei | Fix |
|---|---|---|
| Z.251 | `Components/Shared/GoalCard.razor:12` | Genre-Badge-Farbe: 6-stelliger Fallback `#666666` ODER `rgba()` in C# aus Hex berechnen statt Alpha-Suffix an evtl. 3-stelligen Hex hängen. |
| Z.577 | `Components/Shared/LevelUpCelebration.razor` (36,47) | Inline `#e0e0e0`/`#999` → CSS-Klassen mit `var(--text-secondary)`/`var(--text-muted)` in `celebrations.css` (wie `SessionCompleteCelebration`). |
| Z.847 | `Components/Pages/Goals.razor` `<style>` (310,430,433) | Bootstrap `#dc3545`/`#28a745` → `var(--status-abandoned)`/`var(--status-completed)`. |
| Z.742 | `Components/Shared/PlantShopCard.razor` + `DecorationShopCard.razor` | Inline `style="display:flex;flex-direction:column"` in `.plant-shop-card` CSS verschieben. Optional gemeinsame `ShopCardShell`-Komponente extrahieren (parametrisiert name/desc/image/cost/unlockLevel/isLegendary) — Markup-Duplikat reduzieren. |

**Verify:** Visuelle Sichtprüfung über `/run` (Plant-/Decoration-Shop, Level-Up-Modal, Goals-Seite).

---

## Phase 3 — Korrektheit: Timezone-Reste + Verhaltens-Bugfixes

| # | Datei | Fix |
|---|---|---|
| Z.369 | `Core/ViewModels/DashboardViewModel.cs` (76-90) | "This Week" auf lokale Kalenderwoche ankern: `DateTime.Now.Date` statt `UtcNow.Date`; UTC-gespeicherte `DateCompleted`/Session-Timestamps vor Vergleich via `LocalTimeHelper` konvertieren. Konsistent mit Goals (local-midnight). |
| Z.382 | `Infrastructure/Services/AdvancedStatsService.cs` (24,276) | Jahres-Heatmap/`BuildYearStats`: halboffenes Intervall `[startOfYear, startOfNextYear)` statt `23:59:59`-inklusiv — analog `BookRepository.GetCountByCompletionYearAsync` (94-102). Ggf. Repository-Vergleich auf `< endExclusive` umstellen. |
| Z.755 | `Core/Models/Book.cs:104-130` `AverageRating` | **Bugfix (User-OK):** `SpiceLevelRating` aus dem ungewichteten Schnitt ausschließen (Spice = Intensität, keine Qualität). Wirkt auf `GetTopRatedBooksAsync`, `BuildYearStatsAsync`, `GetAverageRatingAsync`. Test: Buch mit hohem Spice + mittlerer Qualität → Schnitt unverändert von Spice. |
| Z.344 | `Infrastructure/Services/WishlistService.cs:121` `MoveToLibraryAsync` | **Bugfix (User-OK):** `book.DateAdded = UtcNow` nicht überschreiben, wenn bereits gesetzt — Original-Hinzufügedatum erhalten. Test: Wishlist-Buch mit altem DateAdded → nach Move unverändert. |

**Verify:** Tests für AverageRating/MoveToLibrary; bestehende Stats-Tests grün.

---

## Phase 4 — Input-Robustheit / Validierung

| # | Datei | Fix |
|---|---|---|
| Z.193 | `Infrastructure/Services/WishlistService.cs` `SearchWishlistAsync` (163-176) | `IsNullOrWhiteSpace(query)`-Guard wie `QuoteService`/`AnnotationService`; bei leer → `GetWishlistBooksAsync(ct)`. Behebt NRE bei null. |
| Z.270 | `Core/Helpers/SpineColorHelper.cs` (78-88) | Hex normalisieren: 3-stellig → 6 expandieren, 8-stellig Alpha strippen, Hex-Zeichen validieren; nur bei echtem Parse-Fail Hash-Fallback. |
| Z.363 | `Infrastructure/Services/LookupService.cs` `LookupByISBNAsync` (38-48) | ISBN auf Ziffern (+ End-`X`) normalisieren, Länge 10/13 prüfen; bei ungültig früh mit klarer Meldung zurück statt erfolgloser API-Query. |
| Z.613 | `Core/ViewModels/WishlistViewModel.cs` (137-138) | `NewTitle`/`NewAuthor` nur bei `!IsNullOrWhiteSpace(metadata.*)` überschreiben — wie `BookEditViewModel.ApplyMetadataToBookAsync` (442-447). Verhindert Verlust von User-Eingaben. |
| Z.666 | `Infrastructure/Services/AnnotationService.cs` + `QuoteService.cs` `AddAsync` | Text trimmen, bei `IsNullOrWhiteSpace` `ValidationException` werfen (vor Cap-Zählung). Optional `AnnotationValidator`/`QuoteValidator` via `IValidationService`. Title/Context analog trimmen. |

**Verify:** Service-Tests für null/empty/overlong inputs.

---

## Phase 5 — Service- & Daten-Layer-Robustheit

| # | Datei | Fix |
|---|---|---|
| Z.206 | `Infrastructure/Services/PlantService.cs` (562-598, 622-640) | Status-Recompute (pure) von Persistenz trennen. Getter (`GetAllAsync`, `GetByIdAsync`, `GetActivePlantAsync`, `CalculateTotalXpBoostAsync`, `GetPlantsNeedingWaterAsync`) dürfen kein `SaveChanges`; Phoenix-`LastWatered`-Reset nur in echten Mutatoren. Behebt Write-on-Read. |
| Z.213 | `Infrastructure/Services/ImageService.cs` (~201-205) | `OperationCanceledException` (wenn `ct.IsCancellationRequested`) vor generischem catch rethrowen; nur echte Download-/Format-Fehler → null. |
| Z.528 | `Infrastructure/Services/ImageService.cs` (39-42) + `MauiProgram.cs` | `new HttpClient()` entfernen; via `AddHttpClient<IImageService, ImageService>()` registrieren und injizieren. Optionalen Test-Ctor (HttpClient) behalten. |
| Z.350 | `Infrastructure/Services/ShelfService.cs` `GetShelfByIdAsync` (32-45) | Filter `!s.IsHiddenByEntitlement` ergänzen (wie `GetAllShelvesAsync`), damit Downgrade-versteckte Regale nicht per Id erreichbar bleiben. |
| Z.357 | `Infrastructure/Services/EntitlementService.cs:107` | `EntitlementChangeReason.Refresh` einführen; nur `Raise()` wenn `previous != reloaded.Tier`. |
| Z.679 | `Core/ViewModels/AppStartupViewModel.cs` (171-174, 256-259) | Idempotenz-Short-Circuit: `ApplyPurchaseAsync` nur wenn PurchaseToken/Tier vom gespeicherten abweicht — keine redundanten Writes/Events bei jedem Resume. |
| Z.263 | `Infrastructure/Services/ReviewPromptService.cs` (17-45) | Bei `UpdateSettingsAsync`-Fehler Cache invalidieren, damit Phantom-Increment verworfen wird (kein verschwendeter Monats-Slot). |
| Z.375 | `Infrastructure/Services/ProgressService.cs` `EndSessionAsync` | Die 4+ Coin/XP-Schreibvorgänge eines Session-Abschlusses als eine serialisierte/transaktionale Einheit fassen (BeginTransaction über die Kette ODER dokumentierte bewusste Nicht-Atomarität). Single-user-Risiko niedrig — mind. Kommentar/`#region`-Hinweis. |
| Z.187 | `Infrastructure/Services/GoalService.cs` `CalculateGoalProgressAsync` (187-216) | Persist-Pfad nicht auf der no-tracking-Instanz mit neu zugewiesenen `GoalGenres` ausführen — Display-Projektion von der persistierten Entität trennen. SQLite-Test (nicht InMemory) für Auto-Complete eines genre-gefilterten Ziels. |
| Z.200 | `Infrastructure/Data/DbInitializer.cs` `ApplyMigrationPragmasAsync` (178-194) | `PRAGMA journal_mode=WAL` explizit vor `synchronous=NORMAL` setzen und Rückgabe loggen — ODER Kommentar an realen journal_mode anpassen. Zentral im `UseSqlite`-Setup verankern. **Erst prüfen** wo journal_mode real gesetzt wird. |
| Z.736 | `Infrastructure/Repositories/UnitOfWork.cs` (115-129) | `IAsyncDisposable` implementieren, `_transaction.DisposeAsync()` awaiten, bei offener TX `RollbackAsync` (+ Log); `ObjectDisposedException.ThrowIf(_disposed)` in public async-Methoden. |
| Z.673 | `Infrastructure/Data/SchemaDriftGuard.cs` | Test ergänzen, der `ExpectedTables`-Spaltenliste gegen `AppDbContextModelSnapshot`/`context.Model` abgleicht und bei Drift failt (analog `AppResourcesCoverageTests`). Hartcodiertes SQL bleibt, aber Drift wird sichtbar. |
| Z.711 | `BookLoggerApp/Services/MigrationService.cs` (11,36-53) | Persistentes Log bei >256 KB rotieren/truncaten; `MemoryLog`-Länge begrenzen; volle Dateipfade nur als Dateiname loggen. |
| Z.705 | `BookLoggerApp/Services/ScannerService.cs` (44-51) | `Application.Current.Windows[0].Page` statt obsoletem `MainPage`; bei null Fehler/Toast statt stillem null-Return. Typo `MainPge` im Kommentar fixen. |
| Z.181 | `Core/Services/Analytics/AnalyticsConsentGate.cs` (74-97) | `InitializeAsync` + `OnSettingsChanged` durch ein privates `ApplyConsent(settings)` vereinheitlichen: setzt `_initialized=true`, vergleicht gegen zuletzt *applied* Wert, feuert `ConsentChanged` auch bei Erstanwendung. |
| Z.557 | `BookLoggerApp/Services/FilePickerService.cs` (54-94) | Cache-Kopie mit Guid-Präfix-Namen (Kollisionsschutz); Kopie nach Konsum löschen bzw. Ownership dokumentieren. |

**Verify:** Service-/SQLite-Tests; Build der Infrastructure (lokal, nicht CI).

---

## Phase 6 — CancellationToken-Konsistenz

| # | Datei | Fix |
|---|---|---|
| Z.515 | `Core/Services/Abstractions/IShelfService.cs` (+ `ShelfService.cs`) | Alle Methoden `CancellationToken ct = default`; an `CreateDbContextAsync(ct)`/`ToListAsync(ct)`/`SaveChangesAsync(ct)` durchreichen — wie `WishlistService`. |
| Z.245 | `Infrastructure/Repositories/Repository.cs` (52-94) | `UpdateAsync`/`DeleteAsync`/`DeleteRangeAsync`: CS1998 beseitigen (`return Task.CompletedTask` non-async ODER tatsächlich nutzen). Toten if/else-Zweig in `UpdateAsync` (beide rufen `_dbSet.Update`) zusammenlegen. `ct` nutzen oder als bewusst ignoriert dokumentieren. |
| Z.570 | `Infrastructure/Repositories/Specific/ReadingSessionRepository.cs` + `BookRepository.cs` | Eager-Loading-Vertrag dokumentieren/angleichen: wenn `Moods` für Range-Analytics nötig → `.Include(rs => rs.Moods)` in `GetSessionsInRangeAsync` (oder explizites Flag). Caller der no-Include-Listen auf angenommene-aber-ungeladene `BookGenres` auditieren. |

**Verify:** Build; bestehende Repo-Tests grün.

---

## Phase 7 — Analytics-Härtung

| # | Datei | Fix |
|---|---|---|
| Z.501 | `Core/Services/Analytics/AnalyticsParamNames.cs` (86-94) + `AnalyticsParamBuilder.cs` (66-73) | Von Denylist auf **Allowlist** erlaubter Param-Keys umstellen; unbekannte Keys in DEBUG ablehnen. Comparer des internen Dictionary an `OrdinalIgnoreCase` der Forbidden-Liste angleichen. |
| Z.508 | `Core/Services/Analytics/AnalyticsParamBuilder.cs` (10,82-83) | PII-Tripwire-Schwelle von Storage-Truncation-Länge trennen: `MaxStringLength` auf 100 (Firebase-Cap) anheben ODER separate Konstanten; DEBUG/RELEASE-Divergenz beseitigen. |
| Z.652 | `Core/Infrastructure/AnalyticsBootstrapper.cs` (12-16) + `UserPropertiesPublisher.cs` (27-30,44-47) | `Install` → `InstallCrashReporter` umbenennen (oder Analytics mitverdrahten). Geschluckte Exceptions in `UserPropertiesPublisher` an `ICrashReportingService.RecordNonFatal` (consent-permitting) statt nur `Debug.WriteLine`. |

**Verify:** Analytics-Unit-Tests (Param-Builder Guard, Allowlist-Reject in DEBUG).

---

## Phase 8 — Stats: Perf & Korrektheit

| # | Datei | Fix |
|---|---|---|
| Z.389 | `Infrastructure/Services/AdvancedStatsService.cs` `GetReadingSpeedTrendAsync` (122-142) | Gleitendes 30-Tage-Fenster statt Kalendermonats-Grenzen (wie `GetAverageFinishTimeTrendAsync` 30/60) ODER bei zu wenig Sessions "nicht genug Daten" anzeigen. |
| Z.749 | `Infrastructure/Services/AdvancedStatsService.cs` `GetGenreRadarDataAsync` (198-208) | Redundantes zweites `GroupBy` entfernen → `.ToDictionary(g => g.Key, g => g.Count())` nach erstem GroupBy. |
| Z.761 | `Infrastructure/Services/AdvancedStatsService.cs` (109,188-195,215,225,252,274) + `StatsService.cs` (134,229) | Wo möglich DB-seitig aggregieren / nur benötigten Status/Jahr `Where`-filtern statt `GetAllAsync` + In-Memory — analog optimierten Methoden (`GetTotalPagesReadAsync`, `GetBooksByGenreAsync`). |

**Verify:** Stats-Tests grün; Ergebnis-Gleichheit vor/nach (Aggregations-Snapshot-Test).

---

## Phase 9 — Page-Lifecycle / Component-Konsistenz

| # | Datei | Fix |
|---|---|---|
| Z.225 | `Components/Pages/Stats.razor` `OnLocationChanged` (~484) | Letzter ungeschützter async-void-Handler: await-Body in try/catch (wie die 6 bereits gefixten Handler). |
| Z.238 | `Components/Pages/BookDetail.razor` `OnBookShareCardReady` (625) | Zu synchronem `void` + `_ = InvokeAsync(async () => {...})` umbauen — wie `BookEdit.razor:422` — damit Exceptions beobachtet bleiben. |
| Z.717 | `Components/Pages/Stats.razor` (439,467-495) | Mehrfach-Load konsolidieren: `_currentUrl` in `OnInitializedAsync` initialisieren, nur ein Reload-Trigger (`OnParametersSetAsync` ODER `LocationChanged`). |
| Z.730 | `Components/Pages/PlantShop.razor` `OnParametersSetAsync` (341-351) | Redundante `LoadCommand`-Aufrufe entfernen (Seite hat keine Route-Parameter) ODER First-Load-Flag. |
| Z.723 | `Components/Pages/Goals.razor` (503) | `@inherits ObservableComponentBase` + `Observe(ViewModel)` statt manueller `PropertyChanged`-Verdrahtung/`Dispose` (wie alle anderen Seiten). |

**Verify:** `/run` Smoke-Test der betroffenen Seiten (kein Doppel-Flicker, keine unbehandelten Crashes bei Navigation).

---

## Phase 10 — Dead-Code & Qualität (Post-Launch-tauglich)

| # | Datei | Fix |
|---|---|---|
| Z.685 | `Components/Shared/ReadingTimerInline.razor` `CalculateXP` (937-943) | Magic Numbers 5/20/50 durch `XpCalculator.CalculateXpForSession(minutes, pages, 0)` ersetzen (Single Source of Truth). |
| Z.692 | `Infrastructure/Services/PlantService.cs` (189-237,311-356) | Toten XP-basierten Plant-Level-Pfad (`AddExperienceAsync`/`LevelUpAsync`/`CanLevelUpAsync`, `Experience`-Feld) entfernen oder als `[Obsolete]`/internal markieren — Doppel-Coin-Risiko ausschließen. |
| Z.768 | `Infrastructure/Services/ShareCardService.cs` (805,819,833,850) | Ungenutzte `DrawCoverPlaceholder`/`DrawInfoChip`/`DrawStarRating`/`DrawCategoryRatings` entfernen. |
| Z.699 | `Infrastructure/Services/ImportExportService.cs` `CreateBackupAsync` (367-383) | Explorative Selbstgespräch-Kommentare durch einen knappen Einzeiler ersetzen (warum System.IO statt IFileSystem). |
| Z.774 | `Core/ViewModels/BookListViewModel.cs` (40,85-87) | Command-Kollision auflösen: entweder nur generiertes `[RelayCommand]` ODER nur manuelles `AddAsyncCommand`. `@onclick` an Command binden (CanExecute-Guard wirksam). |
| Z.780 | `Core/ViewModels/BookshelfViewModel.cs` (40-41,544-562) | Toten `MovePlantToPositionAsync` + nie befüllte `BookshelfPlants`-Property entfernen (Reorder läuft über `ReorderShelfItemsAsync`). |
| Z.786 | `Core/ViewModels/StatsViewModel.cs:232` | Tote `CurrentLevel = settings.UserLevel`-Zuweisung entfernen (wird Z.237 überschrieben). |
| Z.804 | `Core/ViewModels/UserProgressViewModel.cs` (79-82) | Ungenutzte private `GetXpForLevel` entfernen. |
| Z.598 | `Core/Validators/ReadingGoalValidator.cs:28` vs `Core/Models/ReadingGoal.cs:22` | Obergrenze angleichen: eine autoritative Grenze (Validator `LessThanOrEqualTo` ↔ `[Range]`). `Validator_Goal_TargetMax`-Message anpassen. |
| Z.606 | `Core/ViewModels/StatsTrendsViewModel.cs` (112-124) + `StatsAnalysesViewModel.cs` (111-119) | Year-Change-Commands in `ExecuteSafelyAsync` kapseln (IsBusy/ClearError/DB-Gate/Crash-Report) wie `FilterTopBooksByCategoryAsync`. |
| Z.620 | `Core/ViewModels/PlantShopViewModel.cs` `SelectSpecies` (118-122) | `ClearError()` ergänzen — wie `DecorationShopViewModel.SelectDecoration`. |
| Z.627 | `Core/ViewModels/DecorationShopViewModel.cs` (66) | Level-/Coin-Fehlermeldungen an Plant-Shop angleichen (erforderliches + aktuelles Level; gleiche Keys). |
| Z.584 | `Components/Shared/DeleteConfirmationModal.razor` + 3 Celebration-Overlays | `role="dialog"` `aria-modal="true"` `aria-labelledby` ergänzen (wie `PaywallModal`/`ReviewPromptModal`). |

**Verify:** Build Core+Tests grün; keine Referenz-Reste (Grep nach entfernten Symbolen).

---

## Phase 11 — Entitlement-Leck (niedrig)

| # | Datei | Fix |
|---|---|---|
| Z.996 | `Infrastructure/Services/ShareCardService.cs` `GenerateStatsCardAsync`/`GenerateBookCardAsync` (36-78) | `IFeatureGuard` injizieren, `RequireAccess(FeatureKey.ShareCards)` vor Generierung — Service-seitige Durchsetzung statt nur UI-Gate. Test: Free-Tier → `RequireAccess`-Wurf. Pattern wie SEC-06/SEC-08 (`DecorationService`/`PlantService` Guards). |

---

## Gesamt-Verifikation

```bash
# Core + Tests bauen
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj -c Release
dotnet build BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release

# Alle Tests (inkl. AppResourcesCoverageTests für neue resx-Keys)
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release

# Gezielt nach Phase
dotnet test ... --filter "FullyQualifiedName~ShareCard"
dotnet test ... --filter "FullyQualifiedName~AppResourcesCoverage"
```

- **Infrastructure/MAUI** bauen lokal (CI baut nur Core+Tests). MAUI-Layer-Fixes (FileSaver, Scanner, FilePicker, MigrationService, Manifest, ImageService-DI) sind nicht unit-testbar → per Android-Build + `/run`-Smoke-Test prüfen.
- Visuelle Phasen (1,2,9) über `/run` auf Gerät/Emulator sichten.
- Nach Abschluss: `CHANGELOG.md` unter `## [Unveröffentlicht]` ergänzen (`### Behoben` / `### Geändert` / `### Sicherheit` für Phase 11), und in `CODE_REVIEW_TODO.md` die erledigten Punkte auf `[x]` mit Fix-Notiz setzen.

## Reihenfolge / Empfehlung

Phasen 0–3 sind **release-relevant** (user-sichtbar) → zuerst. Phasen 4–7 Robustheit. Phasen 8–11 Qualität/Perf/Cleanup → Post-Launch-tauglich, aber im selben Branch sinnvoll abzuarbeiten. Jede Phase als eigener Commit (`fix:`/`refactor:`/`chore:` je nach Inhalt), Tests grün vor jedem Commit. Kein `git push` ohne explizite Aufforderung.
