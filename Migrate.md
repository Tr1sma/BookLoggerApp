# Migration zu .NET 10 - Detaillierter Plan

**Projekt:** BookLoggerApp
**Aktueller Stand:** .NET 9
**Ziel:** .NET 10
**Datum:** 2025-11-27
**Erstellt für:** Komplette Migration aller Projekte, Bibliotheken und Tests

---

## Executive Summary

Dieser Plan beschreibt die vollständige Migration des BookLoggerApp-Projekts von .NET 9 auf .NET 10. Die Migration umfasst:
- 4 Projektdateien (.csproj)
- 15+ NuGet-Pakete
- CI/CD Pipeline (GitHub Actions)
- MAUI Blazor Hybrid Anwendung mit Multi-Targeting (Android, iOS, macOS Catalyst, Windows)

**Geschätzte Dauer:** 4-6 Stunden
**Risiko-Level:** Mittel
**Breaking Changes erwartet:** Möglich bei EF Core und MAUI

---

## Inhaltsverzeichnis

1. [Vorbereitung](#1-vorbereitung)
2. [Projekt-Analyse](#2-projekt-analyse)
3. [Migrations-Strategie](#3-migrations-strategie)
4. [Schritt-für-Schritt Durchführung](#4-schritt-für-schritt-durchführung)
5. [NuGet-Pakete Update](#5-nuget-pakete-update)
6. [CI/CD Pipeline Update](#6-cicd-pipeline-update)
7. [Testing & Validierung](#7-testing--validierung)
8. [Rollback-Plan](#8-rollback-plan)
9. [Bekannte Breaking Changes](#9-bekannte-breaking-changes)
10. [Post-Migration Optimierungen](#10-post-migration-optimierungen)

---

## 1. Vorbereitung

### 1.1 Prerequisites

**Zu installierende Software:**
```bash
# .NET 10 SDK Installation
# Download: https://dotnet.microsoft.com/download/dotnet/10.0
winget install Microsoft.DotNet.SDK.10

# Verify Installation
dotnet --list-sdks
# Erwartete Output: 10.0.xxx [C:\Program Files\dotnet\sdks]
```

**Workloads für MAUI:**
```bash
# MAUI Workload für .NET 10 installieren
dotnet workload install maui

# Android Workload
dotnet workload install android

# iOS Workload (falls macOS/iOS-Entwicklung)
dotnet workload install ios

# macOS Catalyst Workload
dotnet workload install maccatalyst

# Verifizierung
dotnet workload list
```

### 1.2 Backup & Version Control

**Git Branch erstellen:**
```bash
# Neuen Feature Branch von main erstellen
git checkout main
git pull origin main
git checkout -b feature/migrate-to-net10

# Backup-Tag erstellen (vor der Migration)
git tag -a backup-before-net10-migration -m "Backup before .NET 10 migration"
git push origin backup-before-net10-migration
```

**Lokales Backup:**
```bash
# Komplettes Projektverzeichnis sichern
# Manuell kopieren nach: C:\Backup\BookLoggerApp_NET9_Backup_2025-11-27
```

### 1.3 Dependency Check

**Aktuelle Abhängigkeiten dokumentieren:**
```bash
# NuGet Packages auflisten
dotnet list BookLoggerApp.Core/BookLoggerApp.Core.csproj package
dotnet list BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj package
dotnet list BookLoggerApp.Tests/BookLoggerApp.Tests.csproj package
dotnet list BookLoggerApp/BookLoggerApp.csproj package

# Output in Datei speichern für Vergleich
dotnet list package --include-transitive > pre-migration-packages.txt
```

---

## 2. Projekt-Analyse

### 2.1 Aktuelle Projekt-Struktur

**Projekte und ihre TargetFrameworks:**

| Projekt | Aktuelles Framework | Neues Framework | Typ |
|---------|-------------------|-----------------|-----|
| BookLoggerApp.Core | net9.0 | net10.0 | Class Library |
| BookLoggerApp.Infrastructure | net9.0 | net10.0 | Class Library |
| BookLoggerApp.Tests | net9.0 | net10.0 | Test Project |
| BookLoggerApp (MAUI) | net9.0-* | net10.0-* | MAUI App |

### 2.2 NuGet-Pakete Inventar

**BookLoggerApp.Core:**
- CommunityToolkit.Mvvm: 8.4.0 → **prüfen auf neueste Version**
- FluentValidation: 12.1.0 → **prüfen auf 13.x oder neuere**
- Microsoft.EntityFrameworkCore: 9.0.0 → **10.0.0**

**BookLoggerApp.Infrastructure:**
- CsvHelper: 33.1.0 → **prüfen auf neueste Version**
- FluentValidation: 12.1.0 → **prüfen auf 13.x oder neuere**
- Microsoft.EntityFrameworkCore.Design: 9.0.0 → **10.0.0**
- Microsoft.EntityFrameworkCore.Sqlite: 9.0.0 → **10.0.0**

**BookLoggerApp.Tests:**
- Microsoft.EntityFrameworkCore.InMemory: 9.0.10 → **10.0.0**
- xunit: 2.9.0 → **prüfen auf neueste Version**
- FluentAssertions: 8.6.0 → **prüfen auf neueste Version**
- xunit.runner.visualstudio: 2.8.2 → **prüfen auf neueste Version**
- Microsoft.NET.Test.Sdk: 17.11.1 → **prüfen auf neueste Version**

**BookLoggerApp (MAUI):**
- Microsoft.EntityFrameworkCore.Design: 9.0.10 → **10.0.0**
- Microsoft.Extensions.Logging.Configuration: 9.0.9 → **10.0.0**
- Microsoft.Maui.Controls: $(MauiVersion) → **automatisch mit .NET 10**
- Microsoft.AspNetCore.Components.WebView.Maui: $(MauiVersion) → **automatisch mit .NET 10**
- Microsoft.Extensions.Logging.Debug: 9.0.5 → **10.0.0**

### 2.3 CI/CD Pipeline

**GitHub Actions Workflow (.github/workflows/ci.yml):**
- Aktuell: `dotnet-version: 9.0.x`
- Neu: `dotnet-version: 10.0.x`

---

## 3. Migrations-Strategie

### 3.1 Reihenfolge der Migration

**Bottom-Up Approach (empfohlen):**
1. **BookLoggerApp.Core** (keine Abhängigkeiten)
2. **BookLoggerApp.Infrastructure** (abhängig von Core)
3. **BookLoggerApp.Tests** (abhängig von Core + Infrastructure)
4. **BookLoggerApp (MAUI)** (abhängig von Core + Infrastructure)
5. **CI/CD Pipeline** (nach erfolgreichen lokalen Builds)

### 3.2 Risiko-Management

**Kritische Bereiche:**
- Entity Framework Core Migration (Breaking Changes möglich)
- MAUI Platform-spezifische APIs
- NuGet-Paket Kompatibilität
- SQLite Provider Änderungen

**Mitigation:**
- Schrittweise Migration
- Tests nach jedem Schritt
- Rollback-Plan bereithalten
- Separate Branch für Migration

### 3.3 Testing-Strategie

**Nach jedem Schritt:**
1. `dotnet restore` erfolgreich
2. `dotnet build` ohne Fehler
3. Unit Tests grün (`dotnet test`)
4. App startet erfolgreich (für MAUI)
5. Manuelle Rauchtest-Durchführung

---

## 4. Schritt-für-Schritt Durchführung

### Phase 1: BookLoggerApp.Core Migration

**Schritt 1.1: TargetFramework ändern**

Datei: `BookLoggerApp.Core/BookLoggerApp.Core.csproj`

```xml
<!-- VORHER -->
<TargetFramework>net9.0</TargetFramework>

<!-- NACHHER -->
<TargetFramework>net10.0</TargetFramework>
```

**Schritt 1.2: NuGet-Pakete aktualisieren**

```bash
cd BookLoggerApp.Core

# Entity Framework Core aktualisieren
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.0

# Weitere Pakete auf neueste stabile Versionen aktualisieren
dotnet add package CommunityToolkit.Mvvm --version 8.4.0  # oder neuere
dotnet add package FluentValidation --version 13.0.0  # oder neuere
```

**Schritt 1.3: Restore & Build**

```bash
dotnet restore BookLoggerApp.Core/BookLoggerApp.Core.csproj
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj -c Release

# Bei Erfolg: Commit
git add BookLoggerApp.Core/BookLoggerApp.Core.csproj
git commit -m "Migrate BookLoggerApp.Core to .NET 10"
```

**Schritt 1.4: Validierung**

```bash
# Prüfen, ob das Projekt korrekt auf .NET 10 zielt
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj --verbosity detailed | grep "TargetFramework"
```

---

### Phase 2: BookLoggerApp.Infrastructure Migration

**Schritt 2.1: TargetFramework ändern**

Datei: `BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj`

```xml
<!-- VORHER -->
<TargetFramework>net9.0</TargetFramework>

<!-- NACHHER -->
<TargetFramework>net10.0</TargetFramework>
```

**Schritt 2.2: NuGet-Pakete aktualisieren**

```bash
cd BookLoggerApp.Infrastructure

# EF Core Pakete aktualisieren
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0

# CsvHelper aktualisieren
dotnet add package CsvHelper --version 33.1.0  # oder neuere

# FluentValidation aktualisieren
dotnet add package FluentValidation --version 13.0.0  # oder neuere
```

**Schritt 2.3: Restore & Build**

```bash
dotnet restore BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
dotnet build BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj -c Release

# Bei Erfolg: Commit
git add BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
git commit -m "Migrate BookLoggerApp.Infrastructure to .NET 10"
```

**Schritt 2.4: EF Core Migrations prüfen**

```bash
# Prüfen, ob bestehende Migrations kompatibel sind
dotnet ef migrations list --project BookLoggerApp.Infrastructure

# Falls erforderlich: Neue Migration erstellen
dotnet ef migrations add Net10Migration --project BookLoggerApp.Infrastructure
```

---

### Phase 3: BookLoggerApp.Tests Migration

**Schritt 3.1: TargetFramework ändern**

Datei: `BookLoggerApp.Tests/BookLoggerApp.Tests.csproj`

```xml
<!-- VORHER -->
<TargetFramework>net9.0</TargetFramework>

<!-- NACHHER -->
<TargetFramework>net10.0</TargetFramework>
```

**Schritt 3.2: NuGet-Pakete aktualisieren**

```bash
cd BookLoggerApp.Tests

# EF Core InMemory Provider aktualisieren
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 10.0.0

# Test-Framework-Pakete aktualisieren
dotnet add package xunit --version 2.9.0  # oder neuere
dotnet add package FluentAssertions --version 8.6.0  # oder neuere
dotnet add package xunit.runner.visualstudio --version 2.8.2  # oder neuere
dotnet add package Microsoft.NET.Test.Sdk --version 18.0.0  # oder neuere (falls verfügbar)
```

**Schritt 3.3: Restore & Build**

```bash
dotnet restore BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
dotnet build BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release
```

**Schritt 3.4: Tests ausführen**

```bash
# Alle Tests ausführen
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release

# Bei Erfolg: Commit
git add BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
git commit -m "Migrate BookLoggerApp.Tests to .NET 10"
```

**Schritt 3.5: Test-Ergebnisse analysieren**

```bash
# Detaillierte Test-Ausgabe
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --logger "console;verbosity=detailed"

# Falls Tests fehlschlagen: Ursachen dokumentieren und beheben
```

---

### Phase 4: BookLoggerApp (MAUI) Migration

**Schritt 4.1: TargetFrameworks ändern**

Datei: `BookLoggerApp/BookLoggerApp.csproj`

```xml
<!-- VORHER -->
<TargetFrameworks>net9.0-android;net9.0-ios;net9.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net9.0-windows10.0.19041.0</TargetFrameworks>

<!-- NACHHER -->
<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
```

**WICHTIG:** Prüfen Sie die Windows SDK-Version. Möglicherweise muss auch diese aktualisiert werden:
```xml
<!-- Möglicherweise auf neuere Version -->
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.22621.0</TargetFrameworks>
```

**Schritt 4.2: NuGet-Pakete aktualisieren**

```bash
cd BookLoggerApp

# Microsoft Extensions Pakete aktualisieren
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add package Microsoft.Extensions.Logging.Configuration --version 10.0.0
dotnet add package Microsoft.Extensions.Logging.Debug --version 10.0.0
```

**HINWEIS:** MAUI-Pakete (`Microsoft.Maui.Controls`, `Microsoft.AspNetCore.Components.WebView.Maui`) verwenden `$(MauiVersion)` und werden automatisch mit .NET 10 SDK aktualisiert.

**Schritt 4.3: MAUI Workload verifizieren**

```bash
# MAUI Workload für .NET 10 nochmals prüfen
dotnet workload list

# Falls nicht vorhanden oder veraltet:
dotnet workload update
```

**Schritt 4.4: Restore & Build**

```bash
# Restore für alle Plattformen
dotnet restore BookLoggerApp/BookLoggerApp.csproj

# Build für Android (Beispiel)
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -c Release

# Build für alle konfigurierten Frameworks
dotnet build BookLoggerApp/BookLoggerApp.csproj -c Release
```

**Schritt 4.5: Platform-spezifische Builds testen**

```bash
# Android
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -c Debug

# iOS (auf macOS)
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-ios -c Debug

# macOS Catalyst (auf macOS)
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-maccatalyst -c Debug

# Windows (auf Windows)
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-windows10.0.19041.0 -c Debug
```

**Schritt 4.6: App-Funktionalität testen**

```bash
# Android Emulator/Gerät
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -c Debug -t:Run

# Manuelle Tests durchführen:
# - App startet erfolgreich
# - Datenbank wird initialisiert
# - CRUD-Operationen für Bücher funktionieren
# - Reading Sessions können erstellt werden
# - Navigation zwischen Seiten funktioniert
# - Gamification-Features (Pflanzen, XP) funktionieren
```

**Schritt 4.7: Commit**

```bash
git add BookLoggerApp/BookLoggerApp.csproj
git commit -m "Migrate BookLoggerApp (MAUI) to .NET 10"
```

---

## 5. NuGet-Pakete Update

### 5.1 Automatisiertes Update aller Pakete

**Alle Pakete auf neueste stabile Versionen aktualisieren:**

```bash
# Tool installieren (falls noch nicht vorhanden)
dotnet tool install --global dotnet-outdated-tool

# Veraltete Pakete anzeigen
dotnet outdated

# Automatisches Update (Vorsicht: kann Breaking Changes enthalten)
dotnet outdated --upgrade
```

### 5.2 Manuelle Paket-Updates mit Versionsprüfung

**Für jedes Projekt einzeln:**

```bash
# BookLoggerApp.Core
cd BookLoggerApp.Core
dotnet list package --outdated
dotnet add package CommunityToolkit.Mvvm
dotnet add package FluentValidation
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.0

# BookLoggerApp.Infrastructure
cd ../BookLoggerApp.Infrastructure
dotnet list package --outdated
dotnet add package CsvHelper
dotnet add package FluentValidation
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.0

# BookLoggerApp.Tests
cd ../BookLoggerApp.Tests
dotnet list package --outdated
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 10.0.0
dotnet add package xunit
dotnet add package FluentAssertions
dotnet add package xunit.runner.visualstudio
dotnet add package Microsoft.NET.Test.Sdk

# BookLoggerApp (MAUI)
cd ../BookLoggerApp
dotnet list package --outdated
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add package Microsoft.Extensions.Logging.Configuration --version 10.0.0
dotnet add package Microsoft.Extensions.Logging.Debug --version 10.0.0
```

### 5.3 Paket-Kompatibilitätsprüfung

**Wichtige Pakete und ihre .NET 10 Kompatibilität:**

| Paket | .NET 9 Version | .NET 10 Zielversion | Kompatibilität |
|-------|---------------|---------------------|----------------|
| Microsoft.EntityFrameworkCore | 9.0.0 | 10.0.0 | ✅ Native Support |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.0 | 10.0.0 | ✅ Native Support |
| Microsoft.EntityFrameworkCore.InMemory | 9.0.10 | 10.0.0 | ✅ Native Support |
| CommunityToolkit.Mvvm | 8.4.0 | 8.4.0+ | ✅ Multi-targeting |
| FluentValidation | 12.1.0 | 13.x | ✅ Multi-targeting |
| CsvHelper | 33.1.0 | 33.x+ | ✅ Multi-targeting |
| xunit | 2.9.0 | 2.9.x+ | ✅ Multi-targeting |
| FluentAssertions | 8.6.0 | 8.6.x+ | ✅ Multi-targeting |

**Empfohlene Versionen nach Migration:**

```xml
<!-- BookLoggerApp.Core -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
<PackageReference Include="FluentValidation" Version="13.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />

<!-- BookLoggerApp.Infrastructure -->
<PackageReference Include="CsvHelper" Version="33.1.0" />
<PackageReference Include="FluentValidation" Version="13.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />

<!-- BookLoggerApp.Tests -->
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="FluentAssertions" Version="8.6.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />

<!-- BookLoggerApp (MAUI) -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Configuration" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
```

---

## 6. CI/CD Pipeline Update

### 6.1 GitHub Actions Workflow anpassen

**Datei:** `.github/workflows/ci.yml`

**Änderung 1: .NET Version aktualisieren**

```yaml
# VORHER
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: 9.0.x

# NACHHER
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: 10.0.x
```

**Änderung 2: MAUI Workload (falls MAUI in CI gebaut werden soll - aktuell nicht der Fall)**

```yaml
# Optional: Falls MAUI in CI gebaut werden soll
- name: Install MAUI Workload
  run: dotnet workload install maui
```

**Komplettes aktualisiertes Workflow-Beispiel:**

```yaml
name: CI

on:
  push:
    branches: [ main ]
    paths-ignore:
      - '**/*.md'
      - '**/*.png'
      - '**/*.jpg'
      - '**/*.jpeg'
      - '**/*.svg'
      - '.github/**'
  pull_request:
    branches: [ main ]
  workflow_dispatch:

jobs:
  build-test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      actions: read
      checks: write
      pull-requests: write
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x  # ← GEÄNDERT

      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
          restore-keys: nuget-${{ runner.os }}-

      - run: dotnet restore BookLoggerApp.Core/BookLoggerApp.Core.csproj
      - run: dotnet restore BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

      - run: dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj -c Release --no-restore
      - run: dotnet build BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --no-restore

      - name: Run Tests (trx)
        run: dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --no-build --logger "trx;LogFileName=test_results.trx"

      - name: Publish Test Results
        if: always()
        uses: dorny/test-reporter@v1
        with:
          name: xUnit Tests
          path: "**/TestResults/*.trx"
          reporter: dotnet-trx

      - name: Upload TRX (optional)
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-trx
          path: "**/TestResults/*.trx"
```

**Schritt 6.2: Commit & Push**

```bash
git add .github/workflows/ci.yml
git commit -m "Update CI pipeline to .NET 10"
git push origin feature/migrate-to-net10
```

**Schritt 6.3: Pipeline-Test**

- Pull Request erstellen
- CI-Pipeline beobachten
- Sicherstellen, dass alle Jobs erfolgreich durchlaufen

---

## 7. Testing & Validierung

### 7.1 Unit Tests

**Alle Tests ausführen:**

```bash
# Mit detaillierter Ausgabe
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --logger "console;verbosity=detailed"

# Mit Code Coverage (optional)
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release /p:CollectCoverage=true
```

**Erwartete Ergebnisse:**
- ✅ Alle bestehenden Tests müssen grün sein
- ✅ Keine neuen Warnings
- ✅ Keine Performance-Degradierung

### 7.2 Integration Tests

**Manuelle App-Tests durchführen:**

**Android Emulator:**
```bash
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -c Debug -t:Run
```

**Test-Checklist:**
- [ ] App startet ohne Crash
- [ ] Splash Screen wird angezeigt
- [ ] Datenbank wird initialisiert (Check Debug-Logs)
- [ ] Navigation funktioniert (alle Seiten erreichbar)
- [ ] CRUD-Operationen für Bücher:
  - [ ] Buch hinzufügen
  - [ ] Buch bearbeiten
  - [ ] Buch löschen
  - [ ] Buch-Details anzeigen
- [ ] Reading Sessions:
  - [ ] Session starten
  - [ ] Session beenden
  - [ ] Fortschritt speichern
- [ ] Gamification:
  - [ ] XP wird korrekt berechnet
  - [ ] Level-Ups funktionieren
  - [ ] Pflanzen können gekauft werden
  - [ ] Pflanzen-Boosts werden angewendet
- [ ] Genres:
  - [ ] Genres zuweisen
  - [ ] Genre-Statistiken anzeigen
- [ ] Quotes & Annotations:
  - [ ] Zitat hinzufügen
  - [ ] Annotation erstellen
- [ ] Goals:
  - [ ] Reading Goal erstellen
  - [ ] Fortschritt tracken
- [ ] Stats:
  - [ ] Statistiken werden korrekt angezeigt
  - [ ] Charts rendern korrekt
- [ ] Settings:
  - [ ] Einstellungen ändern
  - [ ] Einstellungen werden persistiert
- [ ] Import/Export:
  - [ ] Daten exportieren
  - [ ] Daten importieren

### 7.3 Performance-Tests

**Startup-Zeit messen:**
```bash
# App starten und Zeit messen
# Vergleich zu .NET 9 Baseline
```

**Database Performance:**
```bash
# Große Datenmenge testen
# - 100+ Bücher
# - 1000+ Reading Sessions
# Vergleich zu .NET 9 Baseline
```

### 7.4 Regression Tests

**Bekannte Problembereiche prüfen:**
- EF Core Migrations (Schema-Änderungen?)
- SQLite Provider (Datenbankzugriff funktioniert?)
- MAUI Blazor Interop (JavaScript-Aufrufe funktionieren?)
- Platform-spezifische APIs (FileSystem, Permissions)

---

## 8. Rollback-Plan

### 8.1 Schneller Rollback (Git)

**Falls kritische Probleme auftreten:**

```bash
# Zurück zur .NET 9 Version
git checkout main

# Oder: Branch löschen und neu starten
git branch -D feature/migrate-to-net10
git checkout -b feature/migrate-to-net10 backup-before-net10-migration
```

### 8.2 Projekt-Dateien zurücksetzen

**Einzelne Projekte zurücksetzen:**

```bash
# BookLoggerApp.Core zurücksetzen
git checkout main -- BookLoggerApp.Core/BookLoggerApp.Core.csproj

# Restore & Build
dotnet restore BookLoggerApp.Core/BookLoggerApp.Core.csproj
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj
```

### 8.3 NuGet Cache leeren

**Bei Package-Problemen:**

```bash
# NuGet Cache löschen
dotnet nuget locals all --clear

# Packages neu herunterladen
dotnet restore
```

### 8.4 Rollback-Dokumentation

**Probleme dokumentieren:**

```markdown
# Rollback durchgeführt am: [DATUM]
# Grund: [BESCHREIBUNG]
# Betroffene Komponenten: [LISTE]
# Nächste Schritte: [AKTIONSPLAN]
```

---

## 9. Bekannte Breaking Changes

### 9.1 Entity Framework Core 10.0

**Potenzielle Breaking Changes:**

1. **Änderungen in Query-Verhalten:**
   - Split queries könnten anders funktionieren
   - Lazy loading Verhalten könnte sich geändert haben
   - Tracking vs. No-Tracking Unterschiede

2. **SQLite Provider:**
   - Neue SQLite-Version könnte erforderlich sein
   - Änderungen in Datentyp-Mappings

3. **Migrations:**
   - Möglicherweise neue Annotations erforderlich
   - Indexing-Strategien könnten sich geändert haben

**Mitigations:**
```csharp
// In AppDbContext: Logging aktivieren für Debugging
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.EnableSensitiveDataLogging();
    optionsBuilder.EnableDetailedErrors();
    optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
}
```

### 9.2 MAUI Breaking Changes

**Potenzielle Änderungen:**

1. **Blazor Hybrid:**
   - JavaScript Interop könnte sich geändert haben
   - WebView-Konfiguration möglicherweise anders

2. **Platform-spezifische APIs:**
   - Android API-Level Anforderungen
   - iOS/macOS Deployment-Targets

3. **Resource Management:**
   - Änderungen in Image-Verarbeitung
   - Font-Handling

**Prüfen:**
```bash
# MAUI Release Notes lesen
# https://github.com/dotnet/maui/releases
```

### 9.3 CommunityToolkit.Mvvm

**Mögliche Änderungen:**
- ObservableProperty Source Generators
- RelayCommand Verhalten
- Messenger Änderungen

**Prüfen:**
```bash
# Release Notes prüfen
# https://github.com/CommunityToolkit/dotnet/releases
```

### 9.4 FluentValidation

**Breaking Changes bei Version 13.x:**
- Validator-Lifecycle
- Async-Validation
- Custom Validators

**Dokumentation:**
```bash
# Breaking Changes lesen
# https://docs.fluentvalidation.net/en/latest/upgrading-to-13.html
```

---

## 10. Post-Migration Optimierungen

### 10.1 Performance-Optimierungen nutzen

**C# 13 Features nutzen (falls .NET 10 C# 13 unterstützt):**

```csharp
// Beispiel: Collection Expressions
// VORHER
var books = new List<Book> { book1, book2 };

// NACHHER (C# 13)
Book[] books = [book1, book2];
```

**EF Core 10.0 Features:**
```csharp
// Neue Query-Optimierungen
// Prüfen: https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew
```

### 10.2 Dependency Updates

**Langfristige Updates planen:**

```bash
# Erstellen einer Dependabot-Konfiguration
# .github/dependabot.yml
```

```yaml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10
```

### 10.3 Code Modernisierung

**Nullable Reference Types prüfen:**
```bash
# Alle Warnings prüfen
dotnet build /p:TreatWarningsAsErrors=true
```

**Async Best Practices:**
```csharp
// ConfigureAwait(false) wo möglich
// Async all the way
// Keine blocking calls (Task.Result, Task.Wait)
```

### 10.4 MAUI Performance-Tuning

**Android:**
```xml
<!-- In BookLoggerApp.csproj -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net10.0-android'">
  <AndroidEnableProfiledAot>true</AndroidEnableProfiledAot>
  <AndroidUseAapt2>true</AndroidUseAapt2>
  <EnableLLVM>true</EnableLLVM>
</PropertyGroup>
```

**iOS:**
```xml
<!-- In BookLoggerApp.csproj -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net10.0-ios'">
  <MtouchLink>SdkOnly</MtouchLink>
  <EnableSGenConc>true</EnableSGenConc>
</PropertyGroup>
```

### 10.5 Dokumentation aktualisieren

**CLAUDE.md aktualisieren:**
```markdown
## Tech Stack
- [.NET 10 MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui)  # ← Aktualisieren
- SQLite für lokale Datenspeicherung
- MVVM + Dependency Injection
- GitHub Actions für CI/CD

## Important Notes
- Project uses latest C# language version and .NET 10  # ← Aktualisieren
```

**README.md aktualisieren:**
```markdown
## 🔧 Tech Stack
- [.NET 10 MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui)  # ← Aktualisieren
```

---

## Checkliste: Migrations-Abschluss

### Pre-Migration
- [ ] .NET 10 SDK installiert
- [ ] MAUI Workloads installiert
- [ ] Git Branch erstellt (`feature/migrate-to-net10`)
- [ ] Backup-Tag erstellt
- [ ] Lokales Backup erstellt
- [ ] Aktuelle Package-Versionen dokumentiert

### Migration Core & Infrastructure
- [ ] BookLoggerApp.Core TargetFramework auf net10.0
- [ ] BookLoggerApp.Core NuGet-Pakete aktualisiert
- [ ] BookLoggerApp.Core Build erfolgreich
- [ ] BookLoggerApp.Infrastructure TargetFramework auf net10.0
- [ ] BookLoggerApp.Infrastructure NuGet-Pakete aktualisiert
- [ ] BookLoggerApp.Infrastructure Build erfolgreich
- [ ] EF Core Migrations geprüft

### Migration Tests
- [ ] BookLoggerApp.Tests TargetFramework auf net10.0
- [ ] BookLoggerApp.Tests NuGet-Pakete aktualisiert
- [ ] BookLoggerApp.Tests Build erfolgreich
- [ ] Alle Unit Tests grün

### Migration MAUI
- [ ] BookLoggerApp TargetFrameworks auf net10.0-*
- [ ] BookLoggerApp NuGet-Pakete aktualisiert
- [ ] BookLoggerApp Build erfolgreich (alle Plattformen)
- [ ] Android: App startet und funktioniert
- [ ] iOS: Build erfolgreich (falls verfügbar)
- [ ] macOS Catalyst: Build erfolgreich (falls verfügbar)
- [ ] Windows: Build erfolgreich (falls verfügbar)

### CI/CD
- [ ] GitHub Actions Workflow auf .NET 10 aktualisiert
- [ ] CI-Pipeline läuft erfolgreich durch

### Testing & Validierung
- [ ] Alle Unit Tests grün
- [ ] Manuelle App-Tests durchgeführt (siehe Checklist oben)
- [ ] Performance vergleichbar oder besser als .NET 9
- [ ] Keine kritischen Bugs gefunden

### Dokumentation
- [ ] CLAUDE.md aktualisiert
- [ ] README.md aktualisiert (falls erforderlich)
- [ ] Migration-Notes dokumentiert

### Abschluss
- [ ] Pull Request erstellt
- [ ] Code Review durchgeführt
- [ ] In main-Branch mergen
- [ ] Release-Tag erstellen (z.B. `v2.0.0-net10`)
- [ ] Release Notes veröffentlichen

---

## Zeitplan (Beispiel)

| Phase | Dauer | Beschreibung |
|-------|-------|--------------|
| Vorbereitung | 30 min | SDK Installation, Backup, Branch erstellen |
| Core Migration | 30 min | TargetFramework + NuGet Updates |
| Infrastructure Migration | 30 min | TargetFramework + NuGet Updates |
| Tests Migration | 30 min | TargetFramework + NuGet Updates + Ausführung |
| MAUI Migration | 1-2 h | TargetFrameworks + NuGet + Platform Builds |
| CI/CD Update | 15 min | Workflow anpassen |
| Testing | 1-2 h | Umfassende manuelle Tests |
| Dokumentation | 15 min | CLAUDE.md, README.md |
| **Gesamt** | **4-6 h** | |

---

## Kontakte & Ressourcen

**Offizielle Dokumentation:**
- .NET 10 Release Notes: https://github.com/dotnet/core/tree/main/release-notes/10.0
- MAUI Release Notes: https://github.com/dotnet/maui/releases
- EF Core What's New: https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew
- Breaking Changes: https://learn.microsoft.com/dotnet/core/compatibility/10.0

**Community:**
- .NET Discord: https://aka.ms/dotnet-discord
- MAUI GitHub Discussions: https://github.com/dotnet/maui/discussions
- Stack Overflow: Tag `.net-10`, `maui`, `ef-core-10.0`

**Troubleshooting:**
- .NET CLI Issues: https://github.com/dotnet/sdk/issues
- MAUI Issues: https://github.com/dotnet/maui/issues
- EF Core Issues: https://github.com/dotnet/efcore/issues

---

## Notizen & Lessons Learned

**Während der Migration ausgefüllt werden:**

```
[DATUM] - [PROBLEM/ERKENNTNIS]
______________________________________

Beispiel:
2025-11-27 - EF Core 10.0: Split Query Verhalten hat sich geändert
Lösung: ...
```

---

**Ende des Migrationsplans**

Viel Erfolg bei der Migration! 🚀
