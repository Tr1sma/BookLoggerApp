# BookHeart - Play Store Release ToDo

## P0 - Blockierend (Play Store lehnt sonst ab)

### Datenschutzerklaerung / Privacy Policy
- [x] Privacy Policy Dokument erstellen (gehostet oder in-app)
  - Welche Daten gespeichert werden (lokal SQLite - kein Server)
  - Kamera-Nutzung (Barcode-Scanner)
  - Netzwerk-Nutzung (Google Books API fuer ISBN-Lookup)
  - Keine Weitergabe an Dritte
- [x] Link zur Privacy Policy in der Settings-Page einbauen
- [ ] Link im Play Store Listing hinterlegen

### Package Name aendern
- [ ] `com.companyname.bookloggerapp` aendern (z.B. `com.bookheart.app` oder `de.deinname.bookheart`)
- [ ] In `BookLoggerApp.csproj` unter `<ApplicationId>` anpassen

### Keystore & Signing
- [ ] Android Keystore generieren (`keytool -genkey ...`)
- [ ] Keystore sicher aufbewahren (Backup!)
- [ ] Signing-Konfiguration in `.csproj` oder Build-Pipeline einrichten
- [ ] AAB (Android App Bundle) Format konfigurieren

### App Store Listing Assets
- [ ] Hi-Res Icon erstellen (512x512)
- [ ] Feature Graphic erstellen (1024x500)
- [ ] Screenshots vorbereiten (min. 2, empfohlen 5-8)
- [ ] Kurzbeschreibung schreiben (max. 80 Zeichen)
- [ ] Langbeschreibung schreiben (max. 4000 Zeichen)
- [ ] App-Kategorie waehlen (Books & Reference)
- [ ] Content Rating Fragebogen ausfuellen
- [ ] Release Notes fuer Version 1.0 schreiben

---

## P1 - Wichtig (fuer User-Retention und Qualitaet)

### Onboarding / Tutorial Flow
- [ ] Willkommensscreen mit App-Vorstellung
- [ ] "Erstes Buch hinzufuegen" guided Tutorial
- [ ] Kurze Erklaerung des XP/Pflanzensystems
- [ ] Erklaerung des Rating-Systems (6 Kategorien)
- [ ] Navigation durch die Hauptbereiche zeigen
- [ ] Onboarding ueberspringbar machen

### Crash Reporting
- [ ] Firebase Crashlytics integrieren
- [ ] Firebase Analytics integrieren (Basis-Events)
- [ ] ANR-Monitoring aktivieren (Application Not Responding)
- [ ] Testen, dass Crashes korrekt gemeldet werden

### In-App Review Prompt
- [ ] Google Play In-App Review API integrieren
- [ ] Review-Dialog nach positiven Momenten anzeigen:
  - Nach einem Level-Up
  - Nach dem Abschliessen eines Buches
  - Nach dem Erreichen eines Leseziels
- [ ] Nicht zu oft anzeigen (max. 1x pro Monat)

---

## P2 - Empfohlen (verbessert Store-Bewertungen)

### Accessibility
- [ ] `aria-label` auf alle Icon-Buttons setzen
- [ ] Emoji-Only-Buttons mit Text ergaenzen
- [ ] Farbkontrast pruefen (WCAG AA: 4.5:1 Ratio)
- [ ] TalkBack-Kompatibilitaet testen (Android Screen Reader)
- [ ] Focus-Indikatoren fuer Keyboard-Navigation
- [ ] Schriftgroessen-Skalierung unterstuetzen

### Light Theme
- [ ] Light-Mode CSS-Variablen definieren
- [ ] Theme-Toggle in Settings funktionsfaehig machen (aktuell nur Dark)
- [ ] System-Theme Auto-Detection implementieren
- [ ] Alle Komponenten mit beiden Themes testen

### Benachrichtigungen / Reminders
- [ ] Taegliche Lese-Erinnerung implementieren (ReminderTime existiert in AppSettings)
- [ ] Notification-Permissions korrekt anfragen
- [ ] Notification-Channel fuer Android erstellen
- [ ] Erinnerung deaktivierbar machen

### Versionierung
- [ ] Semantic Versioning einfuehren (z.B. 1.0.0)
- [ ] Version-Management-System einrichten
- [ ] Changelog-Datei pflegen

---

## P3 - Nice-to-Have (hebt von Konkurrenz ab)

### Android Home Screen Widget
- [ ] Widget: Aktuelles Buch + Fortschritt anzeigen
- [ ] Widget: Lese-Streak anzeigen
- [ ] Widget: Taegliches Lese-Ziel Fortschritt
- [ ] Widget-Konfiguration (Groesse, Inhalt)

### Social / Sharing Features
- [ ] Buch-Empfehlungen teilen (Share-Intent mit Buch-Infos)
- [ ] "Mein Lesejahr"-Grafik als Bild exportieren (Instagram-Story-Format)
- [ ] Lesechallenge mit Freunden (optional)

### Erweiterte Statistiken
- [ ] Heatmap-Kalender (wie GitHub Contributions - welche Tage gelesen?)
- [ ] Lese-Geschwindigkeit Trend (Seiten/Stunde)
- [ ] Jahresvergleich (z.B. 2025 vs 2026)
- [ ] Jahresrueckblick / "Wrapped" Feature (a la Spotify Wrapped)

### Cloud Backup
- [ ] Google Drive Backup/Restore Integration
- [ ] Automatisches Backup konfigurierbar
- [ ] Restore-Flow mit Bestaetigung

### Buch-Empfehlungen
- [ ] "Aehnliche Buecher" basierend auf Genres/Tropes
- [ ] Kuratierte Listen (z.B. "Fantasy-Klassiker")
- [ ] Integration mit OpenLibrary oder Google Books API

---

## Technische Qualitaet

### Testing
- [ ] CI/CD auf .NET 10 aktualisieren (aktuell .NET 9.0.x)
- [ ] Auf mehreren Android-Geraeten testen (verschiedene API-Levels)
- [ ] Performance-Tests (grosse Bibliotheken mit 500+ Buechern)
- [ ] Edge-Cases testen (leere Datenbank, keine Internetverbindung)

### Sicherheit
- [ ] `debuggable=false` fuer Release-Builds sicherstellen
- [ ] Keine hardcodierten API-Keys
- [ ] ProGuard/R8 Konfiguration pruefen

---

## Prioritaeten-Uebersicht

| Prio | Feature                  | Aufwand | Impact | Status |
|------|--------------------------|---------|--------|--------|
| P0   | Privacy Policy           | Gering  | Pflicht| [ ]    |
| P0   | Package Name aendern     | Gering  | Pflicht| [ ]    |
| P0   | Keystore + Signing       | Gering  | Pflicht| [ ]    |
| P0   | Store Listing Assets     | Mittel  | Pflicht| [ ]    |
| P1   | Onboarding Flow          | Mittel  | Hoch   | [ ]    |
| P1   | Crash Reporting          | Gering  | Hoch   | [ ]    |
| P1   | In-App Review            | Gering  | Hoch   | [ ]    |
| P2   | Accessibility            | Mittel  | Mittel | [ ]    |
| P2   | Light Theme              | Mittel  | Mittel | [ ]    |
| P2   | Benachrichtigungen       | Mittel  | Mittel | [ ]    |
| P2   | Versionierung            | Gering  | Mittel | [ ]    |
| P3   | Widgets                  | Hoch    | Hoch   | [ ]    |
| P3   | Social Sharing           | Mittel  | Mittel | [ ]    |
| P3   | Erweiterte Stats         | Mittel  | Hoch   | [ ]    |
| P3   | Cloud Backup             | Hoch    | Mittel | [ ]    |
| P3   | Buch-Empfehlungen        | Mittel  | Mittel | [ ]    |
