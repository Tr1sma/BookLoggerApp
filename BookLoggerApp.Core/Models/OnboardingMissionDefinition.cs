namespace BookLoggerApp.Core.Models;

public sealed class OnboardingMissionDefinition
{
    public required OnboardingMissionId Id { get; init; }
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string CtaLabel { get; init; }
    public required string DefaultRoute { get; init; }
    public bool IsCore { get; init; }
    public bool IsTimeGated { get; init; }
    public IReadOnlyList<OnboardingMissionId> Prerequisites { get; init; } = Array.Empty<OnboardingMissionId>();
}
