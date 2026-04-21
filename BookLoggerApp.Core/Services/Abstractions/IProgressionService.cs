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
    /// <param name="streakDays">The reading streak length in days for the session date</param>
    /// <param name="ct">Cancellation token propagated to downstream settings/plant reads and writes.</param>
    /// <returns>Progression result with XP breakdown and optional level-up</returns>
    Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, int streakDays = 0, CancellationToken ct = default);

    /// <summary>
    /// Awards bonus XP for completing a book.
    /// </summary>
    /// <param name="activePlantId">Optional ID of active plant (for boost calculation)</param>
    /// <param name="ct">Cancellation token propagated to downstream settings/plant reads and writes.</param>
    /// <returns>Progression result with XP breakdown and optional level-up</returns>
    Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId, CancellationToken ct = default);

    /// <summary>
    /// Calculates the total XP boost percentage from all owned plants.
    /// </summary>
    /// <param name="ct">Cancellation token propagated to the underlying plant and decoration queries.</param>
    /// <returns>Total boost percentage (e.g., 0.25 for 25%)</returns>
    Task<decimal> GetTotalPlantBoostAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a level-up occurred and processes coin rewards.
    /// </summary>
    /// <param name="oldXp">XP before the gain</param>
    /// <param name="newXp">XP after the gain</param>
    /// <param name="settingsToUpdate">Optional settings instance to update (if provided, caller must save)</param>
    /// <param name="ct">Cancellation token propagated to the underlying settings and decoration reads.</param>
    /// <returns>Level-up result if level increased, otherwise null</returns>
    Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null, CancellationToken ct = default);

    /// <summary>
    /// Awards a flat bonus XP amount (no plant/decoration boost applied).
    /// Used by special abilities that provide pre-computed XP rewards.
    /// Handles level-up processing if the bonus pushes the user past a level threshold.
    /// </summary>
    Task<LevelUpResult?> AwardBonusXpAsync(int xp, CancellationToken ct = default);
}
