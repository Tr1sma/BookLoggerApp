namespace BookLoggerApp.Core.Models;

public sealed class OnboardingMissionProgress
{
    public required OnboardingMissionId Id { get; init; }
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string CtaLabel { get; init; }
    public required string Route { get; init; }
    public OnboardingMissionStatus Status { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool IsCore { get; init; }
    public bool IsTimeGated { get; init; }
    public string? Note { get; init; }

    public bool IsCompleted => Status == OnboardingMissionStatus.Completed;
    public bool IsLocked => Status == OnboardingMissionStatus.Locked;
}
