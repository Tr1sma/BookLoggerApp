# Changelog

Alle nennenswerten Änderungen an BookHeart werden in dieser Datei festgehalten.

Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

Versionsschema:

- `V0.x.y` – Pre-Release (vor Play-Store-Veröffentlichung)
- `V1.0.0` – Erster öffentlicher Play-Store-Release
- MAJOR wird auf 1 gesetzt wenn der erste public Play-Store-Upload erfolgt
- MINOR für neue Features, PATCH für Bugfixes und kleinere Änderungen

## [Unveröffentlicht]

### Behoben
- Promo-Code-Meldungen, Export-Teilen-Titel und Share-Card-Texte (Statistik & Buch) erscheinen jetzt in der App-Sprache statt fest auf Englisch
- Lesezeit-Anzeige auf Dashboard & Statistik nutzt die lokalisierte Schreibweise (vorher fest „Xh Ym")

## [V1.0.0]

Erster offizieller Play-Store-Release. 🎉

### Behoben
- Münzen/XP/Level gehen bei gleichzeitigen Aktionen nicht mehr verloren
- Bücher speichern zuverlässig komplett
- Eingaben überall geprüft (kein Buch ohne Titel, keine unsinnigen Werte)
- Statistik-Tabs „Trends"/„Analysen" laden zuverlässig
- Streak, Statistiken, Lesezeiten & Ziele rechnen nach lokaler Uhrzeit statt UTC
- XP-Anzeige im Timer stimmt mit Gutschrift überein
- Sitzungen ohne Lesezeit verfälschen Statistik nicht mehr
- Neue Bücher landen am Regal-Ende
- Promo-Codes bleiben gültig
- Abgelaufene/aktive Abos werden korrekt behandelt
- Käufe korrekt bestätigt, keine Auto-Erstattung mehr
- Tarifwechsel über normalen Upgrade-Flow
- Fehler statt falscher „Freigeschaltet"-Feier bei Kaufproblemen
- Doppel-Tipp auf „Kaufen" startet nur einen Kauf
- „Erster Monat"-Hinweis nur bei echtem Einführungsangebot
- Backup-Wiederherstellung absturzsicher (Sicherungskopie + Rollback)
- JSON-Import überspringt nur fehlerhafte Bücher statt komplett abzubrechen
- App-Neustart friert Oberfläche nicht mehr ein
- Schnelles Weiternavigieren bricht Ladevorgang sauber ab
- Sterne-Bewertung per Tastatur & Screenreader bedienbar
- Widgets & Paywall-Texte in App-Sprache
- ISBN-Treffer in Wunschliste nicht mehr fälschlich als Fehler

### Sicherheit
- Bezahl-Funktionen fest abgesichert, nicht mehr umgehbar
- Nach Abo-Ablauf/Herabstufung bleiben Bezahl-Inhalte verborgen (nicht gelöscht)
- Backup kann keine Bezahl-Inhalte für Free freischalten
- Statistiken & Absturzberichte standardmäßig AUS bis zur Zustimmung
- Web-Inhalt erhält nur Kamera-Zugriff (Barcode-Scanner)
- Manipulierte Backups können Speicher nicht mehr volllaufen lassen
- Buchcover nur von öffentlichen Web-Adressen
- CSV-Export gegen Formel-Einschleusung abgesichert

## [0.12.0]

### Hinzugefügt
- Stimmungen & Trigger pro Lesesitzung (1–3 Emojis), „Emotionale Reise"-Diagramm auf Buchdetailseite, in Einstellungen abschaltbar

## [0.11.8]

### Behoben
- Feature-Vorschlag per E-Mail auf Android 11+ wieder möglich

## [0.11.7]

### Hinzugefügt
- Inline-Lese-Timer: Lesezeit manuell eingeben (z. B. `45:30`)
- Predictive-Burndown-Chart (Buchdetail) + Dashboard-Übersicht „voraussichtlich als Nächstes fertig"

## [0.11.6]

### Hinzugefügt
- Buch per Titel suchen & Formular automatisch ausfüllen
- „Blind Date mit einem Buch": Roulette über ungelesene Bücher nach Vibes
- Live-Lese-Timer als dauerhafte Benachrichtigung (Sperrbildschirm/Statusleiste), abschaltbar

### Behoben
- Feature-Vorschlag per E-Mail auf Android 11+ wieder möglich

## [0.11.5]

### Behoben
- Ziel-Löschen aus dem Bearbeitungs-Modal funktioniert wieder
- DE-Begriff „Lesesitzung" statt „Lesesession" in Onboarding-Mission

## [0.11.4]

### Behoben
- „Als Nächstes"-Missionstext lokalisiert
- Navbar-Eintrag „Dashboard" statt „Übersicht"

## [0.11.3]

### Geändert
- Schnellerer Start: veraltete Legacy-DB-Migration vom Startpfad entfernt

### Behoben
- Wunschlisten-Prioritäten in aktiver App-Sprache
- Schnell-Hinzufügen/Wishlist-Modal: Autor jetzt Pflichtfeld

## [0.11.2]

### Hinzugefügt
- Missions- & Feature-Atlas-Texte auf „Einstieg"-Seite vollständig lokalisiert

### Geändert
- Bewertungskategorien lokalisiert (Buchdetails & Statistiken)

### Behoben
- Mission „Buch vollständig bewerten" prüft jetzt die genre-abhängigen Kategorien

## [0.11.1]

### Hinzugefügt
- Deutsche Sprachunterstützung + Sprachauswahl in Einstellungen
- Komplette UI übersetzt (Pages, Components, Fehlermeldungen, Validierung)
- Android-Widget-Strings zweisprachig
- Notifications in aktiver UI-Sprache

### Geändert
- Sprache beim ersten Start aus System-Sprache abgeleitet
- Backup-Restore synchronisiert Sprach-Preference
- `SchemaDriftGuard` repariert auch `UserEntitlements`-Spalten/Tabelle

### Behoben
- SQLite-Fehler „no such column: u.IsHiddenByEntitlement" behoben
- Books-Seite zeigt nach Lade-Fehler Fehlermeldung statt leerer Liste

## [0.10.6]

### Hinzugefügt
- Konfetti-Feier nach Promo-Code-Einlösung & Abo-Abschluss
- Telemetrie für DB-Initialisierung
- DB-Init-Log in „Data Recovery Diagnostics"

### Geändert
- DB-Init auf dedizierten Hintergrund-Thread (langsame Geräte)
- DB-Init-Timeout 45 s → 20 s
- Retry-Button startet immer frischen Versuch, idempotent

### Behoben
- Goals nicht mehr über 100 % abschließbar
- „Loading..."-Zustand löst sich zuverlässig auf
- Retry-Schleife nach DB-Timeout behoben

## [0.10.5]

### Hinzugefügt
- Dezenter, nicht-blockierender Datenschutz-Banner nach Onboarding
- Datenschutzerklärung um Firebase-Abschnitt erweitert
- Changelogs überarbeitet
- Geräte-ID-Reset beim Deaktivieren der Statistiken

## [0.10.4]

### Hinzugefügt
- Promo-Code-Feld in Paywall (Prefix `BH-`)
- Firebase Analytics & Crashlytics (anonym, keine persönlichen Daten)
- Bereich „🔒 Datenschutz" mit Toggles

---
## [0.10.3]

### Hinzugefügt
- Premium-Abo-System mit zwei Tiers: **Plus** & **Premium** (Free bleibt voll nutzbar)
- Paywall-Modal mit Feature-Vergleichstabelle und Preisbuttons

### Behoben
- Release-Crash beim Start (`Crashlytics build ID is missing`) behoben

## [0.9.6]

### Behoben
- Onboarding-/Update-Overlays bei 3-Tasten-Navigation nicht mehr verdeckt
- Android-Widget zeigt Ziel-Fortschritt in lokaler Zeitzone
- Gieß-Benachrichtigungen berücksichtigen Herz-der-Geschichten-Bonus
- Buch-Fortschritt auf 0–100 % begrenzt
- Mehrfach-Tipp auf „Abschließen" vergibt keine doppelte XP
- Plant-Widget berechnet „Tage bis Level-Up" korrekt
- Lese-Ziele nutzen lokale Zeitzone
- Käufe erstatten Münzen bei Speicher-Fehler zurück
- „Alle Daten löschen" entfernt auch Dekorationen
- Level-Up-Celebration auch bei First-of-Day-Bonus
- Status- + Genre-Wechsel ordnet Buch korrekt zu
- XP mit Pflanzen-Boost rundet korrekt
- Keine überlappenden Session-Celebration-Overlays
- Abgebrochene Imports räumen Zwischendateien auf
- Shop-Sortierung bleibt nach Reload konsistent
- Import mit zukünftigem Abschluss-Datum abgewiesen

## [0.9.4]

### Hinzugefügt
- Prestige-Pflanze **Chronikbaum** (Streak-Wächter)
- Prestige-Pflanze **Ewiger Phönix-Bonsai** (Phönix-Schutz)
- Ultimate-Dekoration **Herz der Geschichten** (globale Boni)
- Legendary-Visuals im Shop
- Shop-Detail zeigt Spezialfähigkeit vor Kauf

### Geändert
- Session-Celebration hebt Streak-Rettung & Herz-Boni hervor

### Behoben
- Plant-Boost: UI-Anzeige = XP-Gewährung
- Pflanzen-Level-Up-Münzen erst nach Speichern
- Wochenstatistik beginnt Montag

## [0.9.3]

### Geändert
- Coin-Belohnung pro Level-Up progressiv (50×Level + 3×Level²)
- Bessere Pflanzen geben höheren XP-Boost
- Kleine UI/UX-Updates

## [0.9.2]

### Hinzugefügt
- Erweiterte Statistiken: 3-Tab-System (Übersicht | Trends | Analysen)
- Genre-spezifische Bewertungskategorien (5 neue)

### Geändert
- XP-Boosts höherstufiger Pflanzen erhöht
- Shop-Seite kompakter (2-Spalten-Raster)
- Settings-Seite aufgeräumt
- Stats-Seite kompakter
- „Getting Started" auf kleinen Displays kompakter

### Behoben
- Changelog nach Update wieder zuverlässig angezeigt

## [0.9.0]

### Hinzugefügt
- Breite Regalgegenstände (mehrere Slots)
- Neue Regaldekorationen im Shop
- Shop-Tabs für Pflanzen & Dekorationen
- Versioniertes Onboarding mit „Getting Started"-Hub
- „Getting Started"-CTA auf Regal & Dashboard

### Geändert
- 11/14 Dekorationen jetzt 2 Slots breit
- Onboarding-Overlay kompakter & dunkler

### Behoben
- Tote Pflanzen vollständig entfernt
- Kein Dashboard-Crash nach Restore bei fehlender Pflanzenspezie
- Division durch null bei Pflanzenverstärkung behoben
- Korrupte Cloud-Backups werden abgelehnt
- Auto-Neustart nach Restore zuverlässig (Android 12+)
- Kein „database disk image malformed" nach Restore
- Erstinstallations-Race beim ersten Restore behoben

## [0.8.2] - 2026-04-07

### Hinzugefügt
- Eigene Streak-Feier mit skaliertem XP-Bonus

### Behoben
- Kein Stats-Crash bei toten Pflanzen ohne Boost
- Globale Fehleransicht nach Seitenwechsel zurückgesetzt

## [0.8.1] - 2026-04-07

### Hinzugefügt
- Pflanzen im Regal per Stift-Icon umbenennen
- Play-Store-Update-Check + Changelog nach Update

### Geändert
- README-Featureübersicht aktualisiert
- Pflanzen-Detail-Modal kompakter

### Behoben
- Razor-/Restore-Fehler auf Windows behoben
- App-Icon ohne monochrome Variante
- Backup-Restore findet Dateien bei abweichender Groß-/Kleinschreibung
- Buch-Start idempotent (`DateStarted` bleibt)
- Reading-Timer threadsicher
- Scanner-Abschlusslogik robuster
- Seitenvalidierung berücksichtigt Startseite
- Buchabschluss verhindert doppelte XP
- Wishlist-Bereinigung anhand persistierter Status
- `WishlistInfo` bleibt bei unverändertem Status erhalten
- Tippen auf Pflanzen öffnet zuverlässig das Modal

## [0.8.0] - 2026-04-07

### Geändert
- Pflanzen-Detail-Modal (Name, Level, Gießzeitpunkt, Gießbutton)
- Pflanzennamen größer dargestellt

### Behoben
- Plant-Shop-Karten ohne „Need X more"
- Pflanzenstatus & Gießlogik zuverlässiger
- Drag-and-Drop-Einfügebalken an Ziel-Buch-Höhe ausgerichtet

## [0.7.6] - 2026-04-02

### Hinzugefügt
- Cover-Bild aus Galerie wählen (Android)
- Android-Home-Screen-Widgets (aktuelles Buch, Streak, Leseziel)
- Regalfarbe anpassbar (8 Holzfarb-Presets)
- Android-Zurück-Button eingebunden

### Behoben
- Buttons bleiben nach Tippen nicht mehr hervorgehoben

## [V0.7.5] - 2026-04-02

### Hinzugefügt
- Stats teilen (Reading Wrapped) als PNG im Story-Format
- Buchempfehlung teilen nach Abschluss
- Share-Icon auf Buchdetailseite
- „HIGHLY RECOMMENDED"-Badge ab Bewertung 4.0

### Geändert
- Vereinfachter Review-Flow (zwei Modals)
- Review-Dialog ab Level 7, max. 2×/Monat, abschaltbar
- Book-Completion-Feier direkt bei Abschluss

## [V0.7.4] - 2026-04-01

### Geändert
- „Wie Leveln Pflanzen?"-Absatz auf Englisch
- Backup-Dateiname → `bookheart_backup_*.zip`

### Behoben
- Android-NavBar überdeckt In-App-NavBar nicht mehr
- ISBN-Autofill nutzt anonymen Fallback bei Quota-Fehler

## [V0.7.3] - 2026-03-31

### Hinzugefügt
- Changelog-Datei
- Google Play In-App-Review-Integration

### Geändert
- Buy-me-a-Coffee im Backup-Bereich
- Settings-Layout optimiert
- App-Symbol neu

## [V0.6.3] - 2026-03-30

### Hinzugefügt
- Bodenfläche unter Büchern & Pflanzen im Regal

### Geändert
- Benachrichtigungen verbessert

### Behoben
- Kamera-/Benachrichtigungs-Berechtigungsfehler
- Zielfortschritts-Bug

## [V0.6.2] - 2026-02-18

### Geändert
- Sicherheits- & Abhängigkeits-Updates (Dependabot)

## [V0.6.1] - 2026-02-18

Internes Wartungs-Release.

## [V0.6.0] - 2026-02-18

### Hinzugefügt
- Drag-and-Drop für Bücherregale
- Push-Benachrichtigungen
- Automatische Bildskalierung für große Cover
- Bücher aus Zielen ausschließbar
- Ziele auf Genres/Tropes beschränkbar
- Timer-Hintergrundstatus konsistent

### Geändert
- App-Name zu **BookHeart**
- Regal löschen verschiebt Bücher ins Hauptregal
- Scroll-/Drag-Performance verbessert

### Behoben
- ISBN-/Language-MaxLength-Fehler im Schema
- Division durch Null in StatsService

## [V0.5.4] - 2026-02-13

### Hinzugefügt
- Mehrkategorien-Bewertungssystem
- StatsViewModel mit Lesestatistiken
- Nutzerfortschritts-Tracking + Import/Export

### Geändert
- Buchsuche verbessert
- Backup-Wiederherstellung zuverlässiger

## [V0.5.1] - 2026-01-14

### Hinzugefügt
- Dependabot-Konfiguration
- Auto-Sortierung für spezielle Regale

### Sicherheit
- **[HOCH]** Zip-Bomb-Schwachstelle behoben

## [V0.5.0] - 2026-01-13

### Hinzugefügt
- Tropes/Subgenre-Tagging
- Lazy Loading für Buchcover
- Cloud-Backup
- Debounced-Suche mit Genre/Trope

### Geändert
- Gamification rebalanciert
- StatsService-Abfragen optimiert

### Sicherheit
- **[HOCH]** Zip-Slip-Schwachstelle behoben
- URL-Parameter-Injection behoben
- Sicherheitsaudit für Image-Download

## [V0.4.0] - 2026-01-06

### Hinzugefügt
- Natives MAUI-Barcode-Scanning
- Farbwähler für Buchrücken
- Android-Zurück-Button-Support
- AOT-Kompilierung im Release
- Plattformspezifische async Einstellungen & Datei-Saver

## [V0.3.0] - 2025-12-04

### Hinzugefügt
- Google-Play-Store-Vorbereitungen
- Ziel-Events & Datenlöschung

### Geändert
- Pflanzen-Leveling auf Lesetage
- Mobile UX & Berechtigungen verbessert

### Behoben
- Race Condition bei Münz-Updates

## [V0.2.0] - 2025-11-07

### Geändert
- Migration auf .NET 10

## [V0.1.0] - 2025-11-04

### Hinzugefügt
- Initiale Veröffentlichung
- Bücher verwalten (hinzufügen, bearbeiten, löschen, Cover)
- Leseziel-Tracking
- Gamification: XP, Level, Pflanzen, Shop, Coins, Streaks
- SQLite-Datenbank mit EF Core
- Android-App (MAUI Blazor Hybrid)
