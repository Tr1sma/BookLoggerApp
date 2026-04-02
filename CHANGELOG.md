# Changelog

Alle nennenswerten Änderungen an BookHeart werden in dieser Datei festgehalten.

Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

Versionsschema:

- `V0.x.y` – Pre-Release (vor Play-Store-Veröffentlichung)
- `V1.0.0` – Erster öffentlicher Play-Store-Release
- MAJOR wird auf 1 gesetzt wenn der erste Play-Store-Upload erfolgt
- MINOR für neue Features, PATCH für Bugfixes und kleinere Änderungen

---

## [Unveröffentlicht]

### Hinzugefügt

- **Stats teilen (Reading Wrapped):** Auf der Stats-Seite können Lesestats als PNG im Instagram-Story-Format (1080×1920) geteilt werden. Zeitraum wählbar: Week, Month, Quarter, Year, All Time. Die Karte zeigt abgeschlossene Bücher, gelesene Seiten, Lesezeit, Lieblingsgenre und Top-3-Bücher im BookHeart-Design.
- **Buchempfehlung teilen:** Nach dem Abschließen eines bereits vorhandenen Buchs (d. h. nach echten Lesesitzungen) erscheint in der Abschluss-Feier ein optionaler Button „Share as Recommendation". Die Karte zeigt Titel, Autor, Cover, Seitenanzahl, Gesamtlesezeit und alle Bewertungskategorien. Bücher, die direkt als abgeschlossen hinzugefügt wurden (ohne Lesesitzung), werden ausgeschlossen.

### Geändert
- Der komplexere Review-Flow wurde durch zwei einfache Modals ersetzt: erst App-Feedback, dann optional der Sprung zur Play-Store-Bewertung
- Der vereinfachte Review-Dialog erscheint erst ab Level 7, hoechstens zweimal pro Monat und kann ueber "Nicht mehr fragen" dauerhaft abgeschaltet werden

### Behoben

## [V0.7.4] - 2026-04-01

### Geändert
- Der "Wie Leveln Pflanzen?" Absatz auf der Plant Shop seite wurde auf Englisch Übersetzt
- Backup-Dateiname von `booklogger_backup_*.zip` auf `bookheart_backup_*.zip` umbenannt

### Behoben
- Die untere NavBar von Android überdeckt jetzt nicht länger die In-App NavBar
- ISBN-Autofill nutzt jetzt automatisch einen anonymen Fallback ohne API-Key, wenn Google Books für den Projekt-Key eine Quota-/Rate-Limit-Fehlermeldung liefert

## [V0.7.3] - 2026-03-31

### Hinzugefügt

- Changelog Datei
- Google Play In-App Review Integration: Review-Dialog nach Level-Up, Buch-Abschluss oder Leseziel-Erreichen (max. 2x/Monat, erst ab Level 6)

### Geändert

- Buy-me-a-Coffee-Unterstützung direkt im Backup-&-Restore-Bereich der Einstellungen hinzugefügt
- Layout der Settings Seite wurde Optimiert
- App-Symbol auf appicon512.png umgestellt
- Google-Books-API-Key wird jetzt im CI-Release-Build via GitHub-Secret injiziert (behebt 429-Quota-Fehler bei ISBN-Suche in Play-Store-Versionen)

### Behoben

- Release-Build-Fehler behoben: `MonoAOTCompiler`-Race-Condition (`IndexOutOfRangeException` in `PrecompileLibraryParallel`) durch `AndroidAotParallelism=1` umgangen
- App-Absturz beim Start nach Play-Store-Installation behoben: IL-Linker-Trimming auf `partial` umgestellt (schützt EF Core, FluentValidation und Blazor vor Reflection-Stripping) und `AndroidEnableProfiledAot` ohne zugehörige `.aotprofile`-Datei entfernt
- ISBN-Autofill schlägt in Play-Store-Builds nicht mehr mit „429 Quota exceeded" fehl

## [V0.6.3] - 2026-03-30

### Hinzugefügt

- Bodenfläche unter jedem Buch im Bücherregal (Issue #159)
- Bodenfläche für Pflanzen

### Geändert

- Benachrichtigungen verbessert
- Android Keystore-Signierung und AAB-Build-Pipeline eingerichtet (Issue #134)
- Legacy-Migrations-Hilfscode entfernt

### Behoben

- Kamera- und Benachrichtigungs-Berechtigungsfehler behoben (PR #158)
- Zielfortschritts-Bug behoben
- Fehlerbehandlung in mehreren Services gehärtet

## [V0.6.2] - 2026-02-18

### Geändert

- Abhängigkeiten aktualisiert (Dependabot)

## [V0.6.1] - 2026-02-18

### Geändert

- Interne Branch-Merges aus V6-Entwicklungszweig

## [V0.6.0] - 2026-02-18

### Hinzugefügt

- Drag-and-Drop für Bücherregale (mit Long-Press-Geste und Auto-Scroll)
- Push-Benachrichtigungen implementiert
- Automatische Bildskalierung für große Buchcover
- Bücher können aus Zielen ausgeschlossen werden
- Ziele können auf bestimmte Genres oder Tropes beschränkt werden
- Timer-Hintergrundstatus konsistent über alle Timer-Komponenten

### Geändert

- App-Name zu **BookHeart** geändert
- Regal löschen verschiebt Bücher automatisch ins Hauptregal
- Scroll- und Drag-and-Drop-Performance verbessert

### Behoben

- ISBN und Language MaxLength-Fehler im Datenbankschema
- Division durch Null in StatsService

## [V0.5.4] - 2026-02-13

### Hinzugefügt

- Mehrkategorien-Bewertungssystem mit Datenbankmigrierung
- StatsViewModel mit umfassenden Lesestatistiken
- Nutzerfortschritts-Tracking und Einstellungen mit Import/Export

### Geändert

- Buchsuche verbessert (Pflanzenbelohnungen, Regal-Sortierung)
- Backup-Wiederherstellung zuverlässiger (SQLite WAL-Dateien, virtuelle Pfade für Google Drive)

## [V0.5.1] - 2026-01-14

### Hinzugefügt

- Dependabot-Konfiguration für automatische NuGet-Sicherheitsupdates
- Automatische Sortierung für spezielle Regale

### Sicherheit

- **[HOCH]** Zip-Bomb-Schwachstelle in ImportExportService behoben

## [V0.5.0] - 2026-01-13

### Hinzugefügt

- Tropes/Subgenre-Tagging für Bücher
- Lazy Loading für Buchcover (IntersectionObserver)
- Cloud-Backup-Funktion
- Debounced-Suche mit Genre- und Trope-Unterstützung

### Geändert

- Gamification rebalanciert (neue Pflanzen, angepasste XP-Kurve)
- StatsService-Datenbankabfragen optimiert (DB-seitige Aggregation)

### Sicherheit

- **[HOCH]** Zip-Slip-Schwachstelle in ImportExportService behoben
- URL-Parameter-Injection in LookupService behoben
- Sicherheitsaudit für Image-Download

## [V0.4.0] - 2026-01-06

### Hinzugefügt

- Natives MAUI Barcode-Scanning (ZXing.Net.Maui.Controls)
- Benutzerdefinierter Farbwähler für Buchrücken (erweitertes Farbspektrum)
- Android Zurück-Button-Support
- AOT-Kompilierung im Release-Modus (bessere Performance)
- Plattformspezifische async Einstellungen und Datei-Saver

## [V0.3.0] - 2025-12-04

### Hinzugefügt

- Google Play Store Vorbereitungen
- Ziel-Events und Datenlöschungsfunktion

### Geändert

- Pflanzen-Leveling auf Lesetage umgestellt
- Mobile UX und Berechtigungsverwaltung verbessert

### Behoben

- Race Condition bei Münz-Updates

## [V0.2.0] - 2025-11-07

### Geändert

- Migration auf .NET 10

## [V0.1.0] - 2025-11-04

### Hinzugefügt

- Initiale Veröffentlichung
- Bücher verwalten (hinzufügen, bearbeiten, löschen, Buchcover)
- Leseziel-Tracking
- Gamification: XP, Level, Pflanzen, Shop, Coins, Streaks
- SQLite-Datenbank mit EF Core
- Android-App (MAUI Blazor Hybrid)
