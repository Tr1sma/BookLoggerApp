# Datenschutzerklärung für Book Logger

**Zuletzt aktualisiert:** 4. Dezember 2024

## Einleitung

Book Logger („wir", „unsere" oder „die App") ist eine mobile Anwendung, die Ihnen hilft, Ihren Lesefortschritt zu verfolgen, Ihre Buchsammlung zu verwalten und Ihre Leseziele zu erreichen. Diese Datenschutzerklärung erläutert, wie wir Ihre Daten erfassen, verwenden und schützen.

## Verantwortlicher

Diese App wird von einem unabhängigen Entwickler entwickelt und gepflegt. Bei datenschutzbezogenen Anfragen kontaktieren Sie uns bitte über die App-Store-Seite.

## Welche Daten wir erfassen

### Von Ihnen bereitgestellte Daten

Die App speichert folgende Informationen, die Sie eingeben:

- **Buchinformationen:** Titel, Autor, ISBN, Verlag, Erscheinungsjahr, Seitenzahl, Cover-Bilder, Genres und Beschreibungen
- **Lesefortschritt:** Aktuelle Seite, Lesesitzungen (Datum, Dauer, gelesene Seiten)
- **Persönliche Notizen:** Zitate, Anmerkungen und Buchbewertungen
- **Leseziele:** Ihre persönlichen Leseziele und deren Fortschritt
- **Gamification-Daten:** Virtuelle Pflanzen, Erfahrungspunkte, Level und durch Lesen verdiente Münzen

### Automatisch erfasste Daten

- **Nutzungsstatistiken:** Leseserien, gelesene Gesamtseiten und Lesezeit (lokal berechnet)

### Daten, die NICHT erfasst werden

Wir erfassen **KEINE**:
- Persönlichen Identifikationsdaten (Name, E-Mail, Telefonnummer)
- Standortdaten
- Geräte-IDs
- Analyse- oder Tracking-Daten
- Absturzberichte an externe Server

## Wie wir Ihre Daten verwenden

Alle Daten werden ausschließlich verwendet für:
- Anzeige Ihrer Buchsammlung und Ihres Lesefortschritts
- Verfolgung und Visualisierung Ihrer Lesestatistiken
- Verwaltung Ihrer Leseziele
- Bereitstellung der Gamification-Funktionen (Pflanzen, XP, Level)

## Datenspeicherung

### Nur lokale Speicherung

**Alle Ihre Daten werden lokal auf Ihrem Gerät gespeichert.** Wir verwenden eine SQLite-Datenbank, die im privaten Anwendungsverzeichnis Ihres Geräts gespeichert wird. Das bedeutet:

- Ihre Daten verlassen niemals Ihr Gerät (außer bei Google Books API-Anfragen)
- Wir haben keinen Zugriff auf Ihre Daten
- Wir betreiben keine Server, die Ihre Informationen speichern
- Das Deinstallieren der App löscht alle lokal gespeicherten Daten

### Datenexport

Sie können Ihre Daten jederzeit im JSON- oder CSV-Format über die App-Einstellungen exportieren. Dies ermöglicht Ihnen:
- Persönliche Backups zu erstellen
- Daten auf andere Geräte zu übertragen
- Alle gespeicherten Informationen zu überprüfen

## Drittanbieterdienste

### Google Books API

Wenn Sie die ISBN-Suchfunktion verwenden, fragt die App die Google Books API ab, um Buchmetadaten (Titel, Autor, Beschreibung, Cover-Bild usw.) abzurufen.

- **Gesendete Daten:** Nur die ISBN-Nummer, die Sie scannen oder eingeben
- **Empfangene Daten:** Öffentliche Buchinformationen aus Googles Datenbank
- **Googles Datenschutzerklärung:** https://policies.google.com/privacy

Keine persönlichen Daten oder Lesegewohnheiten werden durch diese Funktion mit Google geteilt.

## Berechtigungen

Die App fordert folgende Berechtigungen an:

| Berechtigung | Zweck |
|--------------|-------|
| **Internet** | Erforderlich für ISBN-Suche via Google Books API |
| **Kamera** | Wird zum Scannen von ISBN-Barcodes verwendet (optionale Funktion) |
| **Taschenlampe** | Wird zur Beleuchtung von Barcodes bei schlechtem Licht verwendet (optional) |

Sie können die Kameraberechtigung verweigern und die App dennoch nutzen, indem Sie ISBNs manuell eingeben.

## Datenspeicherung

Ihre Daten werden auf Ihrem Gerät aufbewahrt, bis Sie:
- Einzelne Bücher oder Lesesitzungen löschen
- Die Funktion "Alle Daten löschen" in den Einstellungen verwenden
- Die Anwendung deinstallieren

## Ihre Rechte

Sie haben das Recht auf:

- **Auskunft:** Alle Ihre Daten innerhalb der App einsehen
- **Export:** Ihre Daten im JSON- oder CSV-Format herunterladen
- **Löschung:** Einzelne Einträge oder alle Daten auf einmal entfernen
- **Datenübertragbarkeit:** Ihre Daten exportieren und anderweitig verwenden

### So löschen Sie Ihre Daten

1. Öffnen Sie die App
2. Navigieren Sie zu **Einstellungen**
3. Scrollen Sie zu **Danger Zone**
4. Tippen Sie auf **Delete All Data**
5. Geben Sie "DELETE" zur Bestätigung ein

Dies entfernt dauerhaft alle Bücher, Lesesitzungen, Ziele, Pflanzen und Fortschritte.

## Datenschutz für Kinder

Book Logger sammelt wissentlich keine Informationen von Kindern unter 13 Jahren. Die App erfordert keine Kontoerstellung oder persönliche Informationen zur Nutzung.

## Sicherheit

Wir implementieren angemessene Sicherheitsmaßnahmen:
- Alle Daten werden im privaten Verzeichnis der App gespeichert, unzugänglich für andere Apps
- Keine Datenübertragung an externe Server (außer Google Books API)
- Keine Benutzerkonten oder Authentifizierung erforderlich

## Änderungen dieser Richtlinie

Wir können diese Datenschutzerklärung von Zeit zu Zeit aktualisieren. Änderungen werden im Datum "Zuletzt aktualisiert" am Anfang dieses Dokuments widergespiegelt. Die fortgesetzte Nutzung der App nach Änderungen gilt als Zustimmung zur aktualisierten Richtlinie.

## Open Source

Die Datenverarbeitung der App kann durch den Quellcode überprüft werden. Es ist keine versteckte Datenerfassung oder Tracking implementiert.

## Kontakt

Bei Fragen zu dieser Datenschutzerklärung oder den Datenpraktiken der App kontaktieren Sie uns bitte über:
- Den Entwicklerkontakt im App-Store-Eintrag
- GitHub Issues (falls zutreffend)

## Zusammenfassung

- **Alle Daten bleiben auf Ihrem Gerät**
- **Keine Konten erforderlich**
- **Keine Analyse oder Tracking**
- **Keine Daten werden verkauft oder geteilt**
- **Vollständiger Datenexport und Löschung verfügbar**
- **Nur Google Books API wird kontaktiert (nur für ISBN-Suche)**

---

*Diese Datenschutzerklärung ist gültig ab dem 4. Dezember 2024.*
