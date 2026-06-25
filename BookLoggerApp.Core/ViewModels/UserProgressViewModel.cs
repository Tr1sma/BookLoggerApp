using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>ViewModel for displaying user progression (level, XP).</summary>
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
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);

            TotalXp = settings.TotalXp;

            // Derive level from total XP; a stale stored level can push progress over 100%.
            CurrentLevel = XpCalculator.CalculateLevelFromXp(TotalXp);

            CalculateProgress();
        }, Tr("Error_FailedTo_LoadUserProgress"));
    }

    private void CalculateProgress()
    {
        int xpForPreviousLevels = 0;
        for (int i = 1; i < CurrentLevel; i++)
        {
            xpForPreviousLevels += XpCalculator.GetXpForLevel(i);
        }

        CurrentLevelXp = TotalXp - xpForPreviousLevels;
        NextLevelXp = XpCalculator.GetXpForLevel(CurrentLevel);

        if (NextLevelXp > 0)
        {
            ProgressPercentage = Math.Clamp((decimal)CurrentLevelXp / NextLevelXp * 100m, 0m, 100m);
        }
        else
        {
            ProgressPercentage = 0m;
        }
    }

    /// <summary>Refreshes the progress display (call after XP is earned).</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }
}
