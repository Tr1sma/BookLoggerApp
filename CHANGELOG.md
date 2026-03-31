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

- Changelog Datei

### Geändert

### Behoben

- App-Absturz beim Start nach Play-Store-Installation behoben: IL-Linker-Trimming auf `partial` umgestellt (schützt EF Core, FluentValidation und Blazor vor Reflection-Stripping) und `AndroidEnableProfiledAot` ohne zugehörige `.aotprofile`-Datei entfernt

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
