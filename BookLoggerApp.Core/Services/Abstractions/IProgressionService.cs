using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing user progression (XP, levels, coins).
/// </summary>
public interface IProgressionService
{
    /// <summary>
    /// Awards XP for a reading session (time + pages).
    /// </summary>
    /// <param name="minutes">Minutes spent reading</param>
    /// <param name="pagesRead">Optional number of pages read</param>
    /// <param name="activePlantId">Optional ID of active plant (for boost calculation)</param>
    /// <param name="hasStreak">Whether the user has an active reading streak (2+ days)</param>
    /// <returns>Progression result with XP breakdown and optional level-up</returns>
    Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, bool hasStreak = false);

    /// <summary>
    /// Awards bonus XP for completing a book.
    /// </summary>
    /// <param name="activePlantId">Optional ID of active plant (for boost calculation)</param>
    /// <returns>Progression result with XP breakdown and optional level-up</returns>
    Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId);

    /// <summary>
    /// Calculates the total XP boost percentage from all owned plants.
    /// </summary>
    /// <returns>Total boost percentage (e.g., 0.25 for 25%)</returns>
    Task<decimal> GetTotalPlantBoostAsync();

    /// <summary>
    /// Checks if a level-up occurred and processes coin rewards.
    /// </summary>
    /// <param name="oldXp">XP before the gain</param>
    /// <param name="newXp">XP after the gain</param>
    /// <param name="settingsToUpdate">Optional settings instance to update (if provided, caller must save)</param>
    /// <returns>Level-up result if level increased, otherwise null</returns>
    Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null);
}
