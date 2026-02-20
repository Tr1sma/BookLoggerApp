using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// ViewModel for displaying user progression (level, XP) in the UI.
/// </summary>
public partial class UserProgressViewModel : ViewModelBase
{
    private readonly IAppSettingsProvider _settingsProvider;

    public UserProgressViewModel(IAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    [ObservableProperty]
    private int _currentLevel = 1;

    [ObservableProperty]
    private int _totalXp = 0;

    [ObservableProperty]
    private int _currentLevelXp = 0; // XP accumulated in current level

    [ObservableProperty]
    private int _nextLevelXp = 100; // XP needed for next level

    [ObservableProperty]
    private decimal _progressPercentage = 0m; // 0-100

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            var settings = await _settingsProvider.GetSettingsAsync();

            TotalXp = settings.TotalXp;

            // Recalculate level from total XP to ensure consistency
            // This fixes the bug where progress > 100% if stored level is stale
            CurrentLevel = XpCalculator.CalculateLevelFromXp(TotalXp);

            // Calculate XP for current level progress
            CalculateProgress();
        }, "Failed to load user progress");
    }

    private void CalculateProgress()
    {
        // Calculate XP accumulated for current level
        int xpForPreviousLevels = 0;
        for (int i = 1; i < CurrentLevel; i++)
        {
            xpForPreviousLevels += XpCalculator.GetXpForLevel(i);
        }

        CurrentLevelXp = TotalXp - xpForPreviousLevels;
        NextLevelXp = XpCalculator.GetXpForLevel(CurrentLevel);

        // Calculate percentage (0-100), clamped to valid range
        if (NextLevelXp > 0)
        {
            ProgressPercentage = Math.Clamp((decimal)CurrentLevelXp / NextLevelXp * 100m, 0m, 100m);
        }
        else
        {
            ProgressPercentage = 0m;
        }
    }

    /// <summary>
    /// Calculate XP required for a specific level (matches XpCalculator logic).
    /// </summary>
    private static int GetXpForLevel(int level)
    {
        return XpCalculator.GetXpForLevel(level);
    }

    /// <summary>
    /// Refresh the progress display (call after XP is earned).
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }
}
