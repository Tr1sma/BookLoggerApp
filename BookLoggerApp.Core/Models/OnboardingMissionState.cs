using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

public class OnboardingMissionState
{
    [Key]
    [MaxLength(100)]
    public string MissionId { get; set; } = string.Empty;

    public OnboardingMissionStatus Status { get; set; } = OnboardingMissionStatus.Locked;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
