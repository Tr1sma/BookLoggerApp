# BookHeart – Privacy Policy

**Last updated:** April 2026

## Overview

BookHeart is an offline-first book management app. Your data is stored exclusively locally on your device and is never transmitted to our servers at any time. We do not collect, store, or share any personal data.

## Data Storage

All books, reading sessions, goals, plants, and settings are stored in a local database (SQLite) on your device. This data does not leave your device unless you explicitly use the backup and restore feature.

## Camera Permission

BookHeart requests access to the camera solely for scanning book barcodes (ISBN). No photos or videos are taken, stored, or transmitted. Camera access is optional and can be denied without affecting the rest of the app’s functionality.

## Network Usage

When searching for a book by ISBN, BookHeart sends the ISBN to the Google Books API in order to retrieve book metadata (title, author, cover image). No personal data or device identifiers are transmitted in this process. The app remains fully usable offline without this feature.

## Third-Party Services

BookHeart uses the following third-party services:

1. **Google Books API** (optional, only for ISBN lookup): The scanned or manually entered ISBN is sent to `googleapis.com/books/v1/volumes` in order to retrieve metadata (title, author, cover image). No personal data is transmitted.

2. **Firebase Analytics** (Android only, enabled by default, can be disabled at any time under Settings → Datenschutz): Sends anonymous usage statistics to Google in order to help us understand which features are being used. **Never** transmits book titles, authors, ISBNs, quotes, annotations, personal notes or any other personal data — only coarse, bucketed figures such as "book count (6-20)" or "current level (11-20)". The Android Advertising ID and Android ID are explicitly disabled in our Firebase configuration.

3. **Firebase Crashlytics** (Android only, enabled by default, can be disabled at any time under Settings → Datenschutz): Sends anonymous crash reports (stack trace, Android version, device model, app version) when the app crashes, so the bug can be fixed. No personal data is transmitted.

**Data processing by Google:** Firebase Analytics and Crashlytics are operated by Google LLC (1600 Amphitheatre Parkway, Mountain View, CA 94043, USA). An anonymous app instance ID and basic device information (Android version, device model, language, region) may be transmitted to servers in the USA. Legal basis: Art. 6(1)(f) GDPR (legitimate interest in app stability and improvement). You can withdraw your consent at any time with effect for the future by toggling the switches under Settings → Datenschutz. When disabled, the anonymous app instance ID is additionally reset via `ResetAnalyticsData()` so that previously collected data can no longer be tied to your device.

## Data Deletion

You can delete all your data at any time using the “Delete All Data” option in the settings. Uninstalling the app will also remove all stored data from your device.

## Privacy for Children

BookHeart does not knowingly collect data from children. The app does not require the creation of a user account or the submission of personal information.

## Changes to This Privacy Policy

Any changes to this privacy policy will be published as part of app updates. The “Last updated” date at the top of this document indicates when it was last revised.

## Contact

If you have any questions about this privacy policy, you can open an issue in our GitHub repository or contact us through the Play Store listing.

# BookHeart – Datenschutzerklärung

**Zuletzt aktualisiert:** April 2026

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

1. **Google Books API** (optional, nur bei ISBN-Suche): Die gescannte oder manuell eingegebene ISBN wird an `googleapis.com/books/v1/volumes` gesendet, um Metadaten (Titel, Autor, Cover) abzurufen. Keine personenbezogenen Daten werden übertragen.

2. **Firebase Analytics** (nur Android, standardmäßig AKTIVIERT, jederzeit unter Einstellungen → Datenschutz deaktivierbar): Sendet anonyme Nutzungsstatistiken an Google, damit wir verstehen, welche Funktionen genutzt werden. Es werden **keine** Buchtitel, Autoren, ISBNs, Zitate, Annotationen, persönliche Notizen oder andere personenbezogene Daten übertragen — nur grob zusammengefasste Kennzahlen wie „Anzahl Bücher (Bucket 6-20)" oder „aktuelles Level (Bucket 11-20)". Die Android Advertising ID und die Android ID sind in unserer Firebase-Konfiguration ausdrücklich deaktiviert.

3. **Firebase Crashlytics** (nur Android, standardmäßig AKTIVIERT, jederzeit unter Einstellungen → Datenschutz deaktivierbar): Sendet bei App-Abstürzen anonyme Fehlerberichte (Stacktrace, Android-Version, Gerätemodell, App-Version) an Google, damit der Fehler behoben werden kann. Keine personenbezogenen Daten werden übertragen.

**Datenverarbeitung durch Google:** Firebase Analytics und Crashlytics werden von Google LLC (1600 Amphitheatre Parkway, Mountain View, CA 94043, USA) betrieben. Dabei kann die anonyme App-Instance-ID sowie grundlegende Geräteinformationen (Android-Version, Gerätemodell, Sprache, Region) an Server in den USA übertragen werden. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (berechtigtes Interesse an App-Stabilität und -Verbesserung). Eine Einwilligung kann durch das Deaktivieren der Schalter unter Einstellungen → Datenschutz jederzeit mit Wirkung für die Zukunft widerrufen werden; bei Deaktivierung wird die anonyme App-Instance-ID zusätzlich über `ResetAnalyticsData()` zurückgesetzt, sodass bereits erhobene Daten nicht mehr mit deinem Gerät verknüpft werden können.

## Datenlöschung

Sie können Ihre gesamten Daten jederzeit über die Option „Alle Daten löschen" in den Einstellungen entfernen. Durch die Deinstallation der App werden ebenfalls sämtliche gespeicherten Daten von Ihrem Gerät gelöscht.

## Datenschutz für Kinder

BookHeart erhebt wissentlich keine Daten von Kindern. Die App erfordert weder die Erstellung eines Benutzerkontos noch die Angabe personenbezogener Informationen.

## Änderungen dieser Datenschutzerklärung

Änderungen an dieser Datenschutzerklärung werden im Rahmen von App-Updates veröffentlicht. Das Datum „Zuletzt aktualisiert" am Anfang dieses Dokuments gibt den Zeitpunkt der letzten Überarbeitung an.

## Kontakt

Bei Fragen zu dieser Datenschutzerklärung können Sie ein Issue in unserem GitHub-Repository eröffnen oder uns über den Play-Store-Eintrag kontaktieren.
