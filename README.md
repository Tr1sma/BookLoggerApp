# 📚 Book Logger App

![CI](https://github.com/TristanAtze/BookLoggerApp/actions/workflows/ci.yml/badge.svg)

Eine Android-App zum Verwalten und Protokollieren deiner Bücher mit Gamification-Elementen.
Gebaut mit **.NET 10 MAUI Blazor Hybrid** und **SQLite** als Datenbank.

---

## 🚀 Features

### Kernfunktionen
- Bücher hinzufügen, bearbeiten, löschen mit Multi-Kategorie-Bewertungssystem
- Lesefortschritt mit Timer-basiertem Session-Tracking
- Zitate und Annotationen zu Büchern
- Regale / Bookshelf-Ansicht mit Spine-View
- Umfangreiche Statistiken und Analytics zum Leseverhalten

### Gamification
- Level- und XP-Progressionssystem für Leser
- Virtuelle Pflanzen, die mit Lesen wachsen
- Pflanzen-Shop mit verschiedenen Spezies
- Leseziele mit Fortschrittsverfolgung
- Achievements und Meilensteine

### Technische Features
- Externe Buchsuche über Google Books API
- Import/Export von Buchdaten
- Offline-first mit lokaler SQLite-Datenbank
- Responsive Design für verschiedene Bildschirmgrößen
- Dark Theme mit gemütlicher brauner Farbpalette

---

## 🔧 Tech Stack

### Frontend
- [.NET 10 MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui)
- Blazor Components & Razor Pages
- CSS mit Mobile-First Design

### Backend & Architektur
- **Layered Architecture** (Domain, Infrastructure, Presentation)
- **Repository Pattern** mit generischen und spezifischen Repositories
- **Unit of Work Pattern** für Transaktionskonsistenz
- **Dependency Injection** über MAUI DI Container
- **FluentValidation** für Modelvalidierung

### Datenspeicherung
- SQLite für lokale Datenspeicherung
- Entity Framework Core 10 mit Code-First Migrations
- DbContext Factory für Blazor-Kompatibilität

### Testing & CI/CD
- xUnit als Test-Framework
- FluentAssertions für aussagekräftige Assertions
- EF Core InMemory Provider für Unit Tests
- GitHub Actions für automatisierte Tests

---

## 📂 Projektstruktur

```
BookLoggerApp/                    → MAUI Blazor Hauptprojekt (Presentation Layer)
  ├── Components/                 → Blazor Pages und Komponenten
  ├── wwwroot/css/                → Styling und CSS
  └── Platforms/                  → Plattform-spezifischer Code

BookLoggerApp.Core/               → Domain Layer
  ├── Models/                     → Domain-Entities und Result-Objekte
  ├── Services/Abstractions/      → Service-Interfaces
  ├── ViewModels/                 → MVVM ViewModels
  ├── Validators/                 → FluentValidation Validators
  └── Exceptions/                 → Custom Exception Hierarchy

BookLoggerApp.Infrastructure/     → Infrastructure Layer
  ├── Data/                       → EF Core DbContext und Konfigurationen
  ├── Repositories/               → Repository-Implementierungen
  └── Services/                   → Service-Implementierungen

BookLoggerApp.Tests/              → Unit Tests
  ├── Repositories/               → Repository Tests
  ├── Services/                   → Service Tests
  └── TestHelpers/                → Test-Hilfsfunktionen
```

---

## 🛠️ Entwicklung

### Voraussetzungen
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 oder Visual Studio Code mit C# Extension
- Android SDK für Android-Deployment

### Build & Test

```bash
# Gesamte Solution bauen
dotnet build BookLoggerApp.sln

# Alle Tests ausführen
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

# Spezifischen Test ausführen
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~YourTestName"
```

### Entity Framework Migrations

```bash
# Neue Migration hinzufügen (vom Solution-Root ausführen)
dotnet ef migrations add MigrationName --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Datenbank aktualisieren
dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Migrations auflisten
dotnet ef migrations list --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
```

### Architektur-Details

Für detaillierte Informationen zur Architektur, den verwendeten Patterns und Entwicklungsrichtlinien siehe [`CLAUDE.md`](CLAUDE.md).

---

## 📜 Lizenz
Dieses Projekt ist **nicht frei für Änderungen, Forks oder Weiterverkauf**.  
Die Details findest du in der Datei [`LICENSE.md`](LICENSE.md).

---

## 👨‍💻 Autor
Entwickelt von **Ben Sowieja**  