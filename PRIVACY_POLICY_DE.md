# BookHeart – Datenschutzerklärung

**Zuletzt aktualisiert:** Februar 2026

## Überblick

BookHeart ist eine Offline-first-App zur Buchverwaltung. Ihre Daten werden ausschließlich lokal auf Ihrem Gerät gespeichert und zu keinem Zeitpunkt an unsere Server übermittelt. Wir erheben, speichern oder teilen keinerlei personenbezogene Daten.

## Datenspeicherung

Sämtliche Bücher, Lesesitzungen, Ziele, Pflanzen und Einstellungen werden in einer lokalen Datenbank (SQLite) auf Ihrem Gerät gespeichert. Diese Daten verlassen Ihr Gerät nicht, es sei denn, Sie nutzen ausdrücklich die Sicherungs- und Wiederherstellungsfunktion.

## Kameraberechtigung

BookHeart fordert den Zugriff auf die Kamera ausschließlich zum Scannen von Buch-Barcodes (ISBN) an. Es werden keine Fotos oder Videos aufgenommen, gespeichert oder übermittelt. Der Kamerazugriff ist optional und kann ohne Einschränkung der übrigen App-Funktionen verweigert werden.

## Netzwerknutzung

Bei der Suche nach einem Buch über die ISBN-Nummer sendet BookHeart diese an die Google Books API, um Buchmetadaten (Titel, Autor, Coverbild) abzurufen. Dabei werden keinerlei personenbezogene Daten oder Gerätekennungen übermittelt. Die App ist auch ohne diese Funktion vollständig offline nutzbar.

## Drittanbieterdienste

BookHeart nutzt folgende externe Dienste:

1. **Google Books API** (optional, nur bei ISBN-Suche) — Abruf von Metadaten (Titel, Autor, Cover). Keine personenbezogenen Daten werden übertragen.

2. **Firebase Analytics** (nur Android, standardmäßig AKTIVIERT, unter Settings → Datenschutz deaktivierbar) — anonyme Nutzungsstatistiken. Es werden keine Buchtitel, Autoren, ISBNs, Zitate, Annotationen, persönliche Notizen, exakte Daten oder exakte Werte übertragen, sondern nur grob zusammengefasste Kennzahlen wie „Anzahl Bücher (Bucket 6-20)". Die Android Advertising ID und die Android ID sind ausdrücklich deaktiviert.

3. **Firebase Crashlytics** (nur Android, standardmäßig AKTIVIERT, unter Settings → Datenschutz deaktivierbar) — anonyme Fehlerberichte (Stacktrace, Android-Version, Gerätemodell, App-Version). Keine personenbezogenen Daten werden übertragen.

**Datenverarbeitung durch Google:** Firebase-Dienste werden von Google LLC (Mountain View, CA, USA) betrieben. Die anonyme App-Instance-ID und grundlegende Geräteinformationen können an Google-Server in den USA übertragen werden. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (berechtigtes Interesse an App-Stabilität und -Verbesserung). Eine Einwilligung kann jederzeit mit Wirkung für die Zukunft durch die Schalter unter Settings → Datenschutz widerrufen werden. Beim Deaktivieren der Analytics wird die anonyme App-Instance-ID zusätzlich über `ResetAnalyticsData()` zurückgesetzt.

## Datenlöschung

Sie können Ihre gesamten Daten jederzeit über die Option „Alle Daten löschen" in den Einstellungen entfernen. Durch die Deinstallation der App werden ebenfalls sämtliche gespeicherten Daten von Ihrem Gerät gelöscht.

## Datenschutz für Kinder

BookHeart erhebt wissentlich keine Daten von Kindern. Die App erfordert weder die Erstellung eines Benutzerkontos noch die Angabe personenbezogener Informationen.

## Änderungen dieser Datenschutzerklärung

Änderungen an dieser Datenschutzerklärung werden im Rahmen von App-Updates veröffentlicht. Das Datum „Zuletzt aktualisiert" am Anfang dieses Dokuments gibt den Zeitpunkt der letzten Überarbeitung an.

## Kontakt

Bei Fragen zu dieser Datenschutzerklärung können Sie ein Issue in unserem GitHub-Repository eröffnen oder uns über den Play-Store-Eintrag kontaktieren.
