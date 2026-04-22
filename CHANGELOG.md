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

### Geändert
- Changelogs vollständig überarbeitet/verbessert
### Behoben

## [0.9.6]

### Behoben

- Onboarding- und Update-Overlays waren auf Geräten mit klassischer 3-Tasten-Navigation teilweise verdeckt
- Android-Widget zeigt den Ziel-Fortschritt jetzt korrekt in der lokalen Zeitzone (zuvor konnten Bücher bei UTC-Offsets in der falschen Periode landen)
- Gieß-Benachrichtigungen berücksichtigen jetzt den Herz-der-Geschichten-Wachstumsbonus — Hinweise feuern rechtzeitig vor dem Durstig-/Welk-Zustand
- Buch-Fortschritt wird auf 0–100 % begrenzt; die aktuelle Seite wird nicht mehr über die Seitenzahl hinaus gespeichert (zuvor zeigte die UI z.B. „600 / 500 (100 %)")
- Mehrfaches Tippen auf „Abschließen" vergibt nicht mehr doppelt XP und überschreibt das Abschluss-Datum nicht
- Plant-Widget berechnet mit aktivem Herz der Geschichten die „Tage bis Level-Up" jetzt korrekt
- Lese-Ziele nutzen die lokale Zeitzone statt UTC — Bücher am späten Abend werden korrekt dem laufenden Tag zugeordnet
- Pflanzen- und Dekorations-Käufe erstatten Münzen zurück, wenn das Speichern fehlschlägt; fehlgeschlagene Käufe verteuern den nächsten Kauf nicht mehr
- „Alle Daten löschen" entfernt jetzt auch gekaufte Dekorationen und ihre Regal-Platzierungen
- Level-Up-Celebration erscheint auch, wenn nur der „Herz der Geschichten"-First-of-Day-Bonus über eine Level-Grenze hebt
- Beim gleichzeitigen Ändern von Buch-Status und Genre wird das Buch korrekt dem neuen Genre-Ziel zugeordnet
- XP-Berechnung mit Pflanzen-Boost rundet mathematisch korrekt (vereinzelte 1-XP-Verluste pro Session behoben)
- Doppel-Tap auf „Awesome!" in Session-Celebrations zeigt keine überlappenden Overlays mehr
- Abgebrochene Backup-Imports räumen halb kopierte Zwischendateien im Cache auf
- Shop-Sortierung (Pflanzen & Dekorationen) bleibt nach Reload konsistent
- Buch-Import mit zukünftigem Abschluss-Datum wird jetzt zuverlässig abgewiesen

## [0.9.4]

### Hinzugefügt

- Neue Prestige-Pflanze **Chronikbaum** (Lv 45 · 20.000 🪙 · 30 % XP-Boost) mit Streak-Wächter: rettet alle 14 Tage automatisch einen brechenden Lese-Streak
- Neue Prestige-Pflanze **Ewiger Phönix-Bonsai** (Lv 57 · 80.000 🪙 · 50 % XP-Boost) mit Phönix-Schutz: wiederbelebt sich selbst und schützt alle anderen Pflanzen vor dem Sterben
- Neue Ultimate-Dekoration **Herz der Geschichten** (Lv 70 · 200.000 🪙, nur 1× kaufbar): +25 % globaler XP-Boost, +25 % auf Level-Up-Münzen, +400 🪙 ab 30-Minuten-Sessions, doppeltes Pflanzenwachstum und +2,5 % der Next-Level-XP auf die erste Lese-Session des Tages
- Legendary-Visuals im Shop: warmer Beige-Rand, sanftes Pulsieren und „✨ Legendär"-Badge für Items mit Spezialfähigkeit
- Shop-Detail-Ansicht zeigt vor dem Kauf eine Beschreibung der Spezialfähigkeit

### Geändert

- Session-Abschluss-Celebration hebt Streak-Rettung und Herz-der-Geschichten-Boni jetzt explizit hervor

### Behoben

- Plant-Boost-Berechnung nutzt einen gemeinsamen Helper — UI-Anzeige und XP-Gewährung können nicht mehr voneinander abweichen
- Pflanzen-Level-Up-Münzen werden erst nach erfolgreichem Speichern gutgeschrieben (bei DB-Konflikten bleiben Münzen und Level konsistent)
- Dashboard-Wochenstatistiken („Diese Woche") beginnen jetzt am **Montag** (zuvor Sonntag, wodurch der Vorsonntag fälschlich mitgezählt wurde)

## [0.9.3]

### Geändert

- Coin-Belohnung pro Level-Up wächst jetzt progressiv statt linear (Formel: 50×Level + 3×Level²)
- Bessere Pflanzen geben einen deutlich höheren XP-Boost
- Mehrere kleine UI/UX-Updates und Verbesserungen

## [0.9.2]

### Hinzugefügt

- Erweiterte Statistiken mit 3-Tab-System (Übersicht | Trends | Analysen) auf der Statistik-Seite
  - Trends: Lese-Kalender (Heatmap), Wochentag- und Tageszeit-Analyse mit Fun-Labels (z.B. „Nachteule 🦉"), Session-Längen, Monats-Leseverlauf, Seiten/Stunde, durchschnittliche Lesedauer pro Buch
  - Analysen: Jahresvergleich, Genre-Radar, Abschlussquote, Buchlängen-Vorliebe, Top-Autoren
- Genre-spezifische Bewertungskategorien: 5 neue Kategorien (Spannung, Humor, Informationsgehalt, Emotionale Tiefe, Atmosphäre) ergänzen die bestehenden 6. Beim Bewerten werden nur passende Kategorien angezeigt, weitere per Dropdown aufklappbar.

### Geändert

- XP-Boosts für höherstufige Pflanzen deutlich erhöht (z.B. Mystic Tome Tree: 20% → 75%, Ancient Bonsai: 15% → 50%)
- Shop-Seite (Pflanzen & Dekorationen) deutlich kompakter: dichteres Kartenraster (zwei Spalten statt einer auf kleinen Handys), kompakteres Kaufen-Modal
- Settings-Seite aufgeräumt: Hero-Karte mit Version, thematische Abschnitte („Data & Backup", „Help & Community", „More Info"), reduzierte Abstände
- Stats-Seite: „Top Rated Books" kompakter (Inline-Rang, scrollbare Filter, 3 statt 5 Einträge); „Level Milestones" als Mini-Karten-Grid mit bis zu 7 Spalten
- „Getting Started"-Seite auf kleinen Displays kompakter

### Behoben

- Changelog wird nach einem Update wieder zuverlässig angezeigt, auch wenn die aktuelle Version keinen eigenen Eintrag hat

## [0.9.0]

### Hinzugefügt

- Breite Regalgegenstände: Dekorationen mit mehreren Slots (z.B. Globus, Marmorbuchstütze, Teleskop, alte Schriftrolle = 2 Slots)
- Neue Regaldekorationen im Shop (Kerzen, Stundenglas, Eulen-Figur, Globus u.v.m.) — rein kosmetisch, günstig, per Level freigeschaltet
- Shop-Seite hat jetzt Tabs für Pflanzen und Dekorationen
- Versioniertes Onboarding mit Intro-Overlay und neuer „Getting Started"-Hub für geführte Missionen (erstes Buch, erste Lesesession, Ziele, Pflanzen, Wunschliste, Scanner, Sharing, Backup)
- „Getting Started"-CTA auf Bücherregal und Dashboard sowie Einstieg in den Settings zum erneuten Öffnen des Intros

### Geändert

- 11 von 14 Dekorationen sind jetzt 2 Slots breit für bessere Erkennbarkeit im Regal (u.a. Stundenglas, Tintenfass & Feder, Magische Leselampe)
- Onboarding-Overlay kompakter und farblich einheitlich dunkler an das BookHeart-Theme angepasst

### Behoben

- Tote Pflanzen werden beim Löschen vollständig entfernt (inklusive Regal-Verknüpfungen) und tauchen nicht mehr als „Add Plant"-Option auf
- Dashboard stürzt nach einem Cloud-Backup-Restore nicht mehr ab, wenn eine Pflanzenspezie-Verknüpfung fehlt
- Stats-Seite: Division durch null bei der Pflanzenverstärkungs-Anzeige behoben (wenn Pflanzen vorhanden, aber XP-Boost = 0)
- Korrupte Cloud-Backups werden vor der Wiederherstellung per SQLite-Integritätsprüfung abgelehnt statt die aktive Datenbank stillschweigend zu überschreiben
- Automatischer Neustart nach Cloud-Backup-Wiederherstellung funktioniert jetzt zuverlässig auf Android 12+
- Cloud-Backup-Restore erzeugt keinen „database disk image malformed"-Fehler mehr: die App startet nach erfolgreichem Restore automatisch neu, damit SQLite-Verbindungen, File-Handles und Blazor-Komponenten frisch aufgebaut werden
- Erstinstallations-Race behoben: der erste Cloud-Backup-Restore auf einer frischen Installation konnte die Datenbank zuvor korrumpieren, weil der Startup-Initializer noch eine aktive Verbindung hielt

## [0.8.2] - 2026-04-07

### Hinzugefügt

- Beim Beenden einer Lesesession erscheint bei aktiver Lese-Streak eine eigene Streak-Feier mit zusätzlichem, nach Streak-Tagen skaliertem XP-Bonus

### Behoben

- Stats-Seite stürzt nicht mehr ab, wenn vorhandene, aber tote Pflanzen keinen aktiven XP-Boost mehr beitragen
- Globale Fehleransicht wird nach einem Seitenwechsel wieder korrekt zurückgesetzt, statt weitere Seiten fälschlich als abgestürzt anzuzeigen

## [0.8.1] - 2026-04-07

### Hinzugefügt

- Pflanzen im Bücherregal können direkt im Detail-Modal über ein Stift-Icon umbenannt werden
- BookHeart prüft auf Android auf verfügbare Play-Store-Updates und zeigt nach einem App-Update die passenden Changelog-Einträge an

### Geändert

- README-Featureübersicht vollständig an die seit V0.1.0 hinzugefügten Funktionen angepasst
- Pflanzen-Detail-Modal im Bücherregal insgesamt kompakter gestaltet

### Behoben

- Projektweiter Razor-/Restore-Fehler auf Windows behoben
- Android-App-Icon verwendet für Launcher-Themes keine monochrome Variante mehr — Icon Themes überlagern BookHeart nicht mehr einfarbig
- Backup-Restore findet `booklogger.db` und den `covers`-Ordner jetzt auch bei abweichender Groß-/Kleinschreibung in ZIP-Inhalten
- Start eines Buches ist jetzt idempotent: `DateStarted` bleibt beim erneuten Start erhalten und der Status wird nicht von „Reading"/"Completed" überschrieben
- Reading-Timer aktualisiert die Laufzeit threadsicher über den UI-Dispatcher
- Scanner-Abschlusslogik beim Schließen der Scanner-Seite robuster gemacht
- Seitenvalidierung beim Beenden von Lesesessions berücksichtigt jetzt die Startseite der Session
- Buchabschluss beim Speichern bewertet den tatsächlichen Datenbankstatus und verhindert doppelte XP-Vergabe
- Beim Verlassen der Wishlist beim Speichern wird die Bereinigung anhand des tatsächlich persistierten Status entschieden
- Beim Bearbeiten von Wishlist-Büchern bleibt `WishlistInfo` erhalten, solange der Status nicht geändert wird
- Tippen auf Pflanzen im Bücherregal öffnet jetzt zuverlässig das Pflanzen-Modal; das Entfernen aus dem Regal wurde in das Detail-Modal verlegt

## [0.8.0] - 2026-04-07

### Geändert

- Pflanzen im Bücherregal öffnen ein Detail-Modal mit Namen, Level, nächstem Gießzeitpunkt und Gießbutton
- Pflanzennamen im Shop-Kaufdialog und im Pflanzen-Modal etwas größer dargestellt

### Behoben

- Plant-Shop-Karten zeigen bei nicht bezahlbaren Pflanzen nur noch den Preis, nicht mehr den Zusatz „Need X more"
- Pflanzenstatus und Gießlogik werden beim Laden und Interagieren zuverlässiger aktualisiert
- Drag-and-Drop-Einfügebalken im Bücherregal richtet sich jetzt an der Höhe des Ziel-Buchs aus statt über die gesamte Regalhöhe zu laufen

## [0.7.6] - 2026-04-02

### Hinzugefügt

- Cover-Bild aus Galerie wählen: Beim Bearbeiten oder Anlegen eines Buches kann das Cover direkt aus der Geräte-Galerie ausgewählt werden (Android). Das Bild wird sofort lokal gespeichert, sodass es auch bei einmaliger Berechtigungsvergabe erhalten bleibt.
- Android-Home-Screen-Widgets: „Aktuelles Buch" (Cover, Titel, Fortschritt), „Lese-Streak" (Streak-Tage mit Heute-Status) und „Lese-Ziel" (Fortschritt zum aktiven Leseziel). Automatische Aktualisierung alle 30 Minuten und sofort nach Änderungen in der App.
- Regalfarbe anpassbar: In den Settings können Bücherleisten und Regalleiste getrennt aus je 8 Holzfarb-Presets gewählt werden
- Android-Zurück-Button in die App eingebunden

### Behoben

- Buttons (z.B. „Add Shelf", „Backup to Cloud") blieben nach dem Tippen visuell hervorgehoben — Hover-Styles werden jetzt nur noch auf Geräten mit Maus/Trackpad angewendet

## [V0.7.5] - 2026-04-02

### Hinzugefügt

- Stats teilen (Reading Wrapped): Auf der Stats-Seite können Lesestats als PNG im Instagram-Story-Format (1080×1920) geteilt werden. Zeitraum wählbar: Week, Month, Quarter, Year, All Time. Die Karte zeigt abgeschlossene Bücher, gelesene Seiten, Lesezeit, Lieblingsgenre und Top-3-Bücher.
- Buchempfehlung teilen: Nach dem Abschließen eines Buchs erscheint in der Completion-Feier ein „Share as Recommendation"-Button. Die Share-Karte zeigt Titel, Autor, Cover, Seitenanzahl, Lesezeit, Gesamtbewertung mit Sternen und alle Einzelbewertungen mit farbigen Fortschrittsbalken.
- Share-Icon auf der Buchdetailseite neben „Book Information" für alle abgeschlossenen Bücher
- „HIGHLY RECOMMENDED"-Badge auf der Share-Karte ab einer Bewertung von 4.0

### Geändert

- Vereinfachter Review-Flow: zwei einfache Modals statt des bisherigen komplexen Flows — erst App-Feedback, dann optional der Sprung zur Play-Store-Bewertung
- Review-Dialog erscheint erst ab Level 7, höchstens zweimal pro Monat und kann über „Nicht mehr fragen" dauerhaft abgeschaltet werden
- Bei Buchabschluss wird direkt die Book-Completion-Feier angezeigt (ohne vorheriges Session-XP-Modal)

## [V0.7.4] - 2026-04-01

### Geändert

- „Wie Leveln Pflanzen?"-Absatz auf der Plant-Shop-Seite auf Englisch übersetzt
- Backup-Dateiname von `booklogger_backup_*.zip` auf `bookheart_backup_*.zip` umbenannt

### Behoben

- Untere Android-NavBar überdeckt die In-App-NavBar nicht mehr
- ISBN-Autofill nutzt automatisch einen anonymen Fallback ohne API-Key, wenn Google Books für den Projekt-Key eine Quota-/Rate-Limit-Fehlermeldung liefert

## [V0.7.3] - 2026-03-31

### Hinzugefügt

- Changelog-Datei
- Google Play In-App-Review-Integration: Review-Dialog nach Level-Up, Buch-Abschluss oder Leseziel-Erreichen (max. 2×/Monat, erst ab Level 6)

### Geändert

- Buy-me-a-Coffee-Unterstützung direkt im Backup-&-Restore-Bereich der Einstellungen
- Layout der Settings-Seite optimiert
- App-Symbol auf neues Design umgestellt

## [V0.6.3] - 2026-03-30

### Hinzugefügt

- Bodenfläche unter jedem Buch im Bücherregal (Issue #159)
- Bodenfläche für Pflanzen

### Geändert

- Benachrichtigungen verbessert

### Behoben

- Kamera- und Benachrichtigungs-Berechtigungsfehler behoben (PR #158)
- Zielfortschritts-Bug behoben

## [V0.6.2] - 2026-02-18

### Geändert

- Sicherheits- und Abhängigkeits-Updates (Dependabot)

## [V0.6.1] - 2026-02-18

Internes Wartungs-Release.

## [V0.6.0] - 2026-02-18

### Hinzugefügt

- Drag-and-Drop für Bücherregale (mit Long-Press-Geste und Auto-Scroll)
- Push-Benachrichtigungen
- Automatische Bildskalierung für große Buchcover
- Bücher können aus Zielen ausgeschlossen werden
- Ziele können auf bestimmte Genres oder Tropes beschränkt werden
- Timer-Hintergrundstatus konsistent über alle Timer-Komponenten

### Geändert

- App-Name zu **BookHeart** geändert
- Regal löschen verschiebt Bücher automatisch ins Hauptregal
- Scroll- und Drag-and-Drop-Performance verbessert

### Behoben

- ISBN- und Language-MaxLength-Fehler im Datenbankschema
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

- Natives MAUI-Barcode-Scanning (ZXing.Net.Maui.Controls)
- Benutzerdefinierter Farbwähler für Buchrücken (erweitertes Farbspektrum)
- Android-Zurück-Button-Support
- AOT-Kompilierung im Release-Modus (bessere Performance)
- Plattformspezifische async Einstellungen und Datei-Saver

## [V0.3.0] - 2025-12-04

### Hinzugefügt

- Google-Play-Store-Vorbereitungen
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
