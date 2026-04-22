using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for AppSettings entity.
/// </summary>
public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Theme)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Language)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(a => a.UserLevel)
            .IsRequired();

        builder.Property(a => a.TotalXp)
            .IsRequired();

        builder.Property(a => a.Coins)
            .IsRequired();

        builder.Property(a => a.ShelfLedgeColor)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(a => a.ShelfBaseColor)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.HasCompletedOnboarding)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.OnboardingFlowVersion)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(a => a.OnboardingIntroStatus)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(OnboardingIntroStatus.NotStarted);

        builder.Property(a => a.OnboardingCurrentStep)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(a => a.OnboardingAutoCompletedForExistingUser)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.OnboardingTutorialPlantNeedsWateringAssist)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.HideGettingStartedCta)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.AnalyticsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.CrashReportingEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.PrivacyBannerDismissed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.PrivacyPolicyAcceptedAt);
    }
}
