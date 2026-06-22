using System;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Single source of truth for "is this reading goal still active today?".
///
/// <para>A goal's <see cref="ReadingGoal.EndDate"/> carries Kind=Unspecified ticks that represent
/// the user's local calendar midnight (the UI's &lt;input type="date"&gt; picker), so it must be
/// compared against the user's local midnight — NOT <see cref="DateTime.UtcNow"/>, which has a
/// time-of-day component and drops the goal several hours early on its final day for users in
/// positive-UTC zones. Both ReadingGoalRepository (DB-query cutoff) and the Android home-screen
/// widget consume this helper so the two surfaces cannot drift (CODE_REVIEW INK-06).</para>
/// </summary>
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
