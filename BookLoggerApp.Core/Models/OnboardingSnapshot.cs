namespace BookLoggerApp.Core.Models;

public sealed class OnboardingSnapshot
{
    public int FlowVersion { get; init; }
    public int IntroStepCount { get; init; }
    public int CurrentIntroStep { get; init; }
    public OnboardingIntroStatus IntroStatus { get; init; }
    public bool ShouldShowIntro { get; init; }
    public IReadOnlyList<OnboardingMissionProgress> Missions { get; init; } = Array.Empty<OnboardingMissionProgress>();
    public IReadOnlyList<OnboardingFeatureAtlasEntry> FeatureAtlas { get; init; } = Array.Empty<OnboardingFeatureAtlasEntry>();

    public int CompletedCoreCount => Missions.Count(m => m.IsCore && m.IsCompleted);

    public int TotalCoreCount => Missions.Count(m => m.IsCore);

    public bool HasCompletedCoreMissions => TotalCoreCount > 0 && CompletedCoreCount == TotalCoreCount;

    public bool HasPendingGuidedSetup => Missions.Any(m => m.IsCore && !m.IsCompleted);

    public bool ShowHubCta => HasPendingGuidedSetup;

    public OnboardingMissionProgress? NextRecommendedMission =>
        Missions.FirstOrDefault(m => m.IsCore && !m.IsCompleted) ??
        Missions.FirstOrDefault(m => !m.IsCompleted);
}
