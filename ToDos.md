# BookHeart – GitHub Issues / Todos

---

## 🏗️ Infrastruktur & Architektur

---

### Issue #1: Premium-/Free-Tier Architektur aufsetzen

**Labels:** `architecture`, `monetization`, `priority: high`

**Beschreibung:**
Es wird ein zentrales System benötigt, das app-weit den Abo-Status des Nutzers verwaltet und Features entsprechend freischaltet oder sperrt. Dieses System ist die Grundlage für alle weiteren Monetarisierungs-Features.

**Aufgaben:**
- [ ] `UserTier` Enum erstellen (`Free`, `Premium`)
- [ ] `ISubscriptionService` Interface definieren mit Methoden:
  - `Task<UserTier> GetCurrentTierAsync()`
  - `Task<bool> HasFeatureAccessAsync(FeatureFlag flag)`
  - `Task RestorePurchasesAsync()`
- [ ] `FeatureFlag` Enum für alle gated Features:
  - `UnlimitedShelves`, `AdvancedStatistics`, `ExportFunctions`, `CustomThemes`, `AiRecommendations`, `AdFree`
- [ ] Konkrete `SubscriptionService` Implementierung mit lokalem Caching des Abo-Status (SQLite)
- [ ] DI-Registration im `MauiProgram.cs`
- [ ] Unit Tests für Tier-Auflösung und Feature-Gating

**Akzeptanzkriterien:**
- Jede Page/Component kann per DI den `ISubscriptionService` injecten und prüfen, ob ein Feature verfügbar ist
- Abo-Status wird lokal gecacht und überlebt App-Neustarts
- Fallback auf `Free` wenn kein Status ermittelbar

---

### Issue #2: In-App Purchase Integration (Google Play Billing)

**Labels:** `monetization`, `platform: android`, `priority: high`

**Beschreibung:**
Integration der Google Play Billing Library für den Kauf von Premium-Abonnements. Nutzer sollen ein monatliches (3,99€) oder jährliches (29,99€) Abo abschließen können.

**Aufgaben:**
- [ ] NuGet-Paket `Plugin.InAppBilling` einbinden (oder direktes Android Billing via Platform-Code)
- [ ] Produkt-IDs in Google Play Console anlegen:
  - `premium_monthly` (3,99€/Monat)
  - `premium_yearly` (29,99€/Jahr)
- [ ] `IBillingService` Interface:
  - `Task<IEnumerable<ProductInfo>> GetProductsAsync()`
  - `Task<PurchaseResult> PurchaseAsync(string productId)`
  - `Task<bool> RestorePurchasesAsync()`
  - `Task<SubscriptionStatus> GetSubscriptionStatusAsync()`
- [ ] Plattform-spezifische Implementierung unter `Platforms/Android/`
- [ ] Purchase-Validierung (Receipt Verification) – mindestens client-seitig, idealerweise mit einem simplen Backend-Check
- [ ] Abo-Status in `SubscriptionService` (Issue #1) einbinden
- [ ] Handling von Edge Cases:
  - Abo läuft aus → Features sperren, Daten behalten
  - Abo-Wechsel (monatlich ↔ jährlich)
  - Restore nach Neuinstallation
- [ ] Unit Tests für Purchase-Flow (mit gemocktem Billing)

**Akzeptanzkriterien:**
- Nutzer kann aus der App heraus Premium kaufen (Monats- oder Jahresabo)
- Abo-Status wird korrekt erkannt und cached
- Restore Purchases funktioniert nach Neuinstallation
- Wenn Abo ausläuft, werden Premium-Features gesperrt, aber keine Daten gelöscht

---

### Issue #3: Werbe-Integration (AdMob)

**Labels:** `monetization`, `platform: android`, `priority: high`

**Beschreibung:**
Free-Nutzer sehen Werbung. Premium-Nutzer sehen keine Werbung. Es werden Banner-Ads und optional Interstitial-Ads eingebunden.

**Aufgaben:**
- [ ] Google AdMob SDK einbinden (via `GoogleMobileAds` NuGet oder manuell)
- [ ] AdMob App-ID und Ad-Unit-IDs in Google AdMob Console anlegen
- [ ] `IAdService` Interface:
  - `Task InitializeAsync()`
  - `Task<bool> ShouldShowAdsAsync()` (prüft Tier via `ISubscriptionService`)
  - `void ShowBanner(string adUnitId)`
  - `Task ShowInterstitialAsync()` (nach Session-Ende o.ä.)
  - `void HideBanner()`
- [ ] Blazor-Komponente `<AdBanner />`:
  - Rendert nichts wenn Premium
  - Zeigt Banner-Ad wenn Free
  - Platzierung: Bottom der Hauptseiten (Bibliothek, Statistiken, Dashboard)
- [ ] Interstitial-Ads:
  - Nach Abschluss einer Lese-Session (max 1x pro 3 Sessions, nicht nerven)
  - Beim Wechsel zwischen Hauptbereichen (max 1x pro 10 Minuten)
- [ ] Test-Ads im Debug-Modus verwenden
- [ ] GDPR/Consent-Dialog für EU-Nutzer (Google UMP SDK)

**Akzeptanzkriterien:**
- Free-Nutzer sehen Banner-Ads auf Hauptseiten
- Premium-Nutzer sehen keine Werbung
- Interstitial-Ads erscheinen nicht übermäßig häufig (Rate-Limiting)
- Consent-Dialog wird beim ersten Start angezeigt (EU)
- Test-Ads im Debug, echte Ads nur im Release

---

### Issue #4: Paywall / Upgrade-Screen UI

**Labels:** `ui`, `monetization`, `priority: high`

**Beschreibung:**
Ein ansprechender Upgrade-Screen, der erscheint wenn Free-Nutzer auf ein Premium-Feature tippen. Muss die Vorteile klar kommunizieren und den Kauf-Flow starten.

**Aufgaben:**
- [ ] `PaywallPage.razor` / `PaywallModal.razor` erstellen
- [ ] Inhalte:
  - Feature-Übersicht mit Icons (keine Werbung, AI-Empfehlungen, erweiterte Statistiken, unbegrenzte Regale, Themes, Export)
  - Preis-Toggle: Monatlich (3,99€) / Jährlich (29,99€ – "Spare 35%")
  - CTA-Button "Premium freischalten"
  - "Käufe wiederherstellen" Link
  - Dezenter Hinweis auf Abo-Bedingungen (Verlängerung, Kündigung)
- [ ] Triggerpunkte definieren – Paywall öffnet sich wenn Free-User:
  - 4. Regal erstellen will
  - Erweiterte Statistiken öffnet
  - Export-Funktion nutzt
  - AI-Empfehlungen öffnet
  - Theme wechseln will
- [ ] Soft-Paywall Variante: Feature kurz anteasern ("Hier wäre deine Genre-Analyse...") mit Blur/Overlay + Upgrade-Button
- [ ] Animation / Transition zum Paywall (kein harter Block)
- [ ] Tracking: Welche Paywall-Triggerpunkte konvertieren am besten (lokales Event-Logging)

**Akzeptanzkriterien:**
- Paywall erscheint kontextbezogen beim Zugriff auf Premium-Features
- Nutzer kann Abo direkt aus der Paywall heraus kaufen
- Design passt zum "Cozy" Dark-Mode Theme
- Kein harter Block – Nutzer kann immer zurück zur App

---

## 🤖 AI Features

---

### Issue #5: AI Book Recommendation Service (OpenAI API)

**Labels:** `feature`, `ai`, `priority: medium`

**Beschreibung:**
Premium-Nutzer können mit einer AI chatten, die basierend auf ihrer Bibliothek, Bewertungen und Lesegewohnheiten personalisierte Buchempfehlungen gibt. Backend-seitig wird die OpenAI API (GPT-5 Mini) angesprochen.

**Aufgaben:**
- [ ] `IAiRecommendationService` Interface:
  - `Task<AiResponse> GetRecommendationAsync(AiPrompt prompt)`
  - `Task<int> GetRemainingRequestsAsync()` (monatliches Limit)
  - `Task<IEnumerable<ChatMessage>> GetChatHistoryAsync()`
- [ ] OpenAI API Integration:
  - HTTP Client mit API-Key (sicher speichern – nicht hardcoden!)
  - Request/Response DTOs für `/v1/chat/completions`
  - Model: `gpt-5-mini` (konfigurierbar)
  - System-Prompt der den Kontext setzt:
    ```
    Du bist ein Buchempfehlungs-Assistent. Du kennst die Bibliothek 
    des Nutzers und seine Bewertungen. Empfehle Bücher basierend auf 
    seinen Vorlieben. Antworte auf Deutsch. Halte Antworten unter 
    300 Wörtern. Gib ISBN-Nummern mit an wenn möglich.
    ```
  - Nutzer-Kontext automatisch anhängen: Top-bewertete Bücher, Lieblingsgenres, Bewertungsmuster
- [ ] Rate Limiting:
  - 30 Anfragen pro Monat pro Nutzer
  - Counter in SQLite speichern, monatlich resetten
  - UI zeigt verbleibende Anfragen an
- [ ] Chat-Verlauf lokal speichern (SQLite Tabelle `AiChatMessages`)
- [ ] API-Key Management:
  - **Nicht** im Client speichern!
  - Option A: Eigener minimaler Proxy-Server (z.B. Azure Function) der den Key hält
  - Option B: Key verschlüsselt im App-Bundle (weniger sicher, aber für MVP akzeptabel)
- [ ] Error Handling: Timeout, Rate Limit, API Down, kein Internet
- [ ] Unit Tests mit gemockter API

**Akzeptanzkriterien:**
- Premium-Nutzer kann eine Chat-Konversation starten und Buchempfehlungen erhalten
- AI kennt die Bibliothek und Bewertungen des Nutzers (als Kontext im Prompt)
- Monatliches Limit von 30 Anfragen wird enforced
- Chat-Verlauf wird lokal gespeichert
- Funktioniert nicht für Free-Nutzer (Paywall)

---

### Issue #6: AI Chat UI

**Labels:** `ui`, `ai`, `priority: medium`

**Beschreibung:**
Chat-Interface für die AI-Buchempfehlungen. Muss sich natürlich in das bestehende Cozy-Theme einfügen.

**Aufgaben:**
- [ ] `AiChatPage.razor` erstellen
- [ ] Chat-Bubble Layout (User rechts, AI links) mit Cozy-Theme Styling
- [ ] Eingabefeld unten mit Send-Button
- [ ] Quick-Prompts / Vorschläge über dem Eingabefeld:
  - "Empfiehl mir etwas Ähnliches wie [letztes Buch]"
  - "Was sollte ich als Nächstes lesen?"
  - "Ich suche ein Buch mit gutem Worldbuilding"
- [ ] Loading-State mit Typing-Indikator während API-Call
- [ ] Anzeige verbleibender Anfragen (z.B. "23/30 übrig")
- [ ] Buchempfehlungen in der Antwort als tappbare Karten rendern:
  - Wenn ISBN vorhanden → Google Books API für Cover abrufen
  - "Zur Bibliothek hinzufügen" Button direkt an der Empfehlung
- [ ] Leerer Zustand: Willkommensnachricht + Quick-Prompts
- [ ] Chat-Verlauf laden beim Öffnen
- [ ] Scroll-Verhalten: Auto-Scroll zu neuen Nachrichten

**Akzeptanzkriterien:**
- Chat fühlt sich flüssig an (kein Lag beim Tippen)
- Buchempfehlungen können direkt zur Bibliothek hinzugefügt werden
- Quick-Prompts erleichtern den Einstieg
- Verbleibende Anfragen sind sichtbar
- Dark Mode / Cozy Theme konsistent

---

## 🔒 Feature-Gating (Free vs. Premium)

---

### Issue #7: Bücherregale limitieren (Free: 3, Premium: ∞)

**Labels:** `feature`, `monetization`, `priority: medium`

**Beschreibung:**
Free-Nutzer können maximal 3 eigene Bücherregale/Kategorien erstellen. Bei Versuch ein 4. Regal zu erstellen, erscheint die Paywall. Bücher selbst sind NICHT limitiert.

**Aufgaben:**
- [ ] Shelf-Count Check in `ShelfService` / ViewModel einbauen
- [ ] Beim Erstellen eines neuen Regals: `ISubscriptionService.HasFeatureAccessAsync(FeatureFlag.UnlimitedShelves)` prüfen
- [ ] Wenn Free + bereits 3 Regale → Paywall öffnen statt Regal erstellen
- [ ] UI-Hinweis auf der Regale-Übersicht: "3/3 Regale verwendet – Upgrade für mehr"
- [ ] Wenn Nutzer Premium kündigt und >3 Regale hat: Bestehende Regale bleiben, aber keine neuen möglich

**Akzeptanzkriterien:**
- Free-Nutzer können exakt 3 Regale erstellen, danach Paywall
- Premium-Nutzer können unbegrenzt Regale erstellen
- Downgrade löscht keine bestehenden Regale

---

### Issue #8: Erweiterte Statistiken (Premium-Only)

**Labels:** `feature`, `monetization`, `priority: medium`

**Beschreibung:**
Free-Nutzer sehen Basis-Statistiken (Bücher gelesen, aktuelle Streak). Erweiterte Statistiken sind Premium-exklusiv.

**Aufgaben:**
- [ ] Statistik-Dashboard aufteilen in:
  - **Free:** Bücher gesamt, aktuelle Streak, Bücher diesen Monat
  - **Premium:** Genre-Verteilung (Pie Chart), Lese-Trends über Zeit (Line Chart), Seiten/Monat, Durchschnittsbewertung pro Kategorie (Charakter vs Plot vs Schreibstil etc.), Lesezeit-Trends, "Bestes Lesemonat", Jahresvergleich
- [ ] Premium-Statistiken mit Blur/Overlay + Upgrade-CTA für Free-Nutzer anteasern
- [ ] Soft-Preview: Die Charts rendern, aber verschwommen anzeigen – Nutzer sieht, was er bekommt

**Akzeptanzkriterien:**
- Free-Nutzer sehen Basis-Stats klar und erweiterte Stats verschwommen
- Premium-Nutzer sehen alles
- Blur-Overlay hat "Premium freischalten" Button der zur Paywall führt

---

### Issue #9: Export-Funktionen (Premium-Only)

**Labels:** `feature`, `monetization`, `priority: low`

**Beschreibung:**
JSON- und CSV-Export der Bibliothek ist ein Premium-Feature. Backup/Restore bleibt für alle verfügbar (Datenhoheit).

**Aufgaben:**
- [ ] Export-Buttons in den Einstellungen mit Tier-Check versehen
- [ ] Bei Free → Paywall statt Export
- [ ] **Wichtig:** Datenbank-Backup/Restore bleibt FREE (SQLite Backup) – das ist Datensicherheit, keine Premium-Funktion
- [ ] Klare Unterscheidung in der UI: "Backup" (Free) vs. "Export als CSV/JSON" (Premium)

**Akzeptanzkriterien:**
- CSV/JSON Export nur für Premium
- SQLite Backup/Restore für alle
- Klare UI-Trennung zwischen Backup und Export

---

### Issue #10: Custom Themes (Premium-Only)

**Labels:** `ui`, `monetization`, `priority: low`

**Beschreibung:**
Zusätzliche Themes neben dem Standard Dark-Mode sind Premium-exklusiv.

**Aufgaben:**
- [ ] Theme-System implementieren (CSS Custom Properties swappen)
- [ ] Standard-Theme: Cozy Dark (Free)
- [ ] Premium-Themes (mindestens 3 zum Launch):
  - Light / Cream Mode
  - Midnight Blue
  - Forest Green
  - Optional: Seasonal Themes (Herbst, Weihnachten etc.)
- [ ] Theme-Auswahl in Settings mit Premium-Lock Icons
- [ ] Theme-Vorschau: Nutzer kann Premium-Theme sehen (Preview), aber nicht aktivieren ohne Abo

**Akzeptanzkriterien:**
- Free-Nutzer hat Standard Dark Theme
- Premium-Nutzer kann zwischen Themes wechseln
- Theme-Vorschau für Free-Nutzer sichtbar
- Theme-Wechsel ohne App-Neustart

---

## 📋 Store & Legal

---

### Issue #11: Google Play Store Listing vorbereiten

**Labels:** `release`, `priority: high`

**Beschreibung:**
Alles was für den Google Play Store Launch benötigt wird.

**Aufgaben:**
- [ ] App-Name: "BookHeart" (Verfügbarkeit im Play Store prüfen)
- [ ] Kurzbeschreibung (80 Zeichen): "Tracke deine Bücher, sammle XP und lass dich von AI inspirieren"
- [ ] Langbeschreibung mit Keywords (ASO-optimiert):
  - Keywords einbauen: "Bücher tracken", "Leseliste", "Reading Tracker", "Buchempfehlung", "Lese-Statistiken", "offline"
- [ ] Screenshots erstellen (mind. 4, idealerweise 8):
  - Bibliothek / Regal-Ansicht
  - Lese-Session Timer
  - Statistik-Dashboard
  - Virtueller Garten
  - AI-Chat
  - Spine-Ansicht
- [ ] Feature-Graphic (1024x500)
- [ ] App-Icon (512x512) – finales Design
- [ ] Kategorie: "Bücher & Nachschlagewerke"
- [ ] Altersfreigabe: Fragebogen ausfüllen (IARC)
- [ ] Datenschutzerklärung URL (siehe Issue #12)
- [ ] In-App-Produkte in Play Console anlegen (siehe Issue #2)

**Akzeptanzkriterien:**
- Vollständiges Store Listing fertig und review-ready
- Screenshots zeigen die App im besten Licht
- ASO-Keywords in Titel, Kurz- und Langbeschreibung

---

### Issue #12: Datenschutzerklärung & Rechtliches

**Labels:** `legal`, `release`, `priority: high`

**Beschreibung:**
Für den Play Store, AdMob und die AI-Features wird eine Datenschutzerklärung benötigt. Pflicht für Store-Veröffentlichung.

**Aufgaben:**
- [ ] Datenschutzerklärung erstellen (Deutsch + Englisch):
  - Welche Daten werden gesammelt (lokal gespeichert, nicht übertragen)
  - AdMob: Welche Daten sammelt Google Ads
  - AI-Feature: Welche Daten werden an OpenAI gesendet (Buchtitel, Bewertungen – keine persönlichen Daten)
  - Abo-Daten: Über Google Play verwaltet
  - Keine Account-Pflicht, keine Registrierung
  - Kontaktdaten des Entwicklers
- [ ] Datenschutzerklärung auf GitHub Pages oder simple Website hosten
- [ ] URL in Play Store Listing eintragen
- [ ] Impressum (falls erforderlich nach deutschem Recht – bei kommerzieller App: ja)
- [ ] Google Play Data Safety Fragebogen ausfüllen

**Akzeptanzkriterien:**
- Datenschutzerklärung online erreichbar unter stabiler URL
- Deckt AdMob, OpenAI API und lokale Datenhaltung ab
- Impressum vorhanden (deutsches Recht)
- Data Safety Section im Play Store korrekt ausgefüllt

---

## 🧪 Qualität & Testing

---

### Issue #13: Monetarisierungs-Flow E2E testen

**Labels:** `testing`, `priority: medium`

**Beschreibung:**
Alle Monetarisierungs-Flows müssen durchgetestet werden bevor der Store-Launch passiert.

**Aufgaben:**
- [ ] Test-Matrix erstellen:
  - Free → Premium Kauf (monatlich)
  - Free → Premium Kauf (jährlich)
  - Premium → Abo läuft aus → Free (Features korrekt gesperrt?)
  - Premium → Kündigung → Restlaufzeit → Free
  - Neuinstallation → Restore Purchases
  - Free: Alle Paywalls triggern und prüfen
  - Free: Ads werden angezeigt
  - Premium: Ads werden NICHT angezeigt
- [ ] Google Play Test-Tracks nutzen (Internal Testing)
- [ ] Sandbox-Käufe mit Test-Accounts durchführen
- [ ] Edge Case: Kein Internet beim Kauf
- [ ] Edge Case: Play Store nicht verfügbar (Huawei etc.)

**Akzeptanzkriterien:**
- Alle Flows in der Test-Matrix bestanden
- Kein Zustand in dem ein zahlender Nutzer Features nicht bekommt
- Kein Zustand in dem ein Free-Nutzer Premium-Features ohne Zahlung nutzen kann

---

## 📌 Empfohlene Reihenfolge

| Priorität | Issue | Begründung |
|-----------|-------|------------|
| 1 | #1 Tier-Architektur | Grundlage für alles |
| 2 | #2 In-App Purchases | Ohne Bezahlung kein Revenue |
| 3 | #3 AdMob | Zweite Revenue-Säule |
| 4 | #4 Paywall UI | Verbindet #1-#3 mit dem Nutzer |
| 5 | #7 Regale limitieren | Erster konkreter Paywall-Trigger |
| 6 | #8 Erweiterte Statistiken | Zweiter Paywall-Trigger |
| 7 | #12 Datenschutz | Muss vor Store-Launch stehen |
| 8 | #11 Store Listing | Parallel zu Entwicklung vorbereiten |
| 9 | #5 AI Service | Premium-Killer-Feature |
| 10 | #6 AI Chat UI | Sichtbar nach Service |
| 11 | #9 Export gaten | Quick Win |
| 12 | #10 Custom Themes | Nice-to-have, kann nach Launch |
| 13 | #13 E2E Testing | Vor Release |