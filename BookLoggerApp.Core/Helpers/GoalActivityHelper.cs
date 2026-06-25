using System;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Single source of truth for "is this reading goal still active today?".
/// </summary>
/// <remarks>
/// EndDate carries Kind=Unspecified ticks for the user's local midnight, so compare
/// against local midnight — not DateTime.UtcNow, which drops the goal early on its final
/// day in positive-UTC zones. Shared by ReadingGoalRepository and the Android widget so
/// they cannot drift (CODE_REVIEW INK-06).
/// </remarks>
public static class GoalActivityHelper
{
    /// <summary>
    /// True when <paramref name="goal"/> is not completed and its end day has not yet passed,
    /// as of the local wall-clock instant <paramref name="asOfLocal"/> (only its calendar date
    /// is used, so the goal stays active for its entire final local day).
    /// </summary>
    public static bool IsActiveAsOf(ReadingGoal goal, DateTime asOfLocal)
    {
        return !goal.IsCompleted && goal.EndDate >= ActiveCutoff(asOfLocal);
    }

    /// <summary>
    /// The inclusive local-midnight cutoff a goal's <see cref="ReadingGoal.EndDate"/> must be
    /// greater than or equal to in order to still count as active.
    /// </summary>
    public static DateTime ActiveCutoff(DateTime asOfLocal) => asOfLocal.Date;
}
