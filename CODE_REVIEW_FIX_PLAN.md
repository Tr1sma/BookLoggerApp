# 10-Phasen-Plan — Abarbeitung der hoch+mittel Befunde aus CODE_REVIEW_TODO.md

> Begleit-Plan zu `CODE_REVIEW_TODO.md`. Jeder Befund wird **erst dann** in der TODO-Datei auf `- [x]` gesetzt, wenn er tatsächlich behoben **und** sein Test grün ist.

## Context

Die `CODE_REVIEW_TODO.md` (Stand 2026-06-15, Branch V12) listet **136 offene Befunde** aus einem vollständigen Code-Review. Ziel: nach Umsetzung aller 10 Phasen sind alle in-scope-Befunde behoben.

**Scope (vom User bestätigt):** Nur die `hoch` + `mittel` Befunde — 17 hoch + 47 mittel = **64 Befunde**. Die 72 `niedrig`-Items sind **nicht** Teil dieses Plans.

**Re-Verifikations-Gate (vor dem Plan durchgeführt):** Die 24 mit `⚠ zu verifizieren` markierten in-scope-Befunde wurden adversarisch gegen den aktuellen Quellcode erneut geprüft. Ergebnis: **21 bestätigt · 2 partiell-aber-real · 0 widerlegt · 1 verworfen.**
- **LOG-05 (Widget Integer-Division) → VERWORFEN:** Widget und App nutzen identische Integer-Division (`Book.cs:96`, `ReadingGoal.cs:42`); keine Inkonsistenz. → **aus dem Plan entfernt. Aktionierbarer Scope: 63 Befunde.**
- **SEC-09 / INK-05 → partiell, aber real, bleiben drin.**
- **BUG-13 / SEC-18** bestätigt, aber geringere Praxis-Wirkung — bleiben als Härtung, niedrig priorisiert.

## Gegrundete Schlüssel-Fakten (vor Umsetzung beachten)

1. **`IFeatureGuard` existiert bereits** (`Core/Services/Abstractions/IFeatureGuard.cs`): `RequireAccess(FeatureKey, string?)` wirft `EntitlementRequiredException`; `HasAccess(...)`; `EnforceSoftLimit(...)`. Impl `FeatureGuard` (Singleton). Bereits **optional injiziert** (`IFeatureGuard? guard = null`) in `ShelfService`, `QuoteService`, `GoalService`, `AnnotationService`. → Phase 2 **wiederverwenden**.
2. **`GoalService.AddAsync` guardet bereits** Genre/Excluded bei Erstellung. Lücke = inkrementelle Mutatoren (`AddGenreToGoalAsync`, `ExcludeBookFromGoalAsync`, `RemoveGenreFromGoalAsync`, `IncludeBookInGoalAsync`, `UpdateAsync`).
3. **RowVersion-Fix:** SQLite bumpt `[Timestamp]`-BLOBs nie. `ISaveChangesInterceptor` (setzt `RowVersion = Guid.NewGuid().ToByteArray()`) an **beiden** DI-Pfaden registrieren (`AddDbContextFactory` **und** transientes `AddDbContext`). `UserEntitlements` hat **keine** RowVersion-Spalte; Config fehlt `.IsConcurrencyToken()`.
4. **BUG-16 ist ein Refactor, kein Transaktions-Wrap:** neue orchestrierende `IBookService.SaveBookWithRelationsAsync(...)` mit einem UoW + `BeginTransaction/Commit`.
5. **BUG-06 Fix-Ort ist `AdvancedStatsService`** (per-Query-Context via `IDbContextFactory`), nicht die VMs.
6. **Test-Infra:** Tests-Projekt = `net10.0`, referenziert nur Core+Infrastructure, **kann das MAUI-Projekt nicht referenzieren**. `Platforms/Android/*` + Widget + Billing = **nicht unit-testbar**; reine Logik nach Core extrahieren.
7. **EF-InMemory reproduziert Concurrency/PK/Transaktions-Bugs NICHT.** → **SQLite-In-Memory-Fixture** in Phase 1; Phasen 1, 4, 6 brauchen sie.

## Standing Rules (jede Phase)

- **TDD:** zuerst fehlschlagender Test, dann Fix, dann grün. Wo nur SQLite den Bug zeigt: SQLite-Fixture.
- **Build/Test:** `dotnet build BookLoggerApp.sln` + `dotnet test` grün. `AppResourcesCoverageTests` nie brechen.
- **CHANGELOG.md** unter `## [Unveröffentlicht]` ergänzen (deutsch).
- **Lokalisierung:** neue UI-Strings → beide `.resx` (EN+DE).
- **Obsidian-Vault** bei Klassen-Änderungen pflegen + pushen.
- **Kein `git push`.** Lokaler Commit pro Phase ok.
- **CODE_REVIEW_TODO.md:** behobene Befunde auf `- [x]` setzen (erst nach grünem Test).

---

## Phase 1 — Nebenläufigkeit, RowVersion & Transaktions-Fundament  *(10)*
BUG-01/BUG-10/SEC-09/INK-07 (RowVersion-Interceptor, eine Wurzel-Korrektur), BUG-08/INK-03/SEC-12 (AppSettings-Schreib-Serialisierung + Mirror-Sync verengen), BUG-06 (AdvancedStatsService per-Query-Context), BUG-16 (BookService-Orchestrierungs-Refactor mit Transaktion), INK-13 (BookDetailViewModel DB-Init-Gate).
**Dateien:** `AppDbContext.cs` (+Interceptor), `MauiProgram.cs` (beide Registrierungen), `AppSettingsConfiguration.cs`, `UserEntitlementConfiguration.cs`, `AppSettingsProvider.cs`, `EntitlementService.cs`, `AdvancedStatsService.cs`, `BookService.cs`, `BookEditViewModel.cs`, `BookDetailViewModel.cs`.
**Verifikation:** SQLite-Fixture; Interceptor-bump + stale-Write-Test; AppSettings-Race; BUG-16-Rollback.

## Phase 2 — Entitlement-Durchsetzung im Service-Layer  *(10)*
SEC-08 (PlantService.Purchase), SEC-06 (DecorationService.Purchase), SEC-03/07/10 (GoalService-Mutatoren), SEC-16/INK-08 (WishlistService), SEC-17 (GenreService.AddTrope), SEC-15 (SettingsViewModel Shelf-Farben), SEC-11 (Plant/Decoration Read-Filter `IsHiddenByEntitlement`).
**Dateien:** `PlantService.cs`, `DecorationService.cs`, `GoalService.cs`, `WishlistService.cs`, `GenreService.cs`, `SettingsViewModel.cs`, `MauiProgram.cs`.
**Verifikation:** je Methode `IEntitlementService`=false → `EntitlementRequiredException`. InMemory genügt.

## Phase 3 — Validierung aktivieren + CancellationToken durchreichen  *(5)*
BUG-05 (ValidateAndThrowAsync in BookService/GoalService/ProgressService/PlantService), BUG-15/CQ-02 (ct auf spezifische Repo-Interfaces+Impls), INK-05 (Services reichen ct durch), CQ-01 (ViewModelBase per-Load CTS).
**Dateien:** `ValidationService.cs` + 4 Validatoren, `BookService.cs`, `GoalService.cs`, `ProgressService.cs`, `PlantService.cs`, `StatsService.cs`, spezifische Repos + Interfaces, `ViewModelBase.cs` + Content-VMs.

## Phase 4 — Entitlement-Lifecycle, Lapse & Play-Billing  *(8)*
BUG-02 (Resume-Lapse Promo ausschließen), BUG-09 (Expiry → ApplyLapseAsync), SEC-04 (tier-bewusstes Re-Hide), LOG-01 (Abo-Ablauf nicht raten), LOG-07 (Intro-Badge per-SKU), BUG-04 (PurchaseToken statt TransactionIdentifier), BUG-12 (oldPurchaseToken durchreichen), BUG-14 (Error statt false-Success).
**Dateien:** `EntitlementService.cs`, `EntitlementLapseHandler.cs`, `AppStartupViewModel.cs`, `Services/Billing/AndroidBillingService.cs`, `PaywallViewModel.cs`, `PaywallModal.razor`.
**Verifikation:** Service-Logik unit-testbar; **Android-Billing/PaywallModal manuell/On-Device.**

## Phase 5 — Privacy, Consent & Plattform-Permissions  *(5)*
SEC-02 (Consent fail-closed), SEC-01 (Manifest-Flags false + MainActivity `?? true` weg), BUG-07 (SetUserProperty/SetUserId gaten), SEC-18 (WebView-Permission prüfen), BUG-13 (AppRestart non-blocking).
**Dateien:** `AnalyticsConsentGate.cs`, `AndroidManifest.xml`, `MainActivity.cs`, `FirebaseAnalyticsService.cs`, `CustomWebChromeClient.cs`, `AppRestartService.cs`.
**Verifikation:** `AnalyticsConsentGate` (Core) unit-testbar; Rest **manuell/On-Device.**

## Phase 6 — Import/Export, Backup, Migration & Daten-Security  *(7)*
BUG-03 (Import neue Ids), BUG-11 (Restore .bak-Rollback), SEC-05 (Zip-Bomb reale Bytes), SEC-13 (SSRF URL-Validierung), SEC-14 (CSV-Injection), INK-10 (AsNoTracking konsistent), LOG-03 (MigrationRecovery enger matchen).
**Dateien:** `ImportExportService.cs`, `ImageService.cs`, `MigrationRecovery.cs`, `Repository.cs` + spezifische Repos.
**Verifikation:** BUG-03/11 **SQLite-Fixture**; SEC-05/13/14/LOG-03 reine Logik-Tests.

## Phase 7 — Zeitzonen (UTC↔Lokal) & Stats-Korrektheit  *(7)*
LOG-02 (Streak lokaler Tag), LOG-04/LOG-08 (AdvancedStatsService ToLocalTime vor Bucketing), LOG-06 (BookDetail ToLocalTime), INK-01 (Goal-Timestamp kanonisch StartedAt), INK-06 (Widget lokaler Tag + Core-Helper), INK-12 (0-Minuten-Sessions ausschließen).
**Dateien:** `StatsService.cs`, `AdvancedStatsService.cs`, `GoalService.cs`, `BookDetail.razor`, `WidgetDataService.cs`.
**Verifikation:** Stats/Goal (Infra) unit-testbar; Widget-Helper testen; Render manuell.

## Phase 8 — Gamification-Konsistenz  *(3)*
INK-02 (ShelfService Einfüge-Position vereinheitlichen), INK-04 (XP-Vorschau Truncation), INK-11 (QuickReadingTimer Dead-Code).
**Dateien:** `ShelfService.cs`, `ReadingTimerInline.razor`, `QuickReadingTimer.razor`.

## Phase 9 — ViewModel-State & UI-Robustheit  *(4)*
BUG-17 (Event-Handler eigene Fehlerbehandlung), BUG-18 (PurchaseTierAsync Reentrancy-Guard), LOG-09 (SearchAsync Filter bei leerem Query), UX-02 (RatingInput a11y).
**Dateien:** `AppStartupViewModel.cs`, `PaywallViewModel.cs`, `BookshelfViewModel.cs`, `RatingInput.razor`.

## Phase 10 — Widget- & UI-Lokalisierung  *(4)*  *(LOG-05 entfällt)*
UX-01 (Widget-Strings → strings.xml/de), INK-09 (Wishlist-Lookup Fehler-Flag aus VM), UX-03 (PaywallModal Preise → resx), UX-04 (PaywallViewModel Strings → Tr()).
**Dateien:** `Widgets/*Provider.cs` + Android-`strings.xml`(de), `Bookshelf.razor`, `WishlistViewModel.cs`, `PaywallModal.razor`, `PaywallViewModel.cs`, beide `AppResources.resx`.

---

## Shared-File-Sequencing
- `EntitlementService.cs`: P1 → P4. `BookService.cs`: P1 → P3. `GoalService.cs`: P2 → P3 → P7. `PlantService.cs`: P2 → P3. `AppStartupViewModel.cs`: P4 + P9. `PaywallViewModel.cs`: P4 + P9 + P10. `PaywallModal.razor`: P4 + P10. `AdvancedStatsService.cs`: P1 + P7. Spezifische Repos: P3 + P6.

## Globale Verifikation
- **Phase 1:** wiederverwendbare SQLite-In-Memory-Fixture anlegen.
- **Pro Phase:** Tests grün, voller `dotnet test`, `dotnet build`, CHANGELOG, lokaler Commit (kein Push), Vault gepflegt, behobene Befunde in `CODE_REVIEW_TODO.md` → `- [x]`.
- **Manuell/On-Device (nicht CI):** P4 (Billing/PaywallModal), P5 (Manifest/MainActivity/WebView/Restart), Widget-Anteile in P7 & P10.
- **Abschluss:** LOG-05 mit Notiz „re-verifiziert: kein Defekt (Widget==App), verworfen" markieren.

## Scope-Zusammenfassung
- **64 hoch+mittel** im Scope; **63 aktioniert**, **1 verworfen (LOG-05)**.
- 23 der 24 `⚠`-Befunde re-verifiziert bestätigt; 0 widerlegt.
- 72 `niedrig`-Befunde sind nicht Teil dieses Plans.
