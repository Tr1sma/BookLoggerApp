# V2 Progression System - Implementation Roadmap

## Overview
Implement a comprehensive progression system with XP rewards, level-based coin rewards, plant XP boosters, progress visualization, and celebration animations.

## Requirements Summary
- **XP Rewards**: 5 XP/minute + 20 XP/page + 100 XP/book completion
- **Coin Rewards**: Level × 50 coins per level-up (Level 1→2 = 50 coins, Level 2→3 = 100 coins, etc.)
- **Plant Boosters**: All owned plants give cumulative XP boost (e.g., 10% + 15% = 25% total boost)
- **Dynamic Pricing**: Plants get more expensive with each purchase (BaseCost + PurchaseCount × 200)
- **UI Updates**: Level display in header with XP progress bar
- **Celebrations**: Fullscreen animations for session completion and level-ups
- **Level Overview Page**: Dedicated page showing progression stats

---

## Phase 1: Core Data Model Updates

### 1.1 Update PlantSpecies Model
**File**: `BookLoggerApp.Core/Models/PlantSpecies.cs`
- Add `XpBoostPercentage` property (decimal) - e.g., 0.10 for 10% boost

### 1.2 Update PlantSpecies Seed Data
**File**: `BookLoggerApp.Infrastructure/Data/AppDbContext.cs`
- Add XpBoostPercentage values to each plant:
  - Starter Sprout: 5% base, +0.5% per level (max 10% at level 10)
  - Bookworm Fern: 8% base, +0.5% per level (max 14% at level 12)
  - Reading Cactus: 10% base, +1% per level (max 25% at level 15)

### 1.3 Add Plant Purchase Counter
**File**: `BookLoggerApp.Core/Models/AppSettings.cs`
- Add `PlantsPurchased` property (int, default 0)
- Used for dynamic pricing calculation

### 1.4 Create Migration
- Run: `dotnet ef migrations add AddProgressionSystemFields`

---

## Phase 2: Update XP Calculation

### 2.1 Update XpCalculator Constants
**File**: `BookLoggerApp.Infrastructure/Services/Helpers/XpCalculator.cs`
- Change `XP_PER_MINUTE = 5` (was 1)
- Change `XP_PER_PAGE = 20` (was 2)
- Add `XP_BOOK_COMPLETION = 100` (new constant)

### 2.2 Add Plant Boost Integration
**File**: `XpCalculator.cs`
- Add new method: `ApplyPlantBoost(int baseXp, decimal boostPercentage)`
- Formula: `(int)(baseXp * (1 + boostPercentage))`

---

## Phase 3: Create ProgressionService

### 3.1 Create IProgressionService Interface
**File**: `BookLoggerApp.Core/Services/Abstractions/IProgressionService.cs`

Methods:
```csharp
Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId);
Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId);
Task<decimal> GetTotalPlantBoostAsync();
Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp);
```

### 3.2 Create ProgressionResult Model
**File**: `BookLoggerApp.Core/Models/ProgressionResult.cs`

Properties:
```csharp
public int XpEarned { get; set; }
public int BaseXp { get; set; }
public decimal PlantBoostPercentage { get; set; }
public int BoostedXp { get; set; }
public int NewTotalXp { get; set; }
public LevelUpResult? LevelUp { get; set; }
```

### 3.3 Create LevelUpResult Model
**File**: `BookLoggerApp.Core/Models/LevelUpResult.cs`

Properties:
```csharp
public int OldLevel { get; set; }
public int NewLevel { get; set; }
public int CoinsAwarded { get; set; }
public int NewTotalCoins { get; set; }
```

### 3.4 Implement ProgressionService
**File**: `BookLoggerApp.Infrastructure/Services/ProgressionService.cs`

Key logic:
1. Calculate base XP from activity
2. Get total plant boost from all owned plants
3. Apply boost to get final XP
4. Add XP to user's TotalXp
5. Check if level-up occurred (use XpCalculator.CalculateLevelFromXp)
6. If level-up: award coins for each level gained (NewLevel × 50)
7. Return detailed result with breakdown

### 3.5 Register Service
**File**: `BookLoggerApp/MauiProgram.cs`
- Add: `builder.Services.AddTransient<IProgressionService, ProgressionService>()`

---

## Phase 4: Plant Boost & Dynamic Pricing

### 4.1 Add Plant Boost Calculation to PlantService
**File**: `BookLoggerApp.Infrastructure/Services/PlantService.cs`

Add methods:
```csharp
Task<List<UserPlant>> GetAllOwnedPlantsAsync()
Task<decimal> CalculateTotalXpBoostAsync()
```

Formula per plant:
```csharp
decimal baseBoost = species.XpBoostPercentage;
decimal levelBonus = plant.CurrentLevel * (species.XpBoostPercentage / species.MaxLevel);
decimal plantBoost = baseBoost + levelBonus;
```

Total = Sum of all plant boosts

### 4.2 Dynamic Plant Pricing
**File**: `BookLoggerApp.Core/Services/Abstractions/IPlantService.cs` & Implementation

Add method:
```csharp
Task<int> GetPlantCostAsync(Guid speciesId)
```

Update `PurchasePlantAsync()`:
- Get `PlantsPurchased` from AppSettings
- Calculate price: `BaseCost + (PlantsPurchased × 200)`
- Example: First plant = 500, second = 700, third = 900
- Increment `PlantsPurchased` after successful purchase
- Deduct calculated cost (not just BaseCost)

### 4.3 Update PlantShopViewModel
**File**: `BookLoggerApp.Core/ViewModels/PlantShopViewModel.cs`
- Show dynamic price instead of BaseCost
- Update affordability check to use dynamic price

---

## Phase 5: Integrate with Session Flow

### 5.1 Update ProgressService.EndSessionAsync
**File**: `BookLoggerApp.Infrastructure/Services/ProgressService.cs`

Modified flow:
1. Calculate session duration and pages
2. Get active plant ID from PlantService
3. **Call ProgressionService.AwardSessionXpAsync(minutes, pagesRead, activePlantId)**
4. Store ProgressionResult in session (add new property to ReadingSession?)
5. Award XP to active plant if exists (PlantService.AddExperienceAsync)
6. Return both ReadingSession and ProgressionResult

Consider changing return type: `Task<(ReadingSession session, ProgressionResult progression)>`

### 5.2 Update BookService.CompleteBookAsync
**File**: `BookLoggerApp.Infrastructure/Services/BookService.cs`

Add after marking complete:
```csharp
var activePlant = await _plantService.GetActivePlantAsync();
var progression = await _progressionService.AwardBookCompletionXpAsync(activePlant?.Id);
// Optionally return progression result
```

---

## Phase 6: UI - User Progress Widget

### 6.1 Create UserProgressWidget Component
**File**: `BookLoggerApp/Components/Shared/UserProgressWidget.razor`

Layout:
```
┌─────────────────────────┐
│ 🏆 Level 5              │
│ ▓▓▓▓▓▓░░░░ 1250/2000 XP │
└─────────────────────────┘
```

Features:
- Shows current level with icon
- Shows current XP / next level XP requirement
- Animated gradient progress bar
- Click to navigate to Level Overview page
- Real-time updates when XP changes

### 6.2 Create UserProgressViewModel
**File**: `BookLoggerApp.Core/ViewModels/UserProgressViewModel.cs`

Properties:
```csharp
int CurrentLevel
int TotalXp
int CurrentLevelXp    // XP accumulated in current level
int NextLevelXp       // XP needed for next level
decimal ProgressPercentage // calculated: CurrentLevelXp / NextLevelXp * 100
```

Methods:
- `LoadAsync()` - fetches AppSettings, calculates XP progress
- `RefreshAsync()` - reloads after XP gain

### 6.3 Add to MainLayout
**File**: `BookLoggerApp/Components/Layout/MainLayout.razor`

- Add UserProgressWidget to top-right header area
- Position next to existing "About" link or replace it
- Ensure it's visible on all pages

### 6.4 Create CSS
**File**: `BookLoggerApp/wwwroot/css/userprogress.css`

Styling:
- Compact badge with dark theme integration
- Gradient animated progress bar (use --gradient-warm)
- Hover effects (scale, glow)
- Mobile responsive (collapse to icon only on small screens)
- Smooth transitions

---

## Phase 7: Celebration Animations

### 7.1 Create SessionCompleteCelebration Component
**File**: `BookLoggerApp/Components/Shared/SessionCompleteCelebration.razor`

Fullscreen overlay showing:
- "Session Complete!" header with fade-in
- XP earned breakdown card:
  - Base XP (minutes × 5 + pages × 20)
  - Plant boost (e.g., "+25% from plants")
  - Total XP gained (large number, animated count-up)
- Animated progress bar showing XP addition
- Confetti particles (CSS animation)
- "Continue" button

Props:
```csharp
[Parameter] public ProgressionResult Result { get; set; }
[Parameter] public EventCallback OnClose { get; set; }
```

### 7.2 Create LevelUpCelebration Component
**File**: `BookLoggerApp/Components/Shared/LevelUpCelebration.razor`

Fullscreen overlay showing:
- "LEVEL UP!" with scale/pulse animation
- Level badge: "Level 4 → Level 5" with transition
- Coins awarded with coin icon spin animation
  - "You earned 250 coins!" (NewLevel × 50)
- Fireworks/particle effects (CSS keyframes)
- "Awesome!" / "Continue" button

Props:
```csharp
[Parameter] public LevelUpResult Result { get; set; }
[Parameter] public EventCallback OnClose { get; set; }
```

### 7.3 Create Animations CSS
**File**: `BookLoggerApp/wwwroot/css/celebrations.css`

Keyframe animations:
```css
@keyframes levelUpPulse - scale(1.0 → 1.2 → 1.0) + glow
@keyframes confettiFall - particles falling from top
@keyframes coinSpin - rotate 360deg + bounce
@keyframes numberCountUp - opacity + slight scale
@keyframes fireworkBurst - radial explosion
```

Effects:
- Backdrop blur + dark overlay (rgba(0,0,0,0.85))
- Smooth fade-in transitions
- Stagger animations (delay between elements)

### 7.4 Update ReadingViewModel
**File**: `BookLoggerApp.Core/ViewModels/ReadingViewModel.cs`

Add properties:
```csharp
[ObservableProperty]
private bool _showSessionCelebration;

[ObservableProperty]
private ProgressionResult? _sessionProgressionResult;

[ObservableProperty]
private bool _showLevelUpCelebration;

[ObservableProperty]
private LevelUpResult? _levelUpResult;
```

Modify `EndSessionCommand`:
1. Call EndSessionAsync (now returns ProgressionResult)
2. Set `SessionProgressionResult = result`
3. Set `ShowSessionCelebration = true`
4. If `result.LevelUp != null`:
   - After session celebration closed, show level-up
   - Set `LevelUpResult = result.LevelUp`
   - Set `ShowLevelUpCelebration = true`

### 7.5 Update Reading Page
**File**: `BookLoggerApp/Components/Pages/Reading.razor`

Add conditional rendering:
```razor
@if (ViewModel.ShowSessionCelebration && ViewModel.SessionProgressionResult != null)
{
    <SessionCompleteCelebration
        Result="@ViewModel.SessionProgressionResult"
        OnClose="@(() => ViewModel.ShowSessionCelebration = false)" />
}

@if (ViewModel.ShowLevelUpCelebration && ViewModel.LevelUpResult != null)
{
    <LevelUpCelebration
        Result="@ViewModel.LevelUpResult"
        OnClose="@(() => ViewModel.ShowLevelUpCelebration = false)" />
}
```

---

## Phase 8: Level Overview Page

### 8.1 Create LevelOverview Page
**File**: `BookLoggerApp/Components/Pages/LevelOverview.razor`

Route: `@page "/level-overview"`

Sections:

**1. Hero Section:**
- Large level badge (circular, gradient, animated)
- Current XP / Next Level XP
- Large progress bar

**2. Stats Grid (3 cards):**
- Total XP Earned (lifetime)
- Current Coins balance
- Next Level Unlocks (e.g., "Unlock at Level 10: Reading Cactus")

**3. Plant Boost Summary:**
- Title: "Your XP Boosters"
- List all owned plants:
  - Plant icon + name
  - Current level
  - Boost contribution (e.g., "Starter Sprout Lv.5: +10%")
- **Total Boost**: Large number showing cumulative percentage

**4. Level Milestones:**
- Visual timeline or list showing:
  - Levels achieved
  - Dates reached (optional, if tracking)
  - Coins earned per level

**5. Future Milestones:**
- Next 3-5 levels preview
- Show coin rewards
- Show plant unlocks

### 8.2 Create LevelOverviewViewModel
**File**: `BookLoggerApp.Core/ViewModels/LevelOverviewViewModel.cs`

Properties:
```csharp
int CurrentLevel
int TotalXp
int Coins
int CurrentLevelXp
int NextLevelXp
decimal ProgressPercentage

List<PlantBoostInfo> PlantBoosts // custom class with plant + boost info
decimal TotalBoostPercentage

List<LevelMilestone> CompletedLevels
List<LevelMilestone> UpcomingLevels
```

Methods:
- `LoadAsync()` - fetches all data
- Calculate boost breakdown per plant
- Generate milestone data

### 8.3 Create CSS
**File**: `BookLoggerApp/wwwroot/css/leveloverview.css`

Styling:
- Hero section: gradient background, large text, centered
- Stat cards: grid layout, card styling (existing --gradient-card)
- Plant boost list: each item with icon, name, level, boost percentage
- Progress bars: animated fill, gradient colors
- Timeline/milestone: vertical line connecting levels
- Responsive: stack on mobile

### 8.4 Add Navigation Link
**File**: `BookLoggerApp/Components/Shared/NavMenu.razor`

Add menu item:
```razor
<a href="/level-overview" class="nav-link">
    <span class="nav-icon">🏆</span>
    <span class="nav-text">Level</span>
</a>
```

Update CSS to accommodate new menu item

---

## Phase 9: Testing & Polish

### 9.1 Manual Testing Checklist
- [ ] Start reading session → earn XP with correct formula (5/min, 20/page)
- [ ] Complete session → see celebration with correct XP breakdown
- [ ] Level up → see celebration with correct coins (Level × 50)
- [ ] Multiple level-ups in one session (e.g., 1 → 3) → award coins for each level
- [ ] Complete book → earn 100 bonus XP
- [ ] Purchase first plant → costs BaseCost (500)
- [ ] Purchase second plant → costs BaseCost + 200 (700)
- [ ] Purchase third plant → costs BaseCost + 400 (900)
- [ ] Single plant → XP boost applied correctly
- [ ] Multiple plants → XP boost is cumulative
- [ ] Plant levels up → XP boost increases
- [ ] Progress bar in header updates in real-time
- [ ] Level overview page displays all data accurately
- [ ] Animations play smoothly without lag
- [ ] Click level widget → navigate to overview page

### 9.2 Edge Cases
- [ ] Level up multiple levels (skip levels) → award coins for all levels
- [ ] No active plant → no boost applied (0%)
- [ ] 0 pages read → earn time-only XP (5 XP/min)
- [ ] Very long sessions → XP scales correctly
- [ ] Session with streak bonus → bonus added on top
- [ ] Completing session after book marked complete → book bonus awarded once

### 9.3 UI Polish
- [ ] Add loading states to all async operations
- [ ] Add error handling with user-friendly messages
- [ ] Smooth transitions between animations (sequential, not overlapping)
- [ ] Mobile responsiveness (test on small screens)
- [ ] Dark mode integration (use existing CSS variables)
- [ ] Accessibility: ARIA labels, keyboard navigation, focus management
- [ ] Performance: ensure animations don't block UI thread

### 9.4 Data Integrity
- [ ] XP never negative
- [ ] Coins never negative
- [ ] Level always ≥ 1
- [ ] Plant boost percentage never negative
- [ ] Dynamic pricing always ≥ BaseCost

---

## Phase 10: Documentation

### 10.1 Update README (if exists)
- Add section on progression system
- Explain XP sources
- Explain coin earning
- Explain plant boosters

### 10.2 Code Comments
- Add XML documentation to new services
- Document complex formulas (XP boost calculation)
- Add comments to celebration animation sequences

### 10.3 Future Enhancements (Not in V2)
Ideas for V3:
- **Achievements System**: Badges for milestones (read 10 books, 100 hours, etc.)
- **Leaderboards**: Compare stats with friends (if multiplayer added)
- **Daily Challenges**: Bonus XP for completing goals
- **Rare Plants**: Special plants with unique abilities
- **Prestige System**: Reset level for permanent bonuses
- **Plant Customization**: Skins, names, decorations
- **Reading Streaks**: Visual streak counter with fire icons
- **XP Events**: Double XP weekends
- **Quest System**: Long-term goals with big rewards

---

## Implementation Order Summary

### Week 1: Backend Foundation (8-10 hours)
✅ Phase 1: Data models - Add XpBoostPercentage, PlantsPurchased, migrations
✅ Phase 2: XP calculation - Update constants, add boost formula
✅ Phase 3: ProgressionService - Create service, models, orchestration logic
✅ Phase 4: Plant boost & pricing - Boost calculation, dynamic pricing

### Week 2: Integration (4-5 hours)
✅ Phase 5: Session flow integration - Wire up ProgressionService calls

### Week 3: Frontend - Core UI (8-10 hours)
✅ Phase 6: User progress widget - Header component with progress bar
✅ Phase 7: Celebration animations - Session complete & level-up overlays

### Week 4: Frontend - Overview (6-8 hours)
✅ Phase 8: Level overview page - Comprehensive stats page

### Week 5: Polish & Documentation (6-10 hours)
✅ Phase 9: Testing & refinement - Bug fixes, edge cases, polish
✅ Phase 10: Documentation - Comments, README updates

**Total Estimated Time**: 32-43 hours

---

## Technical Decisions

### XP Formula Justification
- **5 XP/minute**: Rewards time investment, encourages longer sessions
- **20 XP/page**: Rewards actual reading progress, scales with book length
- **100 XP/book**: Significant reward for completion, encourages finishing books
- **Plant boost**: Creates gameplay loop (buy plants → boost XP → level faster → earn coins → buy more plants)

### Coin Formula (Level × 50)
- Progressive rewards: Higher levels feel more rewarding
- Level 1→2: 50 coins (10% of first plant)
- Level 10→11: 500 coins (enough for first plant)
- Level 20→21: 1000 coins (second plant after dynamic pricing)
- Encourages continued engagement

### Dynamic Plant Pricing (+200 per purchase)
- First plant (500) → achievable early
- Second plant (700) → requires 2-3 level-ups
- Third plant (900) → requires more commitment
- Prevents buying all plants immediately
- Creates sense of progression and earned rewards

### Cumulative Plant Boosts
- Rewards collection and diversity
- Encourages purchasing multiple plants
- Each plant level-up feels meaningful
- Can reach significant boosts (30-50%) with max-level collection
- Balanced by increasing XP requirements per level

### Fullscreen Celebrations
- Maximizes impact and satisfaction
- Forces player to acknowledge achievement
- Creates memorable "wow" moments
- Better than toast notifications for major events
- Ideal for mobile (fullscreen space)

---

## Success Criteria

✅ **Users earn XP at correct rates**
   - 5 XP per minute read
   - 20 XP per page read
   - 100 XP per book completed

✅ **Level-ups award correct coins**
   - Formula: NewLevel × 50 coins
   - Multiple levels award cumulative coins

✅ **Plant XP boosters work correctly**
   - Each plant contributes boost percentage
   - Boosts stack cumulatively
   - Plant level-ups increase boost

✅ **Plants become more expensive**
   - First plant: BaseCost
   - Each subsequent: +200 coins

✅ **Header shows level and XP**
   - Current level displayed
   - XP progress bar (current/next)
   - Clicking navigates to overview

✅ **Celebrations appear correctly**
   - Session complete: shows XP breakdown
   - Level-up: shows new level + coins
   - Animations play smoothly

✅ **Level overview provides insights**
   - Current stats (level, XP, coins)
   - Plant boost breakdown
   - Progression milestones
   - Future rewards preview

✅ **System feels rewarding**
   - Clear feedback after every action
   - Visible progress towards goals
   - Satisfying animations
   - Meaningful choices (which plants to buy)

---

## Conclusion

This V2 Progression System transforms BookLoggerApp from a simple reading tracker into an engaging, gamified experience. By combining XP rewards, level-based progression, strategic plant collection, and satisfying visual feedback, users will be motivated to read more, track consistently, and engage deeply with the app.

The system creates multiple gameplay loops:
1. **Short-term**: Read → Earn XP → See celebration
2. **Medium-term**: Level up → Earn coins → Buy plants
3. **Long-term**: Collect plants → Boost XP → Level faster → Unlock rare plants

Each interaction is rewarded, progress is visible, and achievements are celebrated. This creates a positive reinforcement cycle that encourages continued use and reading habit formation.

**Ready to make reading addictive! 📚✨**
