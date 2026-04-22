# BookHeart - Privacy Policy

**Last updated: February 2026**

## Overview

BookHeart is an offline-first book tracking app. Your data is stored locally on your device and is never transmitted to our servers. We do not collect, store, or share any personal information.

## Data Storage

All your books, reading sessions, goals, plants, and settings are stored in a local database (SQLite) on your device. This data never leaves your device unless you explicitly use the Backup & Restore feature.

## Camera Permission

BookHeart requests camera access solely for scanning book barcodes (ISBN). No photos or videos are captured, stored, or transmitted. Camera access is optional and can be denied without affecting other app features.

## Network Usage

When you look up a book by ISBN, BookHeart sends the ISBN number to the Google Books API to retrieve book metadata (title, author, cover image). No personal data or device identifiers are included in these requests. The app functions fully offline without this feature.

## Third-Party Services

BookHeart uses the following third-party services:

1. **Google Books API** (optional, ISBN lookup only) — metadata retrieval (title, author, cover). No personal data transmitted.

2. **Firebase Analytics** (Android only, enabled by default, can be disabled under Settings → Datenschutz) — anonymous usage statistics. Never transmits book titles, authors, ISBNs, quotes, annotations, personal notes, exact dates or exact values; only bucketed figures like "book count (6-20)". Android Advertising ID and Android ID are explicitly disabled.

3. **Firebase Crashlytics** (Android only, enabled by default, can be disabled under Settings → Datenschutz) — anonymous crash reports (stack trace, Android version, device model, app version). No personal data is transmitted.

**Data processing by Google:** Firebase services are operated by Google LLC (Mountain View, CA, USA). An anonymous app-instance ID and basic device info may be transmitted to Google servers in the USA. Legal basis: Art. 6(1)(f) GDPR (legitimate interest in app stability and improvement). Consent can be withdrawn at any time with effect for the future by toggling the switches under Settings → Datenschutz. Disabling analytics additionally resets the anonymous app-instance ID via `ResetAnalyticsData()`.

## Data Deletion

You can delete all your data at any time using the "Delete All Data" option in Settings. Uninstalling the app also removes all stored data from your device.

## Children's Privacy

BookHeart does not knowingly collect any information from children. The app does not require account creation or any personal information to function.

## Changes to This Policy

Any changes to this privacy policy will be reflected in app updates. The "Last updated" date at the top indicates the most recent revision.

## Contact

If you have questions about this privacy policy, please open an issue on our GitHub repository or contact us via the Play Store listing.
