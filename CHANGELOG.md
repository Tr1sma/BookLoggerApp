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

- Erweiterte Statistiken: 3-Tab-System (Übersicht | Trends | Analysen) auf der Statistik-Seite mit Blazor-ApexCharts
  - Trends-Tab: Lese-Kalender (Heatmap), Wochentag-Verteilung, Tageszeit-Analyse mit Fun-Labels (z.B. „Nachteule 🦉"), Session-Längen-Verteilung, Monatlicher Leseverlauf, Lesegeschwindigkeit (Seiten/Stunde), Durchschnittliche Lesedauer pro Buch
  - Analysen-Tab: Jahresvergleich, Genre-Radar (Spinnennetz-Diagramm), Abschlussquote (Donut-Chart), Buchlängen-Vorliebe, Meistgelesene Autoren
- Genre-spezifische Bewertungskategorien: 5 neue Kategorien (Spannung, Humor, Informationsgehalt, Emotionale Tiefe, Atmosphäre) ergänzen die bestehenden 6 Kategorien. Beim Bewerten eines Buches werden nur die zum Genre passenden Kategorien angezeigt — weitere Kategorien lassen sich per Dropdown aufklappen.

### Behoben

- Changelog wird nach einem Update wieder zuverlässig in der App angezeigt, auch wenn die aktuelle Version noch keinen eigenen Changelog-Eintrag hat

### Geändert

- "Getting Started"-Seite auf sehr kleinen mobilen Displays kompakter gestaltet: engeres Hero-Layout, kleinere Kartenabstände und reduzierter vertikaler Leerraum ohne Funktionsänderung
- Shop-Seite (Pflanzen & Dekorationen) deutlich kompakter gestaltet: dichteres Kartenraster (mehr Karten pro Reihe, auch auf sehr kleinen Handys zwei Spalten statt einer), kleinere Karten-Bilder und Paddings, schlankerer Header, kompakteres Kaufen-Modal mit viewport-basierter Maximalhöhe — auf einem typischen Android-Handy sind jetzt deutlich mehr Items auf einen Blick sichtbar
- Stats-Seite: „Top Rated Books"-Abschnitt kompakter gestaltet (Inline-Rang statt Kreis-Badge, horizontal scrollbare Kategorie-Filter, engere Abstände und 3 statt 5 Einträge als Standard)
- Settings-Seite: kompakteres Layout mit reduzierten Abständen, Paddings und Heading-Größen für bessere Übersicht auf mobilen Displays — mehr Sektionen auf einen Blick sichtbar, ohne Funktions- oder Bedienungsänderung
- Settings-Seite weiter aufgeräumt: neue Hero-Karte mit Version oben, thematisch zusammengelegte Abschnitte („Data & Backup" inkl. Danger-Zone, „Help & Community" inkl. Getting Started + Buy-me-a-coffee), „More Info"-Sammelabschnitt für Diagnostics + Privacy Policy — deutlich weniger Scrollen, gleiche Funktionen
- Stats-Seite: „Level Milestones"-Liste deutlich kompakter — aus der Vollzeilen-Liste wird ein Mini-Karten-Grid (3 Spalten auf kleinen Handys, bis zu 7 auf Desktop), kleinere Icons und Schriften, auf einem typischen Android-Handy sind dadurch viel mehr Milestones gleichzeitig sichtbar

## [0.9.0]

### Hinzugefügt

- Breite Regalgegenstände: Dekorationen mit `SlotWidth > 1` nehmen jetzt mehrere Slots auf dem Bücherregal ein (z.B. Globus, Marmorbuchstütze, Teleskop, alte Schriftrolle = 2 Slots)
- Neue Regaldekorationen (Kerzen, Stundenglas, Eulen-Figur, Globus u.v.m.) im Shop kaufbar und auf Regalen platzierbar — rein kosmetisch, günstig, per Level freigeschaltet
- Shop-Seite hat jetzt Tabs für Pflanzen und Dekorationen
- Versioniertes Onboarding mit Intro-Overlay und neuem "Getting Started"-Hub für geführte Missionen rund um erstes Buch, erste Lesesession, Ziele, Pflanzen, Wunschliste, Scanner, Sharing und Backup
- "Getting Started"-CTA auf Bücherregal und Dashboard sowie neuer Einstieg in den Settings zum erneuten Öffnen oder Wiederholen des Intros

### Geändert

- Stundenglas, Tintenfass & Feder, Eulen-Figur, Magische Leselampe, Drachen-Figur und Alchemie-Kolben sind jetzt 2 Slots breit für bessere Erkennbarkeit im Regal (11 von 14 Dekorationen nun 2-slot-breit)
- Onboarding-Overlay insgesamt kompakter (kleinere max. Breite/Höhe, engere Abstände, kleinere Überschriften) und farblich einheitlich dunkler an das BookHeart-Theme angepasst

### Behoben

- Tote Pflanzen werden beim Löschen jetzt vollständig entfernt, inklusive Regal-Verknüpfungen, und tauchen danach nicht mehr als "Add Plant"-Option im Bücherregal auf.
- Onboarding wird nach einem Update nicht mehr ungefragt Bestandsnutzern angezeigt, sondern nur noch neuen Installationen automatisch eingeblendet
- Intro-Schritte werden jetzt exakt fortgesetzt, der Zurück-/Skip-Flow ist korrekt navigierbar und die Rating-Erklärung verwendet die echten sechs Kategorien inklusive "Spice Level"
- "Delete All Data" setzt jetzt auch den gespeicherten Onboarding- und Missionsfortschritt sauber zurück
- Harter Blazor-Fehler beim ersten Antippen von "Add Book" im Onboarding-Intro behoben — Fehler beim Abschließen des Intros (z.B. kurzer DB-Lock beim Erststart) führen nicht mehr zum App-Absturz, sondern werden still abgefangen und das Overlay sauber ausgeblendet
- Changelog-Overlay erscheint jetzt nicht mehr über dem Buch-Hinzufügen-Formular wenn der Nutzer "Add Book" im Onboarding-Intro tippt — Changelog wird für neue Nutzer während des Onboardings generell unterdrückt
- Harter Blazor-Fehler beim ersten App-Start auf einer Neuinstallation behoben: GettingStartedCta auf dem Bücherregal wurde ohne DB-Initialisierungsschutz gerendert und löste eine "no such table"-Exception aus — Komponente wartet jetzt auf die DB-Initialisierung und schluckt verbleibende Fehler still
- Voraussetzungs-Hinweis im "Getting Started"-Hub zeigte bei bestimmten Missionen fälschlicherweise "Complete 'Add your first book' first" an, obwohl die tatsächliche Voraussetzung bereits erfüllt war — Logik korrigiert
- Dashboard/Bücherregal laden nach den neuen AppSettings-Änderungen wieder zuverlässig: fehlende EF-Migration für `HideGettingStartedCta` ergänzt, wodurch `PendingModelChanges`-Abbrüche beim Start vermieden werden
- "Getting Started"-CTA auf dem Bücherregal bleibt nicht mehr dauerhaft unsichtbar, wenn die Initialisierung beim ersten Laden kurz fehlschlägt; Sichtbarkeit wird jetzt robust aus Settings (`HideGettingStartedCta`) und Onboarding-Status neu aufgebaut
- Harter Blazor-Fehler auf dem Dashboard nach einer Cloud-Backup-Wiederherstellung behoben: Wenn die Pflanzenspezie-Verknüpfung nach einem Backup nicht aufgelöst werden konnte (fehlende PlantSpecies-Zeile), griff der PlantWidget-Template direkt auf `Plant.Species.Name` zu und stürzte ab — alle Species-Zugriffe sind jetzt Null-gesichert
- Harter Blazor-Fehler auf der Stats-Seite behoben: Division durch null bei der Pflanzenverstärkungs-Balkenanzeige wenn `TotalPlantBoost = 0` aber Pflanzendaten vorhanden waren
- Korrupte Cloud-Backups werden jetzt vor der Wiederherstellung per SQLite `PRAGMA integrity_check` geprüft und abgelehnt, anstatt die aktive Datenbank silent zu überschreiben
- Cloud-Backup-Wiederherstellung wirft nicht mehr den Fehler "database disk image is malformed" beim direkten Weiterverwenden der App: nach erfolgreichem Restore zeigt die Settings-Seite kurz einen "Backup restored"-Hinweis und startet BookHeart automatisch neu, damit alle SQLite-Verbindungspools, nativen File-Handles und Blazor-Komponenten frisch gegen die restaurierte Datenbank aufgebaut werden. Ein manueller Neustart ist dafür nicht mehr nötig.
- Scholar's Spectacles (Brille) ist jetzt korrekt als 2 Slots breit markiert — ihr Inhalts-Aspect-Ratio von ~3.8:1 passte visuell nie in einen einzelnen Regal-Slot
- Dekorationen im Bücherregal werden nicht mehr mit dem grünen Pflanzen-Tint gerendert und der Inhalt wird nicht mehr am unteren Rand gecroppt (`object-fit: contain` + zentrierte Position statt `cover` + `bottom` — die Regeln wurden bisher unbeabsichtigt von den Pflanzen-Cards geerbt)
- Automatischer Neustart nach Cloud-Backup-Wiederherstellung funktioniert jetzt zuverlässig auf Android 12+: bisher wurde nur ein `AlarmManager`-PendingIntent geplant und der Prozess sofort beendet, was von Android seit API 31 durch die Background-Activity-Launch-Restriktionen stillschweigend blockiert wurde — die App schloss sich zwar, startete aber nie neu. Der Restart nutzt jetzt eine Hybrid-Strategie (direktes `StartActivity` aus dem Vordergrund + `AlarmManager`-Fallback + kurze Verzögerung vor dem Prozess-Kill), damit BookHeart auf allen unterstützten Android-Versionen verlässlich neu startet
- `PendingModelChangesWarning`-Absturz beim App-Start nach dem Brille-SlotWidth-Fix behoben: die Seed-Änderung der `Scholar's Spectacles` auf `SlotWidth = 2` war ohne passende EF-Migration im Code gelandet, sodass das gebaute Modell vom `AppDbContextModelSnapshot` abwich und `Database.MigrateAsync()` beim Start schon vor dem Runtime-Sync abbrach. Nachgeliefert als Migration `FixSpectaclesSlotWidth`
- Cloud-Backup-Restore brach mit roter `Failed to restore backup`-Meldung ab und triggerte direkt danach Folgefehler auf allen Pages, sodass nur ein manueller App-Neustart die importierten Daten sichtbar machte. Ursache: nach dem DB-Swap feuerte `ImportExportService` über `AppSettingsProvider.InvalidateCache()` das `ProgressionChanged`-Event, dessen Subscriber auf noch nicht aufgeräumte DbContexts zugriffen und die gesamte `RestoreFromBackupAsync`-Methode mit einer Exception beendeten — der automatische Restart wurde dadurch gar nicht erst getriggert. Fix: der Restore-Pfad ruft `InvalidateCache(notifyProgressionChanged: false)` und umgeht das Event, `OnProgressionChanged`/`OnSettingsChanged` fangen zusätzlich Subscriber-Exceptions pro Handler ab, der Restore-Fehlerpfad zeigt jetzt Exception-Typ plus Inner-Exception im roten Alert, und der komplette Schritt-für-Schritt-Verlauf landet im bestehenden "Data Recovery Diagnostics"-Log. `AppRestartService.RestartApp` marshalt zusätzlich explizit auf den Android-Main-Thread, falls der Blazor-Dispatcher abweicht
- Erstinstallations-Race behoben, bei dem der erste Cloud-Backup-Restore auf einer frischen Installation mit `database disk image malformed` abbrach und anschließende Folgefehler auf allen Seiten verursachte. Ursache: `DbInitializer.InitializeAsync` läuft am App-Start fire-and-forget in einem Scope, dessen `AppDbContext` für die gesamte Init-Dauer (Migration, Plant/Decoration-Sync, Seed-Validation) eine aktive SQLite-Verbindung hält. Klickte der User schnell genug auf Restore, überschrieb `File.Copy` die DB-Datei unter dieser laufenden Verbindung und korrumpierte sie — `SqliteConnection.ClearAllPools()` räumt nur gepoolte, nicht aktiv verwendete Verbindungen. Fix: `SettingsViewModel.RestoreFromBackupAsync` und `ImportExportService.RestoreFromBackupAsync` warten jetzt beide explizit über `DatabaseInitializationHelper.EnsureInitializedAsync()` auf den vollständigen Abschluss des Startup-Initializers, bevor der DB-Swap beginnt. Beim zweiten Aufruf ist der `TaskCompletionSource` bereits gesetzt und der Await kostet nichts

## [0.8.1] - 2026-04-07

### Hinzugefügt

- Pflanzen im Bücherregal können jetzt direkt im Detail-Modal über ein Stift-Icon umbenannt werden
- BookHeart prüft jetzt auf Android auf verfügbare Play-Store-Updates und zeigt nach einem App-Update beim ersten Start die passenden Changelog-Einträge an

### Geändert

### Behoben

- Projektweiter Razor-/Restore-Fehler auf Windows behoben, indem die Paketversion für alle Target Frameworks konsistent aus der App-Version abgeleitet wird
- Das Android-App-Icon verwendet für Launcher-Themes jetzt keine monochrome Icon-Variante mehr, damit Icon Themes BookHeart nicht mehr einfarbig überlagern

## [0.8.2] - 2026-04-07

### Hinzugefügt

- Beim Beenden einer Lesesession erscheint bei aktiver Lese-Streak jetzt eine eigene Streak-Feier mit zusätzlichem, nach Streak-Tagen skaliertem XP-Bonus

### Geändert

### Behoben

- Die Feier-Reihenfolge nach einer Lesesession zeigt Level-Ups jetzt auch dann zuverlässig an, wenn gleichzeitig Buchabschluss und Streak-Bonus ausgelost wurden
- Abgebrochene oder nur gestartete Lesesessions ohne echten Fortschritt verlängern keine Streak mehr und lösen dadurch keinen Streak-XP-Bonus mehr aus
- Der Streak-XP-Bonus wird pro Tag nur noch einmal vergeben, nämlich bei der ersten qualifizierenden Lesesession des Tages
- Die Stats-Seite stürzt nicht mehr ab, wenn vorhandene, aber tote Pflanzen keinen aktiven XP-Boost mehr beitragen
- Die globale Fehleransicht wird nach einem Seitenwechsel wieder korrekt zurückgesetzt, statt weitere Seiten fälschlich ebenfalls als abgestürzt anzuzeigen

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
