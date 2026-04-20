namespace BookLoggerApp.Core.Models;

/// <summary>
/// String keys for late-game special abilities on plants and decorations.
/// Keep in sync with the seed data in PlantSeedData / DecorationSeedData.
/// </summary>
public static class SpecialAbilityKeys
{
    /// <summary>Chronikbaum — rescues a breaking reading streak once per 14 days.</summary>
    public const string StreakGuardian = "streak_guardian";

    /// <summary>Ewiger Phönix-Bonsai — self-revives and prevents other plants from dying while alive.</summary>
    public const string EternalPhoenix = "eternal_phoenix";

    /// <summary>Herz der Geschichten — five combined effects (XP boost, coin boost, session bonus coins, doubled plant growth, first-of-day XP bonus).</summary>
    public const string StoryHeart = "story_heart";
}
