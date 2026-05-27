namespace BookLoggerApp.Core.Models;

public sealed class OnboardingFeatureAtlasEntry
{
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public required string Description { get; init; }
    public string? DescriptionKey { get; init; }
    public string? Route { get; init; }
    public string? CtaLabel { get; init; }
    public string? CtaLabelKey { get; init; }
    public string? Badge { get; init; }
    public string? BadgeKey { get; init; }
}
