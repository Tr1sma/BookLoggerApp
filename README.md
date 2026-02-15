# 📚 Book Logger App

![CI](https://github.com/TristanAtze/BookLoggerApp/actions/workflows/ci.yml/badge.svg)

Eine moderne Android-App zum Verwalten und Protokollieren deiner Bücher mit starken Gamification-Elementen.  
Gebaut mit **.NET 10 MAUI Blazor Hybrid** und **SQLite**.

---

## Features

### Bibliotheks-Management

- **Umfassende Buch-Verwaltung**: Bücher hinzufügen, bearbeiten und löschen.
- **Detaillierter Lesestatus**: Geplant, Am Lesen, Abgeschlossen, Abgebrochen.
- **Spine-Ansicht**: Personalisiere den Buchrücken für das virtuelle Regal (Farbe oder Bild).
- **Drag & Drop**: Sortiere deine Bücher im Regal per Drag & Drop. (BETA)

### Erweitertes Bewertungssystem

Statt einer einfachen 5-Sterne-Wertung bietet die App ein **Multi-Kategorie-Rating** (1-5 Sterne):

- 🎭 Charaktere
- 📜 Plot
- ✍️ Schreibstil
- 🌶️ Spice Level
- ⏱️ Pacing
- 🌍 World Building

### Lesesessions & Tracking

- **Aktiver Lese-Timer**: Starte Sessions, pausiere und setze sie fort.
- **Echtzeit-Tracking**: Erfassung von Lesezeit, gelesenen Seiten und XP.
- **Session-Zusammenfassung**: Detaillierte Übersicht nach jeder Session.
- **Streaks**: Verfolge deine täglichen Lesegewohnheiten.

### Gamification & Belohnungen

- **Level-System**: Sammle XP durch Lesen und steige im Level auf.
- **Virtueller Garten**:
  - Schalte neue Pflanzen-Spezies frei.
  - Pflanze und züchte virtuelle Pflanzen, die mit deiner Lesezeit wachsen.
- **Shop**: Kaufe neue Pflanzenarten und Deko (in-game Währung).
- **Achievements**: Schalte Meilensteine und Erfolge frei.
- **Leseziele**: Setze dir Ziele (z.B. "30 Minuten täglich") und verfolge den Fortschritt.

### Statistiken & Analytics

- **Dashboards**: Visuelle Aufbereitung deiner Lesegewohnheiten.
- **Trends**: Verlauf der Leseaktivität über die Zeit.
- **Genre-Analyse**: Welches Genre liest du am meisten?
- **Rating-Insights**: Durchschnittsbewertungen pro Kategorie (z.B. "Wie bewerte ich Plot vs. Charaktere?").

### Daten & Sicherheit

- **Offline-First**: Alle Daten liegen lokal auf deinem Gerät (SQLite).
- **Import/Export**:
  - Export als JSON (Vollständiges Backup) oder CSV (Tabellenkalkulation).
  - Import von Daten aus JSON/CSV.
- **Backup**: Erstelle und wiederherstelle vollständige Datenbank-Backups.

### Technik & Design

- **Modernes UI**: Responsives Blazor Hybrid Interface.
- **Dark Mode**: Augenfreundliches, warmes "Cozy"-Theme.
- **Cross-Platform Architektur**: Vorbereitet für Android, potenziell iOS/Windows.

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

## Entwicklung

### Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022+ oder VS Code (C# Dev Kit)
- Android SDK (für Emulator/Device)

### Starten

```bash
# Abhängigkeiten wiederherstellen
dotnet restore

# App bauen und starten (Android)
dotnet build -f net10.0-android -t:Run

# Tests ausführen
dotnet test
```

### Datenbank Migrationen

```bash
# Neue Migration erstellen
dotnet ef migrations add <Name> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

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

