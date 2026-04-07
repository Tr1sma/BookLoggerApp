# Changelog

Alle nennenswerten Änderungen an BookHeart werden in dieser Datei festgehalten.

Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

Versionsschema:

- `V0.x.y` – Pre-Release (vor Play-Store-Veröffentlichung)
- `V1.0.0` – Erster öffentlicher Play-Store-Release
- MAJOR wird auf 1 gesetzt wenn der erste public Play-Store-Upload erfolgt
- MINOR für neue Features, PATCH für Bugfixes und kleinere Änderungen

---
## [Unveröffentlicht]

### Hinzugefügt

- Beim Beenden einer Lesesession erscheint bei aktiver Lese-Streak jetzt eine eigene Streak-Feier mit zusätzlichem, nach Streak-Tagen skaliertem XP-Bonus

### Geändert

### Behoben

- Die Feier-Reihenfolge nach einer Lesesession zeigt Level-Ups jetzt auch dann zuverlässig an, wenn gleichzeitig Buchabschluss und Streak-Bonus ausgelost wurden
- Abgebrochene oder nur gestartete Lesesessions ohne echten Fortschritt verlängern keine Streak mehr und lösen dadurch keinen Streak-XP-Bonus mehr aus
- Der Streak-XP-Bonus wird pro Tag nur noch einmal vergeben, nämlich bei der ersten qualifizierenden Lesesession des Tages

## [0.8.1] - 2026-04-07

### Hinzugefügt

- Pflanzen im Bücherregal können jetzt direkt im Detail-Modal über ein Stift-Icon umbenannt werden
- BookHeart prüft jetzt auf Android auf verfügbare Play-Store-Updates und zeigt nach einem App-Update beim ersten Start die passenden Changelog-Einträge an

### Geändert

- README-Featureübersicht vollständig an die seit V0.1.0 hinzugefügten Funktionen angepasst (u.a. Widgets, Sharing, Scanner/Lookup, erweiterte Ziel- und Regalfeatures)
- Das Pflanzen-Detail-Modal im Bücherregal wurde insgesamt kompakter gestaltet

### Behoben

- Play-Store-Update-Hinweise und die Prüfung nach dem Wiederöffnen der App reagieren jetzt robuster auf laufende oder fehlgeschlagene In-App-Updates
- Zip-Slip-Sicherheitstest stabilisiert: Test-Mocks für Dateisystem und AppSettings sind jetzt vollständig deterministisch implementiert, damit der Test nur noch auf die eigentliche Pfadvalidierung fehlschlägt.
- Backup-Restore findet `booklogger.db` und den `covers`-Ordner jetzt robust auch bei abweichender Groß-/Kleinschreibung in ZIP-Inhalten
- AppSettingsProvider weist jetzt ungültige Münz-Beträge (`<= 0`) in `SpendCoinsAsync` und `AddCoinsAsync` mit `ArgumentOutOfRangeException` ab
- Start eines Buches ist jetzt idempotent: `DateStarted` bleibt beim erneuten Start erhalten und der Status wird nicht von `Reading`/`Completed` überschrieben
- Reading-Timer im ReadingViewModel aktualisiert die `ElapsedTime` jetzt threadsicher über den UI-Dispatcher, sodass PropertyChanged zuverlässig auf dem UI-Thread ausgelöst wird
- Scanner-Abschlusslogik robuster gemacht: Beim Schließen der Scanner-Seite ohne Cancel-Button wird der Scan jetzt sauber mit `null` beendet, inklusive optionalem Timeout/CancellationToken im Scanner-Service.
- Seitenvalidierung beim Beenden von Lesesessions berücksichtigt jetzt die Startseite der Session, sodass ein zu großes Seiten-Delta über das Buchende hinaus korrekt mit einer klaren Fehlermeldung abgewiesen wird
- Buchabschluss beim Speichern bewertet den tatsächlichen Datenbankstatus und verhindert dadurch doppelte XP-Vergabe bei wiederholtem Speichern ohne erneute Statusänderung
- Beim Verlassen der Wishlist beim Speichern wird die Bereinigung jetzt ebenfalls anhand des tatsächlich persistierten Status entschieden
- Beim Bearbeiten von Wishlist-Büchern bleibt `WishlistInfo` jetzt erhalten, solange der Status nicht explizit im Formular geändert wird
- Tippen auf Pflanzen im Bücherregal öffnet jetzt zuverlässig das Pflanzen-Modal; das Entfernen aus dem Regal wurde aus der überlagerten Pflanzenkarte in das Detail-Modal verlegt

## [0.8.0] - 2026-04-07

### Hinzugefügt

### Geändert

- Pflanzen im Bücherregal öffnen jetzt ein Detail-Modal mit Namen, Level, nächstem Gießzeitpunkt und Gießbutton
- Pflanzennamen werden im Shop-Kaufdialog und im Pflanzen-Modal des Bücherregals etwas größer dargestellt

### Behoben

- Plant-Shop-Karten zeigen bei nicht bezahlbaren Pflanzen nur noch den Preis und nicht mehr den Zusatz "Need X more"
- Pflanzenstatus und Gießlogik werden beim Laden und Interagieren zuverlässiger aus den aktuellen Backend-Daten aktualisiert
- Der Drag-and-Drop-Einfügebalken im Bücherregal richtet sich jetzt an der Höhe des tatsächlichen Ziel-Buchs aus statt über die gesamte Regalhöhe zu laufen

## [0.7.6] - 2026-04-02

### Hinzugefügt

- Cover-Bild aus Galerie wählen: Beim Bearbeiten oder Anlegen eines Buches kann das Cover-Bild jetzt direkt aus der Geräte-Galerie ausgewählt werden (nur Android). Das Bild wird sofort lokal gespeichert, sodass es auch bei einmaliger Berechtigungsvergabe ("Nur dieses Mal") erhalten bleibt.
- Android Home Screen Widgets: Drei neue Widgets fuer den Android-Startbildschirm — "Aktuelles Buch" (Cover, Titel, Fortschrittsbalken), "Lese-Streak" (aktuelle Streak-Tage mit Heute-Status), und "Lese-Ziel" (Fortschritt zum aktiven Leseziel mit Konfigurationsauswahl). Widgets aktualisieren sich automatisch alle 30 Minuten und sofort nach Aenderungen in der App (Session beenden, Buch speichern, Ziel erstellen/loeschen). Design passend zum BookHeart Cozy Dark Mode Theme.
- Regalfarbe anpassbar: In den Settings können die Farben der Bücherleisten (Planke unter jedem Buch) und der Regalleiste (untere Leiste jedes Regals) getrennt voneinander aus je 8 Holzfarb-Presets gewählt werden
- Der Android zurück button wurde in die app eingebunden

### Geändert

### Behoben

- Buttons (z.B. "Add Shelf", "Backup to Cloud") blieben nach dem Tippen visuell highlighted, bis man woanders hintippte — Hover-Styles werden jetzt nur noch auf Geräten mit Maus/Trackpad angewendet

## [V0.7.5] - 2026-04-02

### Hinzugefügt

- Stats teilen (Reading Wrapped): Auf der Stats-Seite können Lesestats als PNG im Instagram-Story-Format (1080×1920) geteilt werden. Zeitraum wählbar: Week, Month, Quarter, Year, All Time. Die Karte zeigt abgeschlossene Bücher, gelesene Seiten, Lesezeit, Lieblingsgenre und Top-3-Bücher im BookHeart-Design.
- Buchempfehlung teilen: Nach dem Abschließen eines Buchs erscheint in der Book-Completion-Feier ein "Share as Recommendation"-Button. Die Share-Karte (PNG) zeigt Titel, Autor, Cover (falls vorhanden), Seitenanzahl, Gesamtlesezeit, Gesamtbewertung mit Sternen und alle Einzelbewertungen mit farbigen Fortschrittsbalken
- Share-Icon auf der Buchdetailseite neben "Book Information" für alle abgeschlossenen Bücher
- "HIGHLY RECOMMENDED"-Badge auf der Share-Karte bei Bewertung ab 4.0

### Geändert
- Der komplexere Review-Flow wurde durch zwei einfache Modals ersetzt: erst App-Feedback, dann optional der Sprung zur Play-Store-Bewertung
- Der vereinfachte Review-Dialog erscheint erst ab Level 7, hoechstens zweimal pro Monat und kann ueber "Nicht mehr fragen" dauerhaft abgeschaltet werden
- Share-Karte mit lebendigerem Design: Warmer Gradient-Hintergrund, Ambient-Glow-Effekte, Gold-Sterne, farbcodierte Kategorien mit Fortschrittsbalken, dynamische Kartenhöhe je nach Inhalt
- Bei Buchabschluss wird direkt die Book-Completion-Feier angezeigt (ohne vorheriges Session-XP-Modal)
- XP-Anzeige in der Book-Completion-Feier zeigt die tatsächlich erhaltenen Gesamt-XP in kompaktem Layout

### Behoben
- Cover-Platzhalter auf Share-Karte entfernt wenn kein Cover vorhanden ist
- Textüberlappungen auf der Share-Karte zwischen Badge/Titel und Stats/Bewertung behoben

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
