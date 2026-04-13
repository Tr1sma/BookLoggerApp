# XP Calculation Guide - BookLoggerApp V2 Progression System

## Overview

This document describes the complete XP calculation system used throughout the BookLoggerApp, including all formulas, bonuses, and progression mechanics.

---

## 📊 Base XP Formula

### Reading Session XP
```
Total XP = (Minutes × 5) + (Pages × 20) + Long Session Bonus + Streak Bonus
```

**Constants** (`XpCalculator.cs`):
- `XP_PER_MINUTE = 5`
- `XP_PER_PAGE = 20`
- `BONUS_XP_LONG_SESSION = 50` (for 60+ minute sessions)
- `BONUS_XP_STREAK = 20` (for 2+ day reading streaks)
- `XP_BOOK_COMPLETION = 100` (bonus for finishing a book)

### Example Calculation
**Scenario**: 45 minutes, 15 pages, with 3-day streak
```
Base XP = (45 × 5) + (15 × 20) + 0 + 20
        = 225 + 300 + 0 + 20
        = 545 XP (before plant boost)
```

---

## 🌱 Plant Boost System

### Formula
```
Boosted XP = Base XP × (1 + Total Plant Boost Percentage)
```

### Plant Boost Calculation
Each plant contributes a boost based on its species and level:
```
Plant Boost = Base Boost + (Current Level × (Base Boost / Max Level))
```

**Example** - Starter Sprout (5% base, max level 10):
- Level 1: 5% + (1 × 0.5%) = 5.5%
- Level 5: 5% + (5 × 0.5%) = 7.5%
- Level 10: 5% + (10 × 0.5%) = 10%

**Example** - Mystic Tome Tree (75% base, max level 25):
- Level 1: 75% + (1 × 3%) = 78%
- Level 10: 75% + (10 × 3%) = 105%
- Level 25: 75% + (25 × 3%) = 150%

### Plant Species XP Boosts

| Plant | Unlock Lv | Cost | Base Boost | Max Level | Boost at Max Lv |
|---|---|---|---|---|---|
| Starter Sprout | 1 | 500 | 5% | 10 | 10% |
| Story Seedling | 3 | 600 | 8% | 11 | 16% |
| Bookworm Fern | 8 | 750 | 12% | 12 | 24% |
| Literary Lily | 14 | 850 | 18% | 14 | 36% |
| Reading Cactus | 21 | 1000 | 25% | 15 | 50% |
| Wisdom Willow | 28 | 1500 | 35% | 18 | 70% |
| Ancient Knowledge Bonsai | 31 | 2500 | 50% | 20 | 100% |
| Mystic Tome Tree | 33 | 5000 | 75% | 25 | 150% |

### Multiple Plants
Total boost is the **sum** of all owned plants' boosts:
```
Total Boost = Plant1 Boost + Plant2 Boost + Plant3 Boost + ...
```

**Example** - 3 plants:
- Starter Sprout (Lv5): 7.5% boost
- Bookworm Fern (Lv3): 15% boost
- Wisdom Willow (Lv8): 50.6% boost
- **Total**: 73.1% boost

### Final XP with Boost
```
Final XP = 545 × (1 + 0.731) = 545 × 1.731 = 943 XP
```

---

## 🎖️ Level Progression

### Quadratic Growth Formula
```
XP Required for Level N = 100 × N²
```

### Level Requirements Table
| Level | XP Required (This Level) | Cumulative Total XP |
|-------|-------------------------|---------------------|
| 1     | 100                     | 0                   |
| 2     | 400                     | 100                 |
| 3     | 900                     | 500                 |
| 4     | 1,600                   | 1,400               |
| 5     | 2,500                   | 3,000               |
| 6     | 3,600                   | 5,500               |
| 7     | 4,900                   | 9,100               |
| 8     | 6,400                   | 14,000              |
| 9     | 8,100                   | 20,400              |
| 10    | 10,000                  | 28,500              |

### Calculating Level from Total XP
The system iterates through levels, subtracting required XP until insufficient XP remains:

```csharp
int level = 1;
int xpRequired = GetXpForLevel(level);

while (totalXp >= xpRequired)
{
    totalXp -= xpRequired;
    level++;
    xpRequired = GetXpForLevel(level);
}

return level; // Current level (in progress)
```

---

## 💰 Coin Rewards

### Level-Up Coins
```
Coins per Level = 50 × Level + 3 × Level²
```
Progressive scaling — higher levels award proportionally more coins to match quadratic XP growth.

**Examples**:
- Level 1 → 2: (50 × 2) + (3 × 4) = **112 coins**
- Level 5 → 6: (50 × 6) + (3 × 36) = **408 coins**
- Level 9 → 10: (50 × 10) + (3 × 100) = **800 coins**

### Multiple Level-Ups
If a user gains multiple levels at once, coins are awarded for **each** level:
```
Total Coins = Σ CalculateCoinsForLevel(Level) for each level gained
```

**Example**: Level 3 → Level 5
```
Coins = CalculateCoinsForLevel(4) + CalculateCoinsForLevel(5) = 248 + 325 = 573 coins
```

---

## 📝 Implementation Details

### Key Files

1. **`XpCalculator.cs`** (`BookLoggerApp.Core/Helpers/`)
   - Contains all base formulas and constants
   - Pure calculation logic, no state

2. **`ProgressionService.cs`** (`BookLoggerApp.Infrastructure/Services/`)
   - Orchestrates XP awards
   - Applies plant boosts
   - Handles level-ups and coin rewards
   - Saves to database

3. **`ProgressService.cs`** (`BookLoggerApp.Infrastructure/Services/`)
   - Manages reading sessions
   - Calculates streak status
   - Calls ProgressionService for XP awards
   - Awards plant XP (2 XP per minute)

### XP Flow During Reading Session

```
┌─────────────────────────┐
│ User Ends Reading       │
│ Session                 │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ ProgressService         │
│ EndSessionAsync()       │
│ • Calculate minutes     │
│ • Get pages read        │
│ • Check streak status   │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ ProgressionService      │
│ AwardSessionXpAsync()   │
│ • Calculate base XP     │
│ • Get plant boosts      │
│ • Apply boost           │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ Check Level-Up          │
│ • Calculate old level   │
│ • Calculate new level   │
│ • Award coins           │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ Return Results          │
│ • ProgressionResult     │
│   - XpEarned           │
│   - BaseXp             │
│   - BoostedXp          │
│   - LevelUp (if any)   │
└─────────────────────────┘
```

---

## 🔄 Streak System

### Streak Requirements
- **Active Streak**: User must read on consecutive days
- **Minimum for Bonus**: 2+ days in a row
- **Broken If**: More than 1 day gap between sessions

### Streak Detection
```csharp
private async Task<bool> HasReadingStreakAsync()
{
    var streak = await GetCurrentStreakAsync();
    return streak >= 2; // At least 2 days
}
```

### Streak Calculation
1. Get all sessions grouped by date
2. Start from today
3. Count backwards while days are consecutive
4. If gap > 1 day, streak ends

---

## 🎮 Live XP Preview (ReadingViewModel)

During an active reading session, users see an **estimated XP** that updates as they read:

```csharp
Estimated XP = (Minutes × 5) + (Pages × 20) + Long Session Bonus (if 60+ min)
```

**Note**: This preview does NOT include:
- Streak bonus (+20 XP)
- Plant boost percentage

These are applied only during the **final calculation** when the session ends, ensuring accurate rewards based on the exact session duration and current streak status.

---

## ✅ Data Integrity

### Safeguards

1. **Atomic Updates**: XP and level updates happen in a single transaction
2. **Level Recalculation**: Level is always calculated from total XP, never just incremented
3. **Coin Awards**: Only awarded during actual level-ups, never duplicated
4. **Streak Validation**: Recalculated on every session end based on historical data

### Potential Edge Cases

- **Timezone Issues**: All dates use `DateTime.UtcNow` for consistency
- **Concurrent Sessions**: The system doesn't prevent multiple active sessions (design choice)
- **Plant Deletion**: If a plant is deleted, total boost is recalculated from remaining plants
- **Negative XP**: Not possible; all XP calculations use Math.Max(0, ...) where needed

---

## 📈 Example Complete Flow

**Scenario**: User reads for 75 minutes, 20 pages, on day 4 of streak, with 50% total plant boost (e.g. Reading Cactus at max level)

### Step 1: Base Calculation
```
Minutes XP:        75 × 5   = 375
Pages XP:          20 × 20  = 400
Long Session:      1 × 50   = 50
Streak Bonus:      1 × 20   = 20
────────────────────────────────
Base XP:                    = 845
```

### Step 2: Apply Plant Boost
```
Boosted XP: 845 × (1 + 0.50) = 845 × 1.50 = 1267 XP
Bonus from Plants: 1267 - 845 = 422 XP
```

### Step 3: Add to User
```
Old Total XP: 450
New Total XP: 450 + 1267 = 1,717
```

### Step 4: Check Level-Up
```
Old Level: CalculateLevelFromXp(450) = Level 2
  → 450 - 100 (Lv1) = 350, 350 < 400 (Lv2) → Level 2

New Level: CalculateLevelFromXp(1,717) = Level 4
  → 1,717 - 100 (Lv1) = 1,617 - 400 (Lv2) = 1,217 - 900 (Lv3) = 317, 317 < 1,600 (Lv4) → Level 4

Levels Gained: 4 - 2 = 2 levels
```

### Step 5: Award Coins
```
Level 3 Coins: (50 × 3) + (3 × 9) = 177
Level 4 Coins: (50 × 4) + (3 × 16) = 248
────────────────────────────────────────
Total Coins:                        = 425
```

### Final Result
- **XP Earned**: 1267 (845 base + 422 plant bonus)
- **Level Up**: 2 → 4
- **Coins Earned**: 425

---

## 🐛 Bug Fixes (Phase 9)

### Issues Found and Fixed

1. **Missing Streak Parameter**
   - **Problem**: `ProgressionService.AwardSessionXpAsync` didn't accept streak parameter
   - **Fix**: Added `bool hasStreak = false` parameter
   - **Files**: `IProgressionService.cs`, `ProgressionService.cs`

2. **Streak Not Passed**
   - **Problem**: `ProgressService.EndSessionAsync` didn't check or pass streak status
   - **Fix**: Added `HasReadingStreakAsync()` call and passed to `AwardSessionXpAsync`
   - **File**: `ProgressService.cs:62-69`

3. **Incorrect Live XP Calculation**
   - **Problem**: `ReadingViewModel.UpdatePageAsync` used `pages × 2` instead of `pages × 20`
   - **Fix**: Corrected formula to match `XpCalculator` constants, added long session check
   - **File**: `ReadingViewModel.cs:179-192`

---

## 🎯 Testing Recommendations

### Manual Test Cases

1. **Basic Session**
   - Read for 10 minutes, 5 pages, no streak
   - Expected XP: (10×5) + (5×20) = 150 (before boost)

2. **Long Session Bonus**
   - Read for 65 minutes, 10 pages, no streak
   - Expected XP: (65×5) + (10×20) + 50 = 525 (before boost)

3. **Streak Bonus**
   - Read 3 days in a row, then 30 min, 8 pages on day 4
   - Expected XP: (30×5) + (8×20) + 20 = 330 (before boost)

4. **All Bonuses**
   - Read for 75 min, 15 pages, with 3-day streak
   - Expected XP: (75×5) + (15×20) + 50 + 20 = 745 (before boost)

5. **Plant Boost**
   - With 10% plant boost, 500 base XP
   - Expected: 500 × 1.10 = 550 XP

6. **Level-Up**
   - User at 95 XP (Level 1), earns 50 XP → total 145 XP
   - Expected: Level 2, +112 coins (CalculateCoinsForLevel(2))

7. **Multi-Level**
   - User at 2,900 XP (Level 4), earns 3,000 XP → total 5,900 XP
   - Expected: Level 6, +733 coins (CalculateCoinsForLevel(5) + CalculateCoinsForLevel(6))

---

## 📚 References

- **XpCalculator**: `BookLoggerApp.Core/Helpers/XpCalculator.cs`
- **ProgressionService**: `BookLoggerApp.Infrastructure/Services/ProgressionService.cs`
- **ProgressService**: `BookLoggerApp.Infrastructure/Services/ProgressService.cs`
- **ReadingViewModel**: `BookLoggerApp.Core/ViewModels/ReadingViewModel.cs`
- **StatsViewModel**: `BookLoggerApp.Core/ViewModels/StatsViewModel.cs` (Level overview calculations)

---

*Last Updated: April 2026 - Progressive coin reward formula (50×Level + 3×Level²)*
*Version: V2 Progression System*
