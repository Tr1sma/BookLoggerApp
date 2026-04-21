# 📚 BookHeart
Ein Mapping aller Klassen und abhängigkeiten in form einer Obsidian Graph-View kann [hier](https://github.com/Tr1sma/codebase-map) gefunden werden

![CI](https://github.com/TristanAtze/BookLoggerApp/actions/workflows/ci.yml/badge.svg)

`BookHeart` ist eine moderne Android-App zum Verwalten, Tracken und Teilen deiner Bücher mit starken Gamification-Elementen.  
Gebaut mit **.NET 10 MAUI Blazor Hybrid** und **SQLite**.

---

## Features

### Bibliothek, Regale & Organisation

- **Umfassende Buch-Verwaltung**: Bücher hinzufügen, bearbeiten, löschen und Lesestatus pflegen (Geplant, Am Lesen, Abgeschlossen, Abgebrochen).
- **Virtuelles Bücherregal**: Spine-Ansicht mit anpassbarer Rückenfarbe sowie Bodenfläche unter Büchern und Pflanzen.
- **Drag & Drop mit Long-Press**: Bücher per Long-Press-Geste inkl. Auto-Scroll zwischen Regalen neu anordnen.
- **Regal-Management & Auto-Sort**: Eigene Regale erstellen, Status-Regale automatisch sortieren; beim Löschen eines Regals werden Bücher ins Hauptregal verschoben.
- **Genres, Tropes & Suche**: Debounced-Suche sowie Genre-, Subgenre- und Trope-Unterstützung für schnelleres Finden.
- **Regal-Design anpassbar**: Separate Farbwahl für Bücherleisten und Regalleisten über mehrere Holzfarb-Presets.

### Buchdetails, Cover & Bewertung

- **Mehrkategorien-Bewertung (1-5 Sterne)**: Charaktere, Plot, Schreibstil, Spice Level, Pacing, World Building.
- **Rating-Insights**: Durchschnittswerte je Kategorie für besseres Verständnis der eigenen Bewertungsmuster.
- **Cover-Workflow**: Buchcover aus Galerie wählen (Android), automatische Bildskalierung bei großen Covern und performantes Lazy-Loading in Übersichten.
- **ISBN-Scanner & Autofill**: Natives Barcode-Scanning (ZXing) und Google-Books-Lookup mit robustem Fallback bei Quota-Limits.
- **Buchempfehlung teilen**: Für abgeschlossene Bücher Share-Karte als PNG mit Cover, Bewertungen, Lesedauer und Empfehlung-Badge direkt aus Abschluss-Flow oder Detailansicht.

### Lesen, Ziele & Fortschritt

- **Aktiver Lesetimer**: Sessions starten, pausieren, fortsetzen; konsistenter Timer-Status über alle Timer-Komponenten.
- **Session-Tracking in Echtzeit**: Lesezeit, gelesene Seiten und XP inklusive Session-Zusammenfassung.
- **Lese-Streaks**: Tägliche Lesekette mit klarer Heute-Status-Anzeige.
- **Flexible Leseziele**: Ziele setzen und verfolgen, Bücher von Zielen ausschließen sowie Ziele auf Genres/Tropes begrenzen.
- **Push-Benachrichtigungen & Ziel-Events**: Erinnerungen sowie Hinweise zu Zielabschlüssen und Pflanzenereignissen direkt aufs Gerät.
- **Review-Flow**: Vereinfachter In-App-Review-Ablauf mit fairer Frequenzbegrenzung.

### Gamification, Pflanzen & Shop

- **XP-, Level- und Coin-System**: Fortschritt durch Lesen mit Level-Ups und Belohnungslogik.
- **Pflanzen-Gamification**: Pflanzen freischalten, kaufen, platzieren, leveln und über Lesetage pflegen.
- **Pflanzendetails im Regal**: Detail-Modal mit Pflanzenname, Level, nächstem Gießzeitpunkt und direkter Gießaktion.
- **Plant Shop**: Verbessertes Shop-Erlebnis mit klarerer Preisdarstellung und überarbeiteter Kauflogik.
- **Belohnungs-Events**: Feierliche Flows bei Buchabschluss und Fortschrittsereignissen.

### Statistiken, Sharing & Widgets

- **Umfangreiche Lesestatistiken**: Dashboards, Trends, Genre-Auswertung sowie Fortschritts- und Progressionskennzahlen.
- **Reading Wrapped teilen**: Stats als Instagram-Story-optimierte PNG-Karte (1080x1920) für verschiedene Zeiträume.
- **Android Home Screen Widgets**:
  - Aktuelles Buch (Cover, Titel, Fortschritt)
  - Lese-Streak (Tage + Heute-Status)
  - Leseziel (Fortschritt zum aktiven Ziel)
- **Live-Aktualisierung**: Widgets werden periodisch und nach relevanten App-Aktionen automatisch aktualisiert.

### Daten, Backup & Sicherheit

- **Offline-First**: Lokale Datenspeicherung via SQLite auf dem Gerät.
- **Import/Export**: JSON- und CSV-Unterstützung für Datenaustausch und Sicherung.
- **Cloud-Backup & Restore**: Vollständige Backups lokal oder in der Cloud erstellen und wiederherstellen.
- **Daten zurücksetzen**: Komplette App-Daten können bei Bedarf gezielt gelöscht werden.
- **Sicherheits-Härtung**: Schutzmaßnahmen gegen Zip-Slip/Zip-Bomb und abgesicherte Bild-/URL-Verarbeitung.
- **Stabile Datenbasis**: Laufende Verbesserungen bei Migrationen, Fehlerbehandlung und Zuverlässigkeit.

### Technik & UX

- **.NET 10 MAUI Blazor Hybrid**: Moderne, performante Android-App-Architektur mit nativen Android-Integrationen.
- **Cozy Dark Theme**: Warmes, augenfreundliches Design im BookHeart-Stil.
- **Mobile UX-Optimierungen**: Android-Zurück-Button, verbesserte Berechtigungsabläufe und sauberes Navigationserlebnis.
- **Clean Architecture**: Getrennte Core-, Infrastructure- und Presentation-Layer mit MVVM, DI, Repository und Unit of Work.

---

## Tech Stack
- [.NET 10 MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui)
- SQLite für lokale Datenspeicherung
- MVVM + Dependency Injection
- GitHub Actions für CI/CD

### Frontend

- [.NET 10 MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui)
- Razor Components & Pages
- Modern CSS (Mobile-First, Custom Properties)

### Backend & Architektur

- **Clean Architecture** (Core, Infrastructure, Presentation)
- **MVVM Pattern** für klare Trennung von Logik und UI.
- **Repository & Unit of Work Pattern**.
- **Dependency Injection** (MAUI Built-in).
- **FluentValidation** für robuste Datenvalidierung.

### Data Access

- **SQLite** via Entity Framework Core 10.
- **Code-First Migrations**.
- `DbContextFactory` für sichere Blazor-Concurrency.

---

## Projektstruktur

```bash
BookLoggerApp/                    # Presentation Layer (MAUI + Blazor)
  ├── Components/Pages/           # UI Screens (Razor)
  ├── Platforms/                  # Native Implementierungen (Android etc.)
  └── wwwroot/                    # Statische Assets (CSS, Bilder)

BookLoggerApp.Core/               # Domain Layer (Reine C# Logik)
  ├── Models/                     # Entities (Book, ReadingSession, etc.)
  ├── Services/Abstractions/      # Interfaces (IBookService, etc.)
  └── ViewModels/                 # MVVM State Management

BookLoggerApp.Infrastructure/     # Infrastructure Layer
  ├── Data/                       # EF Core Context & Migrations
  └── Services/                   # Konkrete Implementierungen

BookLoggerApp.Tests/              # Unit Tests (xUnit + FluentAssertions)
```

---

## Projektumfang & Code-Statistiken

> BookHeart wird seit September 2025 von mir alleine entwickelt — Architektur, Features und der Großteil der Implementierung in Eigenarbeit. AI-Assistenten kommen punktuell für Bug-Fixes, Sicherheits-Reviews und Codebase-Scans zum Einsatz.

### Code-Verteilung

Gemessen mit [`scc`](https://github.com/boyter/scc) (ohne `obj/`, `bin/`, `Migrations/` und auto-generierten `.g.cs`):

| Sprache       | Files | Code-Zeilen | Kommentare | Komplexität |
|---------------|------:|------------:|-----------:|------------:|
| **C#**        |   299 |  **30.936** |      4.173 |       1.765 |
| **Razor**     |    42 |   **9.364** |        153 |         787 |
| **CSS**       |    29 |      11.211 |        437 |           – |
| Markdown      |    12 |       3.320 |          – |           – |
| SVG           |    30 |       1.234 |        263 |           – |
| HTML / JS     |    10 |       1.575 |        125 |         116 |
| Build / Misc  |    50 |       2.071 |        118 |          45 |
| **Gesamt**    |   472 |  **59.711** |      5.269 |       2.713 |

**App-Code (C# + Razor): ~40.300 Zeilen.**

### Qualitäts-Metriken (Visual Studio Code Metrics)

| Projekt                          | Wartbarkeitsindex | Zykl. Komplexität | Klassenkopplung |
|----------------------------------|------------------:|------------------:|----------------:|
| BookLoggerApp                    |                69 |             2.166 |             461 |
| BookLoggerApp.Core               |            **86** |             3.292 |             207 |
| BookLoggerApp.Infrastructure     |                55 |             1.483 |             322 |
| BookLoggerApp.Tests              |                70 |             1.298 |             360 |

Wartbarkeitsindex ≥ 70 gilt als gut, ≥ 80 als sehr gut. Der Core-Layer (reine Domänenlogik) erreicht **86**.

### Industriestandard-Aufwandsschätzung (COCOMO)

Auf Basis der Code-Zeilen berechnet `scc` automatisch das [COCOMO-Modell](https://en.wikipedia.org/wiki/COCOMO) (Boehm, 1981) — eine in der Industrie etablierte Schätzung dafür, was ein Unternehmensprojekt vergleichbaren Umfangs in einem klassischen Wasserfall-Setting kosten würde:

| Kennzahl                              | Wert                |
|---------------------------------------|---------------------|
| Geschätzte Entwicklungskosten         | **$1.979.030**      |
| Geschätzte Projektdauer               | **17,83 Monate**    |
| Geschätzte Teamgröße                  | **~10 Entwickler**  |
| Entspricht ca. Personenmonaten        | **176**             |

> Die COCOMO-Werte sind **keine realen Entwicklungskosten** dieses Projekts, sondern eine standardisierte Branchenschätzung. Sie zeigen, welcher Aufwand für eine vergleichbare Codebasis in einem klassischen Industrie-Setting (mehrköpfiges Team, Overhead, Reviews, QA, Meetings) anfallen würde.

---

## Entwicklung

### Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022+ oder VS Code (C# Dev Kit)
- Android SDK (für Emulator/Device)

### Starten

```bash
# Abhängigkeiten wiederherstellen
dotnet restore

# Lösung bauen
dotnet build BookLoggerApp.sln

# App bauen und starten (Android)
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -t:Run

# Tests ausführen
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
```

### Datenbank Migrationen

```bash
# Neue Migration erstellen
dotnet ef migrations add <n> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Datenbank updaten
dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
```

---

## Lizenz

Dieses Projekt ist **nicht frei für Änderungen, Forks oder Weiterverkauf**.  
Details siehe [`LICENSE.md`](LICENSE.md).

---

## Autor
Entwickelt von **Ben Sowieja**
