using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;

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

            CurrentLevel = settings.UserLevel;
            TotalXp = settings.TotalXp;

            // Calculate XP for current level progress
            CalculateProgress();
        }, "Failed to load user progress");
    }

    private void CalculateProgress()
    {
        // Use exponential growth formula: Level N requires 100 * (1.5^(N-1)) XP
        // This matches the XpCalculator in Infrastructure

        // Calculate XP accumulated for current level
        int xpForPreviousLevels = 0;
        for (int i = 1; i < CurrentLevel; i++)
        {
            xpForPreviousLevels += GetXpForLevel(i);
        }

        CurrentLevelXp = TotalXp - xpForPreviousLevels;
        NextLevelXp = GetXpForLevel(CurrentLevel);

        // Calculate percentage (0-100)
        if (NextLevelXp > 0)
        {
            ProgressPercentage = (decimal)CurrentLevelXp / NextLevelXp * 100m;
        }
        else
        {
            ProgressPercentage = 0m;
        }
    }

    /// <summary>
    /// Calculate XP required for a specific level (matches XpCalculator logic).
    /// Exponential growth: Level 1 = 100 XP, Level 2 = 150 XP, Level 3 = 225 XP, etc.
    /// </summary>
    private static int GetXpForLevel(int level)
    {
        return (int)(100 * Math.Pow(1.5, level - 1));
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
