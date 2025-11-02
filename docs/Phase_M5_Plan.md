# Phase M5: Plant Mechanics & Gamification

**Zeitrahmen:** 2 Wochen (80 Stunden)
**Status:** Planned
**Dependencies:** M3 (IPlantService), M4 (UI Framework)
**Parallel zu:** M4 (kann teilweise parallel laufen)

---

## Überblick

In dieser Phase implementieren wir die **Pflanzenmechanik** als Gamification-Feature. User können virtuelle Pflanzen kaufen, pflegen und leveln, indem sie lesen. Dies ist ein MVP-Feature und zentral für die Motivation.

### Ziele

1. ✅ Plant Growth Algorithm implementieren
2. ✅ Plant Shop UI mit Kaufmechanik
3. ✅ Plant Widget auf Dashboard & Detail Pages
4. ✅ Watering Mechanics mit Reminder-Notifications
5. ✅ XP-Integration mit Reading Sessions
6. ✅ Plant Status-Updates (Healthy → Thirsty → Wilting)
7. ✅ Currency System (Coins)

### Deliverables

- Plant Growth Algorithm (Helper-Klasse)
- Plant Shop Page (UI)
- PlantWidget Component (Dashboard)
- Notification Service (Watering Reminders)
- Plant Images (Seed Data)
- Unit Tests für Plant Logic
- Integration Tests (Plant Service)

---

## Plant System Design

### Core Mechanics

#### 1. XP & Leveling
- **XP Source:** Reading Sessions (Minutes + Pages)
- **XP Calculation:** Siehe M3 (XpCalculator)
- **Leveling:** User Plant Level steigt mit gesammeltem XP
- **Formula:** XP für Level N = 100 * 1.5^(N-1)
  - Level 1: 0 XP
  - Level 2: 100 XP
  - Level 3: 250 XP (150 + 100)
  - Level 4: 500 XP (250 + 250)
  - Level 5: 1000 XP (500 + 500)

#### 2. Plant Species Attributes
Jede PlantSpecies hat:
- **MaxLevel:** Maximales Level (z.B. 10)
- **GrowthRate:** XP-Multiplikator (z.B. 1.2 = 20% schneller)
- **WaterIntervalDays:** Tage zwischen Gießen (z.B. 3)
- **BaseCost:** Kosten im Shop (Coins)

#### 3. Watering Mechanics
- **LastWatered:** Timestamp des letzten Gießens
- **Status-Berechnung:**
  - `Healthy`: LastWatered < WaterIntervalDays
  - `Thirsty`: LastWatered >= WaterIntervalDays und < WaterIntervalDays + 1
  - `Wilting`: LastWatered >= WaterIntervalDays + 1 und < WaterIntervalDays + 3
  - `Dead`: LastWatered >= WaterIntervalDays + 3
- **Watering:**
  - Setzt LastWatered auf Now
  - Ändert Status zurück zu `Healthy`
  - Tote Pflanzen können nicht wiederbelebt werden

#### 4. Currency System
- **Coins:** Virtuelle Währung zum Kauf von Pflanzen/Themes
- **Verdienen:**
  - Tägliche Belohnung: 10 Coins (beim ersten Read des Tages)
  - Goal Completion: 50-200 Coins (je nach Goal)
  - Level Up: 100 Coins
  - Streak Bonus: 5 Coins/Tag (ab 7 Tage Streak)
- **Ausgeben:**
  - Plant Species kaufen (100-2000 Coins)
  - Themes kaufen (500 Coins, optional)

---

## Arbeitspaket 1: Plant Growth Algorithm

**Aufwand:** 10 Stunden
**Priorität:** P0

### 1.1 PlantGrowthCalculator Helper

**Location:** `BookLoggerApp.Infrastructure/Services/Helpers/PlantGrowthCalculator.cs`

```csharp
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Services.Helpers;

/// <summary>
/// Helper for calculating plant growth and XP.
/// </summary>
public static class PlantGrowthCalculator
{
    /// <summary>
    /// Calculate XP required to reach a specific level.
    /// </summary>
    public static int GetXpForLevel(int level, double growthRate = 1.0)
    {
        // Base formula: 100 * 1.5^(level-1)
        var baseXp = (int)(100 * Math.Pow(1.5, level - 1));
        return (int)(baseXp / growthRate); // Lower if growthRate > 1 (faster growth)
    }

    /// <summary>
    /// Calculate total XP needed to reach a level from level 1.
    /// </summary>
    public static int GetTotalXpForLevel(int level, double growthRate = 1.0)
    {
        int totalXp = 0;
        for (int i = 2; i <= level; i++)
        {
            totalXp += GetXpForLevel(i, growthRate);
        }
        return totalXp;
    }

    /// <summary>
    /// Calculate current level based on total XP.
    /// </summary>
    public static int CalculateLevelFromXp(int totalXp, double growthRate = 1.0, int maxLevel = 100)
    {
        int level = 1;
        int xpRequired = GetXpForLevel(level + 1, growthRate);

        while (totalXp >= xpRequired && level < maxLevel)
        {
            totalXp -= xpRequired;
            level++;
            xpRequired = GetXpForLevel(level + 1, growthRate);
        }

        return level;
    }

    /// <summary>
    /// Calculate XP needed for next level.
    /// </summary>
    public static int GetXpToNextLevel(int currentLevel, int currentXp, double growthRate = 1.0)
    {
        int totalXpForCurrentLevel = GetTotalXpForLevel(currentLevel, growthRate);
        int totalXpForNextLevel = GetTotalXpForLevel(currentLevel + 1, growthRate);
        int xpIntoCurrentLevel = currentXp - totalXpForCurrentLevel;

        return (totalXpForNextLevel - totalXpForCurrentLevel) - xpIntoCurrentLevel;
    }

    /// <summary>
    /// Calculate plant status based on last watered date.
    /// </summary>
    public static PlantStatus CalculatePlantStatus(DateTime lastWatered, int waterIntervalDays)
    {
        var daysSinceWatered = (DateTime.UtcNow - lastWatered).TotalDays;

        if (daysSinceWatered < waterIntervalDays)
            return PlantStatus.Healthy;
        else if (daysSinceWatered < waterIntervalDays + 1)
            return PlantStatus.Thirsty;
        else if (daysSinceWatered < waterIntervalDays + 3)
            return PlantStatus.Wilting;
        else
            return PlantStatus.Dead;
    }

    /// <summary>
    /// Check if plant needs watering soon.
    /// </summary>
    public static bool NeedsWateringSoon(DateTime lastWatered, int waterIntervalDays)
    {
        var hoursSinceWatered = (DateTime.UtcNow - lastWatered).TotalHours;
        var hoursUntilThirsty = waterIntervalDays * 24;

        // Return true if within 6 hours of becoming thirsty
        return hoursSinceWatered >= (hoursUntilThirsty - 6);
    }
}
```

### 1.2 Unit Tests

**Location:** `BookLoggerApp.Tests/Infrastructure/Services/Helpers/PlantGrowthCalculatorTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Tests.Infrastructure.Services.Helpers;

public class PlantGrowthCalculatorTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 150)]
    [InlineData(3, 225)]
    [InlineData(4, 338)]
    public void GetXpForLevel_ShouldReturnCorrectXp(int level, int expectedXp)
    {
        // Act
        var xp = PlantGrowthCalculator.GetXpForLevel(level);

        // Assert
        xp.Should().BeCloseTo(expectedXp, 1);
    }

    [Fact]
    public void CalculateLevelFromXp_ShouldReturnCorrectLevel()
    {
        // Arrange
        int totalXp = 250; // Should be level 2 (100 XP to level 2, 150 more to level 3)

        // Act
        var level = PlantGrowthCalculator.CalculateLevelFromXp(totalXp);

        // Assert
        level.Should().Be(2);
    }

    [Fact]
    public void CalculatePlantStatus_Healthy_WhenRecentlyWatered()
    {
        // Arrange
        var lastWatered = DateTime.UtcNow.AddDays(-1);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Healthy);
    }

    [Fact]
    public void CalculatePlantStatus_Thirsty_WhenOverdue()
    {
        // Arrange
        var lastWatered = DateTime.UtcNow.AddDays(-3.5);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Thirsty);
    }

    [Fact]
    public void CalculatePlantStatus_Dead_WhenTooLate()
    {
        // Arrange
        var lastWatered = DateTime.UtcNow.AddDays(-7);
        int waterIntervalDays = 3;

        // Act
        var status = PlantGrowthCalculator.CalculatePlantStatus(lastWatered, waterIntervalDays);

        // Assert
        status.Should().Be(PlantStatus.Dead);
    }
}
```

### Acceptance Criteria

- [ ] PlantGrowthCalculator implementiert
- [ ] XP-Berechnungen korrekt (Level, Total XP, XP to Next Level)
- [ ] Plant Status-Berechnung korrekt
- [ ] Unit Tests ≥90% Coverage

---

## Arbeitspaket 2: Plant Service Implementation (Erweitert)

**Aufwand:** 10 Stunden
**Priorität:** P0

### 2.1 IPlantService Implementation

**Location:** `BookLoggerApp.Infrastructure/Services/PlantService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

public class PlantService : IPlantService
{
    private readonly IRepository<UserPlant> _userPlantRepository;
    private readonly IRepository<PlantSpecies> _speciesRepository;
    private readonly IRepository<AppSettings> _settingsRepository;
    private readonly AppDbContext _context;

    public PlantService(
        IRepository<UserPlant> userPlantRepository,
        IRepository<PlantSpecies> speciesRepository,
        IRepository<AppSettings> settingsRepository,
        AppDbContext context)
    {
        _userPlantRepository = userPlantRepository;
        _speciesRepository = speciesRepository;
        _settingsRepository = settingsRepository;
        _context = context;
    }

    public async Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default)
    {
        var species = await _speciesRepository.GetByIdAsync(speciesId, ct);
        if (species == null)
            throw new ArgumentException("Species not found", nameof(speciesId));

        var settings = await _settingsRepository.FirstOrDefaultAsync(_ => true, ct);
        if (settings == null || settings.Coins < species.BaseCost)
            throw new InvalidOperationException("Not enough coins");

        // Deduct coins
        settings.Coins -= species.BaseCost;
        await _settingsRepository.UpdateAsync(settings, ct);

        // Create plant
        var plant = new UserPlant
        {
            SpeciesId = speciesId,
            Name = name,
            CurrentLevel = 1,
            Experience = 0,
            Status = PlantStatus.Healthy,
            LastWatered = DateTime.UtcNow,
            PlantedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Deactivate other plants
        var otherPlants = await _userPlantRepository.GetAllAsync(ct);
        foreach (var p in otherPlants)
        {
            p.IsActive = false;
            await _userPlantRepository.UpdateAsync(p, ct);
        }

        return await _userPlantRepository.AddAsync(plant, ct);
    }

    public async Task<UserPlant> WaterPlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _context.UserPlants
            .Include(p => p.Species)
            .FirstOrDefaultAsync(p => p.Id == plantId, ct);

        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot water a dead plant");

        plant.LastWatered = DateTime.UtcNow;
        plant.Status = PlantGrowthCalculator.CalculatePlantStatus(plant.LastWatered, plant.Species.WaterIntervalDays);

        await _userPlantRepository.UpdateAsync(plant, ct);
        return plant;
    }

    public async Task<UserPlant> AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default)
    {
        var plant = await _context.UserPlants
            .Include(p => p.Species)
            .FirstOrDefaultAsync(p => p.Id == plantId, ct);

        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        plant.Experience += xp;

        // Check for level up
        var newLevel = PlantGrowthCalculator.CalculateLevelFromXp(plant.Experience, plant.Species.GrowthRate, plant.Species.MaxLevel);
        if (newLevel > plant.CurrentLevel)
        {
            plant.CurrentLevel = newLevel;

            // Reward coins for level up
            var settings = await _settingsRepository.FirstOrDefaultAsync(_ => true, ct);
            if (settings != null)
            {
                settings.Coins += 100;
                await _settingsRepository.UpdateAsync(settings, ct);
            }
        }

        await _userPlantRepository.UpdateAsync(plant, ct);
        return plant;
    }

    public async Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var plants = await _userPlantRepository.GetAllAsync(ct);
        foreach (var plant in plants)
        {
            plant.IsActive = (plant.Id == plantId);
            await _userPlantRepository.UpdateAsync(plant, ct);
        }
    }

    public async Task<IReadOnlyList<UserPlant>> GetUserPlantsAsync(CancellationToken ct = default)
    {
        return await _context.UserPlants
            .Include(p => p.Species)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.PlantedAt)
            .ToListAsync(ct);
    }

    public async Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        return await _context.UserPlants
            .Include(p => p.Species)
            .FirstOrDefaultAsync(p => p.IsActive, ct);
    }

    public async Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        return await _speciesRepository.FindAsync(
            s => s.IsAvailable && s.UnlockLevel <= userLevel,
            ct);
    }

    public async Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        var plants = await _context.UserPlants.Include(p => p.Species).ToListAsync(ct);

        foreach (var plant in plants)
        {
            var newStatus = PlantGrowthCalculator.CalculatePlantStatus(plant.LastWatered, plant.Species.WaterIntervalDays);
            if (plant.Status != newStatus)
            {
                plant.Status = newStatus;
                await _userPlantRepository.UpdateAsync(plant, ct);
            }
        }
    }
}
```

### 2.2 Integration with ProgressService

**Update:** `ProgressService.AddSessionAsync` (aus M3)

```csharp
public async Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
{
    // Calculate XP
    var hasStreak = await HasReadingStreakAsync(ct);
    session.XpEarned = XpCalculator.CalculateXpForSession(session.Minutes, session.PagesRead, hasStreak);

    var result = await _sessionRepository.AddAsync(session, ct);

    // Add XP to active plant
    var plantService = _serviceProvider.GetRequiredService<IPlantService>();
    var activePlant = await plantService.GetActivePlantAsync(ct);
    if (activePlant != null)
    {
        await plantService.AddExperienceAsync(activePlant.Id, session.XpEarned, ct);
    }

    // Daily coin reward
    await CheckAndAwardDailyCoinsAsync(ct);

    return result;
}

private async Task CheckAndAwardDailyCoinsAsync(CancellationToken ct)
{
    var settings = await _context.AppSettings.FirstOrDefaultAsync(ct);
    if (settings == null) return;

    var today = DateTime.UtcNow.Date;
    var lastSessionToday = await _sessionRepository.FirstOrDefaultAsync(s => s.StartedAt.Date == today, ct);

    // If this is the first session today, award daily coins
    if (lastSessionToday != null && lastSessionToday.StartedAt.Date == today)
    {
        settings.Coins += 10;
        await _context.SaveChangesAsync(ct);
    }
}
```

### Acceptance Criteria

- [ ] PlantService vollständig implementiert
- [ ] XP-Integration mit Reading Sessions
- [ ] Coin-System funktioniert (Earn & Spend)
- [ ] Unit Tests für Plant Service

---

## Arbeitspaket 3: Plant Shop UI

**Aufwand:** 12 Stunden
**Priorität:** P0

### 3.1 PlantShopViewModel

**Location:** `BookLoggerApp.Core/ViewModels/PlantShopViewModel.cs`

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class PlantShopViewModel : ViewModelBase
{
    private readonly IPlantService _plantService;
    private readonly IRepository<AppSettings> _settingsRepository;

    public PlantShopViewModel(IPlantService plantService, IRepository<AppSettings> settingsRepository)
    {
        _plantService = plantService;
        _settingsRepository = settingsRepository;
    }

    [ObservableProperty]
    private ObservableCollection<PlantSpecies> _availableSpecies = new();

    [ObservableProperty]
    private int _userCoins;

    [ObservableProperty]
    private int _userLevel;

    [ObservableProperty]
    private string _newPlantName = "";

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var settings = await _settingsRepository.FirstOrDefaultAsync(_ => true);
            if (settings != null)
            {
                UserCoins = settings.Coins;
                UserLevel = settings.UserLevel;
            }

            var species = await _plantService.GetAvailableSpeciesAsync(UserLevel);
            AvailableSpecies = new ObservableCollection<PlantSpecies>(species);
        }, "Failed to load shop");
    }

    [RelayCommand]
    public async Task PurchasePlantAsync(Guid speciesId)
    {
        if (string.IsNullOrWhiteSpace(NewPlantName))
        {
            SetError("Please enter a name for your plant");
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.PurchasePlantAsync(speciesId, NewPlantName);
            await LoadAsync(); // Reload to update coins
        }, "Failed to purchase plant");
    }
}
```

### 3.2 PlantShop.razor

**Location:** `BookLoggerApp/Components/Pages/PlantShop.razor`

```razor
@page "/shop"
@using BookLoggerApp.Core.ViewModels
@inject PlantShopViewModel ViewModel
@inject NavigationManager Navigation

<PageTitle>Plant Shop - BookLogger</PageTitle>

<div class="plant-shop-container">
    <div class="shop-header">
        <h1>🌱 Plant Shop</h1>
        <div class="user-coins">
            <span class="coin-icon">🪙</span>
            <span class="coin-count">@ViewModel.UserCoins Coins</span>
        </div>
    </div>

    @if (ViewModel.IsBusy)
    {
        <div class="loading">Loading shop...</div>
    }
    else if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
    {
        <div class="alert alert-danger">@ViewModel.ErrorMessage</div>
    }
    else
    {
        <div class="shop-grid">
            @foreach (var species in ViewModel.AvailableSpecies)
            {
                <PlantShopCard Species="@species"
                               UserCoins="@ViewModel.UserCoins"
                               OnPurchase="() => HandlePurchase(species.Id)" />
            }
        </div>
    }
</div>

@code {
    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async Task HandlePurchase(Guid speciesId)
    {
        // Show name input dialog (TODO: Modal)
        ViewModel.NewPlantName = "My Plant"; // Temporary
        await ViewModel.PurchasePlantCommand.ExecuteAsync(speciesId);
    }
}
```

### 3.3 PlantShopCard Component

**Location:** `BookLoggerApp/Components/Shared/PlantShopCard.razor`

```razor
<div class="plant-shop-card">
    <img src="@Species.ImagePath" alt="@Species.Name" class="plant-image" />
    <div class="plant-info">
        <h3>@Species.Name</h3>
        <p class="plant-description">@Species.Description</p>
        <div class="plant-stats">
            <div class="stat">
                <span class="stat-label">Max Level:</span>
                <span class="stat-value">@Species.MaxLevel</span>
            </div>
            <div class="stat">
                <span class="stat-label">Water Interval:</span>
                <span class="stat-value">@Species.WaterIntervalDays days</span>
            </div>
            <div class="stat">
                <span class="stat-label">Growth Rate:</span>
                <span class="stat-value">@Species.GrowthRate.ToString("0.0")x</span>
            </div>
        </div>
        <div class="plant-purchase">
            <span class="plant-cost">🪙 @Species.BaseCost Coins</span>
            @if (UserCoins >= Species.BaseCost)
            {
                <button class="btn btn-primary" @onclick="OnPurchase.InvokeAsync">
                    Purchase
                </button>
            }
            else
            {
                <button class="btn btn-secondary" disabled>
                    Not Enough Coins
                </button>
            }
        </div>
    </div>
</div>

@code {
    [Parameter] public PlantSpecies Species { get; set; } = null!;
    [Parameter] public int UserCoins { get; set; }
    [Parameter] public EventCallback OnPurchase { get; set; }
}
```

### Acceptance Criteria

- [ ] Plant Shop Page mit Grid Layout
- [ ] PlantShopCard zeigt Species-Details
- [ ] Purchase funktioniert (Coins abziehen, Plant erstellen)
- [ ] Locked Plants anzeigen (wenn User Level zu niedrig)

---

## Arbeitspaket 4: Plant Widget (Dashboard & Details)

**Aufwand:** 10 Stunden
**Priorität:** P0

### 4.1 PlantWidget Component

**Location:** `BookLoggerApp/Components/Shared/PlantWidget.razor`

```razor
<div class="plant-widget @GetStatusClass()">
    <div class="plant-visual">
        <img src="@GetPlantImage()" alt="@Plant.Name" class="plant-image-large" />
        <div class="plant-status-indicator">@GetStatusEmoji()</div>
    </div>
    <div class="plant-info">
        <h3 class="plant-name">@Plant.Name</h3>
        <p class="plant-species">@Plant.Species.Name</p>

        <div class="plant-level">
            <span class="level-badge">Level @Plant.CurrentLevel / @Plant.Species.MaxLevel</span>
        </div>

        <div class="plant-xp">
            <div class="xp-bar">
                <div class="xp-fill" style="width: @GetXpPercentage()%"></div>
            </div>
            <p class="xp-text">XP: @Plant.Experience / @GetNextLevelXp() (@GetXpPercentage()%)</p>
        </div>

        <div class="plant-watering">
            <p class="watering-status">@GetWateringStatus()</p>
            @if (Plant.Status != PlantStatus.Dead)
            {
                <button class="btn btn-water" @onclick="OnWater.InvokeAsync" disabled="@(Plant.Status == PlantStatus.Healthy)">
                    💧 Water Plant
                </button>
            }
            else
            {
                <p class="plant-dead-message">😢 Your plant has died. Purchase a new one from the shop!</p>
            }
        </div>
    </div>
</div>

@code {
    [Parameter] public UserPlant Plant { get; set; } = null!;
    [Parameter] public EventCallback OnWater { get; set; }

    private string GetPlantImage()
    {
        // Could be level-dependent (e.g., different sprites for levels 1-5, 6-10)
        return Plant.Species.ImagePath;
    }

    private string GetStatusClass()
    {
        return $"status-{Plant.Status.ToString().ToLower()}";
    }

    private string GetStatusEmoji()
    {
        return Plant.Status switch
        {
            PlantStatus.Healthy => "😊",
            PlantStatus.Thirsty => "😰",
            PlantStatus.Wilting => "😵",
            PlantStatus.Dead => "💀",
            _ => ""
        };
    }

    private string GetWateringStatus()
    {
        var daysSince = (DateTime.UtcNow - Plant.LastWatered).TotalDays;
        return Plant.Status switch
        {
            PlantStatus.Healthy => $"Last watered {(int)daysSince} day(s) ago. Doing great!",
            PlantStatus.Thirsty => "Needs water soon!",
            PlantStatus.Wilting => "⚠️ Needs water urgently!",
            PlantStatus.Dead => "😢 Plant has died.",
            _ => ""
        };
    }

    private int GetNextLevelXp()
    {
        return PlantGrowthCalculator.GetTotalXpForLevel(Plant.CurrentLevel + 1, Plant.Species.GrowthRate);
    }

    private int GetXpPercentage()
    {
        int totalXpForCurrent = PlantGrowthCalculator.GetTotalXpForLevel(Plant.CurrentLevel, Plant.Species.GrowthRate);
        int totalXpForNext = GetNextLevelXp();
        int xpIntoCurrentLevel = Plant.Experience - totalXpForCurrent;
        int xpNeededForLevel = totalXpForNext - totalXpForCurrent;

        if (xpNeededForLevel == 0) return 100;
        return (xpIntoCurrentLevel * 100) / xpNeededForLevel;
    }
}
```

### 4.2 CSS for PlantWidget

```css
.plant-widget {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    border-radius: 16px;
    padding: 2rem;
    color: white;
    display: flex;
    gap: 2rem;
    align-items: center;
    box-shadow: 0 4px 16px rgba(0,0,0,0.2);
}

.plant-widget.status-thirsty {
    background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
}

.plant-widget.status-wilting {
    background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
}

.plant-widget.status-dead {
    background: linear-gradient(135deg, #636363 0%, #a2ab58 100%);
    filter: grayscale(80%);
}

.plant-visual {
    position: relative;
}

.plant-image-large {
    width: 150px;
    height: 150px;
    object-fit: contain;
}

.plant-status-indicator {
    position: absolute;
    bottom: -10px;
    right: -10px;
    font-size: 2rem;
    background: white;
    border-radius: 50%;
    width: 50px;
    height: 50px;
    display: flex;
    align-items: center;
    justify-content: center;
}

.plant-info {
    flex: 1;
}

.plant-name {
    margin: 0 0 0.5rem 0;
}

.plant-species {
    opacity: 0.9;
    margin: 0 0 1rem 0;
}

.level-badge {
    background: rgba(255,255,255,0.2);
    padding: 0.25rem 0.75rem;
    border-radius: 12px;
    font-size: 0.875rem;
}

.xp-bar {
    height: 12px;
    background: rgba(0,0,0,0.2);
    border-radius: 6px;
    overflow: hidden;
    margin: 0.5rem 0;
}

.xp-fill {
    height: 100%;
    background: linear-gradient(90deg, #4CAF50, #8BC34A);
    transition: width 0.5s;
}

.xp-text {
    font-size: 0.875rem;
    opacity: 0.9;
}

.btn-water {
    background: rgba(255,255,255,0.2);
    border: 2px solid white;
    color: white;
    padding: 0.75rem 1.5rem;
    margin-top: 1rem;
}

.btn-water:hover:not(:disabled) {
    background: rgba(255,255,255,0.3);
}

.btn-water:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}
```

### Acceptance Criteria

- [ ] PlantWidget Component erstellt
- [ ] Zeigt Plant Level, XP, Status
- [ ] Water Button funktioniert
- [ ] Visuelle Unterschiede je nach Status
- [ ] Integration in Dashboard

---

## Arbeitspaket 5: Notifications (Watering Reminders)

**Aufwand:** 12 Stunden
**Priorität:** P1

### 5.1 INotificationService Implementation

**Location:** `BookLoggerApp.Infrastructure/Services/NotificationService.cs`

```csharp
using Plugin.LocalNotification;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

public class NotificationService : INotificationService
{
    public async Task ScheduleReadingReminderAsync(TimeSpan time, CancellationToken ct = default)
    {
        var notification = new NotificationRequest
        {
            NotificationId = 1,
            Title = "Time to Read!",
            Description = "Don't forget your daily reading session 📚",
            ReturningData = "ReadingReminder",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = DateTime.Today.Add(time),
                RepeatType = NotificationRepeat.Daily
            }
        };

        await LocalNotificationCenter.Current.Show(notification);
    }

    public async Task CancelReadingReminderAsync(CancellationToken ct = default)
    {
        await LocalNotificationCenter.Current.Cancel(1);
    }

    public async Task SendGoalCompletedNotificationAsync(string goalTitle, CancellationToken ct = default)
    {
        var notification = new NotificationRequest
        {
            NotificationId = 100 + Random.Shared.Next(1000),
            Title = "🎯 Goal Completed!",
            Description = $"You completed: {goalTitle}",
            ReturningData = "GoalCompleted"
        };

        await LocalNotificationCenter.Current.Show(notification);
    }

    public async Task SendPlantNeedsWaterNotificationAsync(string plantName, CancellationToken ct = default)
    {
        var notification = new NotificationRequest
        {
            NotificationId = 200 + Random.Shared.Next(1000),
            Title = "💧 Your Plant is Thirsty!",
            Description = $"{plantName} needs watering. Don't let it wilt!",
            ReturningData = "PlantWater"
        };

        await LocalNotificationCenter.Current.Show(notification);
    }
}
```

### 5.2 Background Task (Plant Status Check)

**MauiProgram.cs:** Register Background Task (Android)

```csharp
// Register Notification Service
builder.Services.AddSingleton<INotificationService, NotificationService>();

// Background Task (Optional: Use WorkManager on Android)
// Check plant status daily and send notifications
```

**Simplified Approach:** Check plant status when app opens (App.xaml.cs)

```csharp
protected override async void OnStart()
{
    base.OnStart();

    // ... DB Migration ...

    // Check plant statuses
    using var scope = Handler?.MauiContext?.Services?.CreateScope();
    if (scope != null)
    {
        var plantService = scope.ServiceProvider.GetRequiredService<IPlantService>();
        await plantService.UpdatePlantStatusesAsync();

        var activePlant = await plantService.GetActivePlantAsync();
        if (activePlant != null && (activePlant.Status == PlantStatus.Thirsty || activePlant.Status == PlantStatus.Wilting))
        {
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notificationService.SendPlantNeedsWaterNotificationAsync(activePlant.Name);
        }
    }
}
```

### 5.3 NuGet Package

```bash
dotnet add package Plugin.LocalNotification --version 11.0.0
```

### Acceptance Criteria

- [ ] Notification Service implementiert
- [ ] Plant Watering Reminders funktionieren
- [ ] Goal Completed Notifications
- [ ] Reading Reminders (optional)
- [ ] User kann Notifications in Settings aktivieren/deaktivieren

---

## Arbeitspaket 6: Plant Images & Assets

**Aufwand:** 8 Stunden
**Priorität:** P1

### 6.1 Plant Images

**Location:** `BookLoggerApp/wwwroot/images/plants/`

**Benötigte Assets:**
- `starter_sprout.png` (Level 1-5)
- `bookworm_fern.png` (Level 1-10)
- `reading_cactus.png` (Level 1-15)
- Placeholder: `plant_placeholder.png`

**Optionen:**
1. **Eigene Pixel Art** (16x16 oder 32x32 Sprites)
2. **Free Assets** (itch.io, OpenGameArt.org)
3. **Emoji-based** (Fallback: 🌱🌿🌳 als Text)

**Empfehlung für MVP:** Emoji-based oder simple SVG Icons

### 6.2 Seed Data aktualisieren

**AppDbContext.cs:** Plant Images Paths eintragen

```csharp
modelBuilder.Entity<PlantSpecies>().HasData(
    new PlantSpecies
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Name = "Starter Sprout",
        Description = "A simple plant for beginners. Grows quickly!",
        ImagePath = "/images/plants/starter_sprout.png",
        MaxLevel = 5,
        WaterIntervalDays = 2,
        GrowthRate = 1.0,
        BaseCost = 0, // Free
        UnlockLevel = 1
    },
    new PlantSpecies
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
        Name = "Bookworm Fern",
        Description = "A lush fern that grows with every page.",
        ImagePath = "/images/plants/bookworm_fern.png",
        MaxLevel = 10,
        WaterIntervalDays = 3,
        GrowthRate = 1.2,
        BaseCost = 500,
        UnlockLevel = 5
    },
    new PlantSpecies
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
        Name = "Reading Cactus",
        Description = "Low maintenance, high rewards.",
        ImagePath = "/images/plants/reading_cactus.png",
        MaxLevel = 15,
        WaterIntervalDays = 7,
        GrowthRate = 0.8,
        BaseCost = 1000,
        UnlockLevel = 10
    }
);
```

### Acceptance Criteria

- [ ] 3+ Plant Images vorhanden
- [ ] Images in wwwroot/images/plants/
- [ ] Seed Data aktualisiert mit korrekten Pfaden
- [ ] Placeholder für fehlende Images

---

## Arbeitspaket 7: Integration Tests

**Aufwand:** 10 Stunden
**Priorität:** P1

### 7.1 PlantService Integration Tests

**Location:** `BookLoggerApp.Tests/Integration/PlantServiceTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.Infrastructure;

namespace BookLoggerApp.Tests.Integration;

public class PlantServiceTests
{
    [Fact]
    public async Task PurchasePlant_ShouldDeductCoinsAndCreatePlant()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("PlantPurchase");
        // ... setup species, settings ...

        var service = new PlantService(...);

        // Act
        var plant = await service.PurchasePlantAsync(speciesId, "My Plant");

        // Assert
        plant.Should().NotBeNull();
        plant.Name.Should().Be("My Plant");
        // ... assert coins deducted
    }

    [Fact]
    public async Task AddExperience_ShouldLevelUpWhenThresholdReached()
    {
        // ... test level up logic
    }

    [Fact]
    public async Task WaterPlant_ShouldResetStatusToHealthy()
    {
        // ... test watering
    }
}
```

### Acceptance Criteria

- [ ] Integration Tests für PlantService
- [ ] Tests für Purchase, Water, AddXP, LevelUp
- [ ] Test Coverage ≥80% für Plant Logic

---

## Definition of Done (M5)

- [x] Plant Growth Algorithm implementiert und getestet
- [x] PlantService vollständig implementiert
- [x] Plant Shop UI mit Purchase-Mechanik
- [x] PlantWidget auf Dashboard & Details Pages
- [x] Watering Mechanics mit Status-Updates
- [x] Notifications für Plant Watering
- [x] XP-Integration mit Reading Sessions
- [x] Currency System (Earn & Spend Coins)
- [x] Plant Images & Assets vorhanden
- [x] Unit Tests ≥90% Coverage für Plant Logic
- [x] Integration Tests für PlantService
- [x] Manual Testing (All Plant Flows)
- [x] CI-Pipeline grün

---

**Ende Phase M5 Plan**
