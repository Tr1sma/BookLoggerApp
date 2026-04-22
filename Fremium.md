# Premium-Subscription-System (Plus & Premium)

## Context

BookHeart soll vor dem Play-Store-Launch ein Monetarisierungs-System bekommen: **zwei Abo-Tiers** (Plus 2,99€/Mo · 29,99€/J, Premium 11,99€/Mo · 99,99€/J) plus einen **Premium-Lifetime-Einmalkauf** (Launch-Special 99,99€, dann 249,99€). Die App ist pre-Launch, also keine Grandfathering-Pflicht. Ziel: nachhaltige Recurring-Revenue-Quelle, ohne das Grundversprechen "lokaler, persönlicher Reading-Tracker" zu untergraben — Backup, Kern-Lesen und alle Widgets bleiben kostenlos. Premium positioniert sich als "Power-User-Upgrade" für Deep-Analytics, Share-Cards und exklusive Gamification.

Die App hat bereits ein starkes Fundament (XP/Coins/Level, Shop für Plants/Decorations, `IAppSettingsProvider`-Cache, Onboarding-System, Firebase Analytics). **Keine** Billing-Infrastruktur oder Feature-Gates existieren heute.

## Entscheidungen (vom User bestätigt)

### Tier-Struktur

**FREE** (Default)
- Bücher unbegrenzt, Status/Bewertung, Lese-Timer, Sessions
- Basis-Stats (Overview-Tab)
- XP/Level/Coins (Coins werden gesammelt, sofort ausgebbar nach Upgrade)
- **Free-Shop:** 4 Pflanzen (`Starter Sprout`, `Story Seedling`, `Bookworm Fern`, `Reading Cactus`) + 3 Dekos (`Reading Candle`, `Cosy Book Mug`, `Owl Figurine`)
- ISBN-Scan, Cover-Upload (Galerie/Kamera)
- Goals (Books/Pages/Minutes): max 3 aktiv, keine Genre/Trope-Filter
- 3 Regale (ohne benutzerdefinierte Farbe)
- Notizen & Zitate: Soft-Limit **3 pro Buch**
- Alle 3 Android-Widgets (Current Book, Streak, Goal)
- Komplettes Backup/Export/Import (CSV, ZIP, Cloud) — **bewusst nicht monetarisiert**
- 1 Standard-Theme, Missionen, Onboarding

**PLUS** (2,99€/Mo · 29,99€/J)
- Alles aus Free, zusätzlich:
- Alle Standard-Pflanzen + Dekorationen (außer Prestige/Ultimate)
- Notizen & Zitate unbegrenzt
- Wishlist, Tropes
- Reading Goals unbegrenzt (ohne Filter)
- Regale unbegrenzt + Regalfarben
- Weitere Themes

**PREMIUM** (11,99€/Mo · 99,99€/J · Lifetime 99,99€ Launch / 249,99€ regulär)
- Alles aus Plus, zusätzlich:
- Stats-Trends-Tab (Heatmap, Radar, Wochentag-Verteilung)
- Stats-Insights-Tab (Jahresvergleich, Top-Authors, Completion-Rates)
- Share-Cards (Reading Wrapped, Book Recommendation)
- Prestige-Pflanzen (`Chronicle Tree`, `Eternal Phoenix Bonsai`)
- Ultimate-Dekoration (`Heart of Stories`)
- Reading Goals mit Genre/Trope-Filter
- Feature-Vorschlags-Formular (mailto: in Settings)
- Google Play Family Sharing

### Pricing-Policies
- **Introductory Price**: 1. Monat 0,99€ für **beide** Tiers (via Play Offer-System)
- **Family Sharing**: nur Premium
- **Lapse-Verhalten**: sofortiger Downgrade. Daten bleiben in DB, Premium-Views versteckt, 1 Pflanze aktiv, überzählige Shelves/Plants/Decorations per `IsHiddenByEntitlement` markiert (nicht gelöscht).
- **Verifikation**: Client-only über Google Play Billing Library v7+
- **Gating-UX**: Feature sichtbar mit Schloss-Icon → Tap öffnet Paywall-Modal mit Vergleichstabelle Plus vs Premium, je 3 Preisbuttons (Monat/Jahr/Lifetime)
- **Werbung**: keine, nur Upgrade-Prompts
- **Upgrade-CTAs**: permanenter Settings-Eintrag, kontextuelle Dashboard-Card, Reading-Wrapped-Teaser Ende Dezember
- **Promo-Codes**: Settings-Eingabefeld mit hardcoded Short-Term-Codes (Prefix `BH-…`, App-Version-limitiert) **plus** Google-Play-native Promo-Codes für High-Value-Belohnungen wie Lifetime Premium (single-use, redeemed im Play Store → `IBillingService.LaunchRedeemPromoFlowAsync`)

## Architektur

### Neue Enums & Models
Pfad: `BookLoggerApp.Core/Entitlements/`
- `SubscriptionTier { Free=0, Plus=1, Premium=2 }` — monotone Ordinal-Werte erlauben `tier >= Plus`-Checks
- `BillingPeriod { Monthly, Yearly, Lifetime }` — Lifetime wird NICHT als eigener Tier modelliert, sondern als `Tier=Premium, BillingPeriod=Lifetime, ExpiresAt=null`
- `FeatureKey { UnlimitedNotesAndQuotes, UnlimitedReadingGoals, ReadingGoalsWithGenreTropeFilter, UnlimitedShelves, CustomShelfColors, StandardPlantsAndDecorations, PrestigePlants, UltimateDecorations, StatsTrendsTab, StatsInsightsTab, ShareCards, Wishlist, Tropes, PremiumThemes, FeatureSuggestionForm, FamilySharing }`
- `FeaturePolicy` (static): `IReadOnlyDictionary<FeatureKey, SubscriptionTier>` als Single-Source-of-Truth für Paywall-Tabelle **und** Runtime-Checks
- `FeatureDisplayInfo` (record): Label, Icon, Beschreibung pro FeatureKey (für Paywall-Rendering)

Neue Entity `BookLoggerApp.Core/Models/UserEntitlement.cs` (single-row, analog zu AppSettings):
```csharp
public class UserEntitlement
{
    public Guid Id { get; set; }
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
    public BillingPeriod? BillingPeriod { get; set; }
    public string? ProductId { get; set; }          // SKU
    public string? PurchaseToken { get; set; }
    public string? OrderId { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }        // null bei Lifetime
    public DateTime? LastVerifiedAt { get; set; }
    public bool AutoRenewing { get; set; }
    public bool InGracePeriod { get; set; }
    public bool IsInIntroductoryPrice { get; set; }
    public bool IsFamilyShared { get; set; }
    public string? LapseReason { get; set; }
    public DateTime? LapsedAt { get; set; }
    public string? PromoCodeRedeemed { get; set; }
    public DateTime? PromoExpiresAt { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }
}
```

`AppSettings.cs` erweitern um **denormalisierten Spiegel** (für Hot-Path-Reads ohne zweiten DB-Hit):
```csharp
public SubscriptionTier CurrentTier { get; set; } = SubscriptionTier.Free;
public DateTime? EntitlementExpiresAt { get; set; }
```

### Neue Services

| Interface | Layer | Lifecycle | Zweck |
|---|---|---|---|
| `IEntitlementService` | Core | **Singleton** | Cache des Tiers, `HasAccess(FeatureKey)`, Event `EntitlementChanged`, Lapse-Detection |
| `IBillingService` | Core Abstraction / MAUI Impl | **Singleton** | Wrapper um `Plugin.Maui.InAppBilling`; Connect, Query, Purchase, Acknowledge, Redeem |
| `IEntitlementStore` | Core / Infrastructure | Transient | Repository über `IUnitOfWork` für `UserEntitlement` |
| `IPromoCodeService` | Core / Infrastructure | Transient | Validiert hardcoded `BH-…`-Codes, delegiert Play-Native-Codes an Billing |
| `IPaywallCoordinator` | Core | Singleton | Öffnet Paywall-Modal, orchestriert Kauf-Flow |
| `IProductCatalog` | Core | Singleton | Zentrale SKU-Definitionen (`plus_monthly`, `plus_yearly`, `premium_monthly`, `premium_yearly`, `premium_lifetime`) |
| `IFeatureGuard` | Core | Singleton | Helper: `EnforceLimitAsync`, wirft `EntitlementRequiredException` (neue Exception im `BookLoggerApp.Core/Exceptions/`-Tree) |

`IEntitlementService`-Kern:
```csharp
event EventHandler<EntitlementChangedEventArgs>? EntitlementChanged;
SubscriptionTier CurrentTier { get; }
UserEntitlement? CurrentEntitlement { get; }
Task InitializeAsync(CancellationToken ct = default);
bool HasAccess(FeatureKey feature);                   // synchron, aus Cache
Task<bool> HasAccessAsync(FeatureKey feature, CancellationToken ct = default);
Task RefreshAsync(CancellationToken ct = default);
Task ApplyPurchaseAsync(PurchaseResult purchase, CancellationToken ct = default);
Task ApplyLapseAsync(string reason, CancellationToken ct = default);
Task ApplyPromoAsync(PromoActivation promo, CancellationToken ct = default);
```

### Feature-Gating
- **Binäre Feature-Freischaltungen** (Wishlist, Share-Cards etc.): in ViewModel/Component per `_entitlements.HasAccess(FeatureKey.X)` + `LockedFeatureButton.razor`-Wrapper
- **Quantitative Limits** (3 Notes/Buch, 3 Goals, 3 Shelves): in den jeweiligen Services (`AnnotationService.AddAsync`, `GoalService.CreateAsync`, `ShelfService.CreateAsync`) über `IFeatureGuard.EnforceLimitAsync(FeatureKey.X, currentCount, limit: 3)` → `EntitlementRequiredException` bei Verletzung
- **Shop-Items** (Plants/Decorations): neue Flags `PlantSpecies.IsFreeTier`, `PlantSpecies.IsPrestigeTier`, `ShopItem.IsFreeTier`, `ShopItem.IsUltimateTier` direkt auf Seed-Daten. `PlantShopViewModel`/`DecorationShopViewModel` filtern Sortiment nach `CurrentTier`.

### Paywall-UI (Blazor)
Ordner: `BookLoggerApp/Components/Shared/Paywall/`
```
PaywallModal.razor              Overlay-Shell
├── PaywallHeader.razor         Kontexttitel ("Notizen unlimited mit Plus")
├── TierComparisonTable.razor   Free | Plus | Premium, liest FeaturePolicy
│   └── FeatureRow.razor
├── TierSelectionPanel.razor    Tabs Plus / Premium, je 3 PriceButtons
│   └── PriceButton.razor       Monat | Jahr | Lifetime, Intro-Badge wenn <30d seit Install
├── PromoCodeInput.razor
├── PaywallFooter.razor         Restore | Terms | Privacy
└── PaywallErrorBanner.razor
```
`IPaywallCoordinator.ShowPaywallAsync(FeatureKey? trigger)` setzt Observable; `MainLayout.razor` beobachtet analog zu `AppStartupOverlay`. Preise kommen als `BillingProduct.FormattedPrice` (lokalisiert von Play) — **niemals** eigene Formatter (Regional Pricing wird automatisch).

Neue Shared-Component `LockedFeatureButton.razor`: Wrapper-Component, zeigt Schloss-Overlay + ruft `ShowPaywallAsync` on Tap. Einsatz in `PlantShop.razor`, `Stats.razor` (Trends/Insights-Tabs), `Goals.razor` (Filter-UI), `BookDetail.razor` (Notiz/Zitat-Limit), `Settings.razor` (Themes, Wishlist-Toggle).

### Billing-Integration
- **NuGet**: `Plugin.Maui.InAppBilling` (v4.x, .NET 10 MAUI-kompatibel)
- **AndroidManifest** (`BookLoggerApp/Platforms/Android/AndroidManifest.xml`): `<uses-permission android:name="com.android.vending.BILLING" />`
- **MAUI-Impl**: `BookLoggerApp/Services/Billing/AndroidBillingService.cs` (Android-only; Non-Android-Head bekommt `NoOpBillingService` aus Core)
- **App-Start-Flow**: In `AppStartupViewModel` nach DB-Init → `IBillingService.ConnectAsync` → `QueryActivePurchasesAsync` → pro Purchase `IEntitlementService.ApplyPurchaseAsync` → UI-Refresh. Dies ist **gleichzeitig** der Restore-Flow.
- **Purchase-Flow**: Paywall → `IPaywallCoordinator.PurchaseAsync(tier, period)` → `IProductCatalog` liefert ProductId → `LaunchPurchaseFlowAsync(productId, oldPurchaseToken?)` (für Upgrade Plus→Premium mit `IMMEDIATE_WITH_TIME_PRORATION`) → Event `PurchaseUpdated` → `ApplyPurchaseAsync` → `AcknowledgePurchaseAsync` falls nötig
- **Lapse-Detection**: bei App-Start und täglich während Session. `RefreshAsync` vergleicht `QueryActivePurchasesAsync`-Ergebnis mit lokal gespeichertem Entitlement. SKU weg UND `ExpiresAt < UtcNow` → `ApplyLapseAsync("expired")`.

### Lapse-Handler (`EntitlementLapseHandler`)
Pfad: `BookLoggerApp.Infrastructure/Services/EntitlementLapseHandler.cs`
- **Pflanzen**: Deterministisch genau 1 aktiv lassen — Prio: `IsActive && Status==Healthy`, dann `OrderBy(PlantedAt)`. Rest `IsActive=false` und bei Prestige-Species zusätzlich `IsHiddenByEntitlement=true`.
- **Regale**: Top 3 nach `SortOrder` behalten. Shelves[3..] bekommen `IsHiddenByEntitlement=true` (neues Feld) — KEINE Löschung. Bei Re-Upgrade wird Flag zurückgesetzt.
- **Goals**: bestehende bleiben aktiv bis Abschluss. Neu anlegen blockiert bei Overflow / Filter-Feature.
- **Notes/Quotes**: bleiben vollständig sichtbar. Create-Guard greift ab jetzt.
- **Dekorationen**: Ultimate-Deko (`Heart of Stories`) bekommt `IsHiddenByEntitlement=true`.
- Am Ende `_settingsProvider.InvalidateCache()` + `_settingsProvider.UpdateSettingsAsync(tier=Free)`.

### DB-Migration
**Name:** `20260423_AddPremiumSubscriptionSystem` (genereren per `dotnet ef migrations add …`)

Änderungen:
- Neue Tabelle `UserEntitlements` mit allen Feldern aus `UserEntitlement`
- `AppSettings`: neue Spalten `CurrentTier` (int, default 0), `EntitlementExpiresAt` (datetime?)
- `Shelves`: neue Spalte `IsHiddenByEntitlement` (bool, default false)
- `UserPlants`: neue Spalte `IsHiddenByEntitlement` (bool, default false)
- `UserDecorations`: neue Spalte `IsHiddenByEntitlement` (bool, default false)
- `PlantSpecies`: neue Spalten `IsFreeTier` (bool), `IsPrestigeTier` (bool)
- `ShopItems`: neue Spalten `IsFreeTier` (bool), `IsUltimateTier` (bool)

**Seed-Updates:**
- `PlantSeedData.cs`: `IsFreeTier=true` für `Starter Sprout`, `Story Seedling`, `Bookworm Fern`, `Reading Cactus`. `IsPrestigeTier=true` für `Chronicle Tree`, `Eternal Phoenix Bonsai`.
- `DecorationSeedData.cs`: `IsFreeTier=true` für `Reading Candle`, `Cosy Book Mug`, `Owl Figurine`. `IsUltimateTier=true` für `Heart of Stories`.
- `DbInitializer`: default-Seed einer leeren `UserEntitlement`-Row beim ersten Start (Pattern wie bei `AppSettings`).

### DI-Registrierung (`MauiProgram.cs`)
Neue Methode `RegisterEntitlementServices(builder)` nach `RegisterAnalyticsServices`:
```csharp
builder.Services.AddSingleton<IEntitlementService, EntitlementService>();
builder.Services.AddSingleton<IProductCatalog, ProductCatalog>();
builder.Services.AddSingleton<IPaywallCoordinator, PaywallCoordinator>();
builder.Services.AddSingleton<IFeatureGuard, FeatureGuard>();
builder.Services.AddTransient<IEntitlementStore, EntitlementStore>();
builder.Services.AddTransient<IPromoCodeService, PromoCodeService>();
builder.Services.AddTransient<EntitlementLapseHandler>();
#if ANDROID
builder.Services.AddSingleton<IBillingService, AndroidBillingService>();
#else
builder.Services.AddSingleton<IBillingService>(_ => NoOpBillingService.Instance);
#endif
builder.Services.AddTransient<PaywallViewModel>();
builder.Services.AddTransient<EntitlementStatusViewModel>();
```

### Analytics-Events
Ergänze `BookLoggerApp.Core/Services/Analytics/AnalyticsEventNames.cs`:
`paywall_shown`, `paywall_dismissed`, `paywall_tier_selected`, `purchase_initiated`, `purchase_completed`, `purchase_failed`, `purchase_cancelled`, `purchase_restored`, `subscription_lapsed`, `promo_code_redeemed`, `promo_code_failed`, `upgrade_cta_clicked`, `feature_suggestion_sent`.

Neue User-Properties: `subscription_tier`, `subscription_period`, `is_subscriber`. `UserPropertiesPublisher` subscribed auf `EntitlementChanged` und setzt Properties neu.

### Play-Console-Konfiguration (manuelle Schritte des Users)
1. In-App-Products anlegen: `plus_monthly`, `plus_yearly`, `premium_monthly`, `premium_yearly` als Subscriptions; `premium_lifetime` als Managed Product.
2. Base-Plans pro Subscription: Monthly/Yearly.
3. Offer: Intro-Price 0,99€ für ersten Zyklus, `new_customer`-eligibility.
4. Premium-Subscriptions aktivieren: Family-Sharing-Flag = on.
5. Promo-Codes generieren: für Lifetime Premium bis zu 500/Quartal kostenlos in Play Console.

### Feature-Suggestion-Form (Premium-only)
Neue Shared-Component `FeatureSuggestionForm.razor` in `Settings.razor` eingebettet, gated über `LockedFeatureButton` auf `FeatureKey.FeatureSuggestionForm`. Submit ruft `IShareService.OpenMailtoAsync("tristan.atze@gmail.com", subject: "[BookHeart Premium] Feature-Vorschlag", body: …)`. Keine eigene Backend-Integration.

### Upgrade-CTAs (Entry-Points außerhalb Feature-Locks)
1. **Settings-Page**: neuer Menüpunkt "Plus & Premium" öffnet `EntitlementStatusPage.razor`. Zeigt aktuellen Tier, Renewal-Datum, "Plan ändern"-Deep-Link (`IBillingService.OpenSubscriptionManagementAsync`), Restore-Button, Paywall-CTA für Non-Subscriber.
2. **Dashboard-Card** `UpgradeRecommendationCard.razor`: erscheint bei Free-User wenn Heuristiken getriggert werden (≥5 Notizen, ≥30 Tage Streak, etc.). Textvariation basiert auf beobachtetem Verhalten.
3. **Reading-Wrapped-Teaser**: Ab 1. Dezember für Free-User auf Dashboard eine Card "Sieh dein Wrapped 2026 — mit Premium". Bei Tap öffnet Paywall mit `FeatureKey.ShareCards` als Trigger.

## Testing

Neue Test-Dateien in `BookLoggerApp.Tests/`:
- `Unit/Entitlements/FeaturePolicyTests.cs` — Tabelle pro FeatureKey prüfen
- `Unit/Entitlements/SubscriptionTierTests.cs` — Ordinal-Ordering (Free<Plus<Premium)
- `Services/EntitlementServiceTests.cs` — Init, HasAccess, Purchase, Lapse, Promo mit NSubstitute-Mocks für `IBillingService` & `IEntitlementStore`
- `Services/EntitlementLapseHandlerTests.cs` — TestDbContext mit 5 Plants + 5 Shelves; nach Lapse assert: 1 aktiv, Rest hidden, keine Löschungen
- `Services/PromoCodeServiceTests.cs` — hardcoded Codes, Expiry, Kollisionen
- `Services/FeatureGuardTests.cs` — Limit-Enforcement, Exception-Throw
- `Unit/ViewModels/PaywallViewModelTests.cs` — Product-Loading, Tier-Selection, Purchase-Click
- `Unit/ViewModels/PlantShopViewModelTests.cs` erweitern — Plus-User sieht alle Non-Prestige-Pflanzen, Free-User nur die 4 Free-Plants
- `TestHelpers/MockBillingService.cs` — Fake-Impl für deterministische Purchase-Flows
- `TestHelpers/MockEntitlementService.cs` — für Tests anderer ViewModels

Tests laufen auf `net10.0` (nicht android); `IBillingService` wird nur über Interface konsumiert. Android-Impl wird durch `MockBillingService` ersetzt.

## Build-Reihenfolge (14 Schritte)

1. Enums + `UserEntitlement`-Entity + `FeaturePolicy`-Tabelle + `FeatureDisplayInfo` anlegen (Core). Keine DI-Registrierung, keine Migration. App bleibt unverändert.
2. EF-Migration `AddPremiumSubscriptionSystem` erstellen + `AppDbContext`/Configurations erweitern. Seed-Flag-Updates in `PlantSeedData.cs` + `DecorationSeedData.cs`. `DbInitializer` um Entitlement-Row erweitern. Lokal migrieren.
3. `IEntitlementService` + `EntitlementStore` + `EntitlementService` mit In-Memory-Stubs (noch kein Play-Billing). `InitializeAsync` im Startup-Flow. `EntitlementChanged`-Event. DI in `MauiProgram.cs`. Default-Tier=Free → alle Gates geschlossen. **Debug-Switch** `#if DEBUG IEntitlementService.ForceTierAsync(Premium)` via Settings-Debug-Panel, damit Entwicklung weitergeht.
4. `IFeatureGuard` + `EntitlementRequiredException` + Unit-Tests.
5. `EntitlementLapseHandler` + Unit-Tests mit TestDbContext.
6. Service-Guards einbauen: `AnnotationService`, `QuoteService`, `GoalService`, `ShelfService`, `PlantService` (Free-Shop-Filter), `DecorationService` (Free-Shop-Filter). Bestehende Tests erweitern.
7. Paywall-Components (`PaywallModal`, `TierComparisonTable`, `TierSelectionPanel`, `PriceButton`, `FeatureRow`, `PromoCodeInput`, `PaywallFooter`, `PaywallErrorBanner`, `LockedFeatureButton`). `IPaywallCoordinator`-Singleton. Dummy-Produkte aus `IProductCatalog`. Kauf-Button ruft lokal `ApplyPurchaseAsync` mit Fake-Purchase.
8. NuGet `Plugin.Maui.InAppBilling` + `AndroidBillingService`-Impl. AndroidManifest-Permission. `ConnectAsync` + `QueryProductsAsync` → echte Preise in Paywall. `NoOpBillingService` für Tests.
9. Echte Purchase-Flows: `LaunchPurchaseFlowAsync`, `PurchaseUpdated`-Event, `AcknowledgePurchaseAsync`, Upgrade Plus→Premium mit Proration.
10. Restore-Flow (dieselbe Codepfad wie App-Start-Query).
11. `IPromoCodeService` + Settings-Eingabefeld + hardcoded Short-Term-Codes + Play-Native-Promo-Redeem-Intent.
12. Analytics-Events + `UserPropertiesPublisher`-Erweiterung.
13. Lapse-Detection-Timer (app-weit `System.Threading.Timer`, 1x täglich, + App-Start-Check).
14. UI-Integration: `EntitlementStatusPage`, `UpgradeRecommendationCard`, Reading-Wrapped-Teaser, `FeatureSuggestionForm`. Bottom-Nav bleibt unverändert.

Nach jedem Schritt bleibt die App lauffähig. Schritte 1-7 liefern funktionierendes Gating (Premium via Debug-Switch testbar). Schritte 8-10 machen echte Käufe möglich. Schritte 11+ sind Production-Polish.

## Wichtige Risiken/Edge-Cases

1. **Lifetime + aktives Monats-Abo**: Lifetime dominiert; Monats-Sub in Play-Konsole nicht-verlängert markieren. App zeigt Hinweis.
2. **Clock-Skew**: `ExpiresAt` ist immer Play-Store-authoritative.
3. **Refunds**: client-only detection über fehlenden SKU in `QueryActivePurchasesAsync` + `ExpiresAt` geprüft.
4. **Backup/Restore**: `IImportExportService` muss `UserEntitlement` vom Import **ausschließen** (gerätegebunden, analog zu Privacy/Consent-Feldern). Nach Restore zwingend `IEntitlementService.RefreshAsync()` aufrufen.
5. **Regional Pricing**: nie eigene Preise hardcoden — immer `BillingProduct.FormattedPrice` aus Play rendern.
6. **Intro-Price-Filterung**: Play filtert automatisch pro Google-Account.

## Critical Files

Neu:
- `BookLoggerApp.Core/Entitlements/SubscriptionTier.cs`
- `BookLoggerApp.Core/Entitlements/BillingPeriod.cs`
- `BookLoggerApp.Core/Entitlements/FeatureKey.cs`
- `BookLoggerApp.Core/Entitlements/FeaturePolicy.cs`
- `BookLoggerApp.Core/Entitlements/FeatureDisplayInfo.cs`
- `BookLoggerApp.Core/Models/UserEntitlement.cs`
- `BookLoggerApp.Core/Services/Abstractions/IEntitlementService.cs`
- `BookLoggerApp.Core/Services/Abstractions/IBillingService.cs`
- `BookLoggerApp.Core/Services/Abstractions/IProductCatalog.cs`
- `BookLoggerApp.Core/Services/Abstractions/IPaywallCoordinator.cs`
- `BookLoggerApp.Core/Services/Abstractions/IPromoCodeService.cs`
- `BookLoggerApp.Core/Services/Abstractions/IFeatureGuard.cs`
- `BookLoggerApp.Core/Services/Abstractions/IEntitlementStore.cs`
- `BookLoggerApp.Core/Exceptions/EntitlementRequiredException.cs`
- `BookLoggerApp.Infrastructure/Services/EntitlementService.cs`
- `BookLoggerApp.Infrastructure/Services/EntitlementStore.cs`
- `BookLoggerApp.Infrastructure/Services/EntitlementLapseHandler.cs`
- `BookLoggerApp.Infrastructure/Services/ProductCatalog.cs`
- `BookLoggerApp.Infrastructure/Services/PromoCodeService.cs`
- `BookLoggerApp.Infrastructure/Services/PaywallCoordinator.cs`
- `BookLoggerApp.Infrastructure/Services/FeatureGuard.cs`
- `BookLoggerApp.Infrastructure/Services/NoOpBillingService.cs`
- `BookLoggerApp.Infrastructure/Data/Configurations/UserEntitlementConfiguration.cs`
- `BookLoggerApp.Infrastructure/Data/Migrations/20260423_AddPremiumSubscriptionSystem.cs`
- `BookLoggerApp/Services/Billing/AndroidBillingService.cs`
- `BookLoggerApp/Components/Shared/Paywall/*.razor` (9 Komponenten)
- `BookLoggerApp/Components/Shared/LockedFeatureButton.razor`
- `BookLoggerApp/Components/Shared/UpgradeRecommendationCard.razor`
- `BookLoggerApp/Components/Shared/FeatureSuggestionForm.razor`
- `BookLoggerApp/Components/Pages/EntitlementStatusPage.razor`
- `BookLoggerApp.Core/ViewModels/PaywallViewModel.cs`
- `BookLoggerApp.Core/ViewModels/EntitlementStatusViewModel.cs`
- `BookLoggerApp.Tests/Services/EntitlementServiceTests.cs` + die o.g. Test-Dateien

Zu ändern:
- `BookLoggerApp.Core/Models/AppSettings.cs` (neue Felder `CurrentTier`, `EntitlementExpiresAt`)
- `BookLoggerApp.Core/Models/Shelf.cs`, `UserPlant.cs`, `UserDecoration.cs` (`IsHiddenByEntitlement`)
- `BookLoggerApp.Core/Models/PlantSpecies.cs` (`IsFreeTier`, `IsPrestigeTier`)
- `BookLoggerApp.Core/Models/ShopItem.cs` (`IsFreeTier`, `IsUltimateTier`)
- `BookLoggerApp.Infrastructure/Data/SeedData/PlantSeedData.cs`, `DecorationSeedData.cs`
- `BookLoggerApp.Infrastructure/Data/AppDbContext.cs` (DbSet<UserEntitlement>)
- `BookLoggerApp.Infrastructure/Services/AnnotationService.cs`, `QuoteService.cs`, `GoalService.cs`, `ShelfService.cs`, `PlantService.cs`, `DecorationService.cs`, `ImportExportService.cs` (Entitlement ausschließen)
- `BookLoggerApp.Core/ViewModels/PlantShopViewModel.cs`, `DecorationShopViewModel.cs`, `StatsViewModel.cs`, `GoalsViewModel.cs`, `SettingsViewModel.cs`, `AppStartupViewModel.cs`, `DashboardViewModel.cs`
- `BookLoggerApp/MauiProgram.cs` (`RegisterEntitlementServices`)
- `BookLoggerApp/Platforms/Android/AndroidManifest.xml` (BILLING-Permission)
- `BookLoggerApp/Components/Pages/Settings.razor`, `Dashboard.razor`, `Stats.razor`, `Goals.razor`, `BookDetail.razor`, `PlantShop.razor`
- `BookLoggerApp/Components/Layout/MainLayout.razor` (Paywall-Modal-Mount)
- `BookLoggerApp/BookLoggerApp.csproj` (NuGet `Plugin.Maui.InAppBilling`)
- `BookLoggerApp.Core/Services/Analytics/AnalyticsEventNames.cs`, `UserPropertyNames.cs`, `UserPropertiesPublisher.cs`
- Obsidian Vault: neue Dateien pro Service/ViewModel/Component, `Index.md` aktualisieren, pushen
- `CHANGELOG.md`: Eintrag unter `## [Unveröffentlicht] ### Hinzugefügt`

## Verifikation (End-to-End)

1. **Build & Unit-Tests**:
   ```
   dotnet build BookLoggerApp.sln
   dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --logger "trx;LogFileName=test_results.trx"
   ```
   Alle neuen Unit-Tests grün; kein Regressionsbruch in bestehenden Tests.

2. **Migration-Check**: `dotnet ef migrations list --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp` zeigt `AddPremiumSubscriptionSystem`.

3. **Lokaler Smoke-Test** (Debug-Switch auf Premium):
   - Paywall öffnet bei Tap auf Stats-Trends-Tab; zeigt alle 3 Preisbuttons pro Tier
   - Free-Shop zeigt genau 4 Plants + 3 Dekos
   - 4. Notiz pro Buch triggert `EntitlementRequiredException` → Paywall öffnet
   - Debug-Switch auf Free → Plants werden auf 1 aktiv reduziert, 4. Shelf bekommt Hidden-Flag, Trends-Tab zeigt Schloss
   - Debug-Switch zurück auf Premium → alles wiederhergestellt

4. **Play-Store-Integrationstest** (nach Schritt 8+):
   - Internal-Test-Track-Release bauen, Test-Account mit `license-testing@`-Mail verwenden
   - Subscription `plus_monthly` kaufen → App erkennt Tier=Plus → Notizen unlimited freigeschaltet
   - Kauf stornieren in Play Store → `RefreshAsync` beim nächsten Start erkennt Lapse → Downgrade greift korrekt
   - Upgrade Plus→Premium mit Proration → Entitlement aktualisiert
   - Lifetime kaufen → ExpiresAt=null, Monats-Sub-Hinweis korrekt
   - Promo-Code für Lifetime einlösen → Play Store Redeem-Flow → Entitlement-Refresh korrekt

5. **Regressionstest** Gamification: bestehende XP/Coin-Flows, Plant-Wachstum, Decoration-Placement laufen unverändert für Premium-User.
