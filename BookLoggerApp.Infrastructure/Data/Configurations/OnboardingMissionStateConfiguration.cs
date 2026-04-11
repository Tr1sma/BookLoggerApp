using BookLoggerApp.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

public class OnboardingMissionStateConfiguration : IEntityTypeConfiguration<OnboardingMissionState>
{
    public void Configure(EntityTypeBuilder<OnboardingMissionState> builder)
    {
        builder.HasKey(m => m.MissionId);

        builder.Property(m => m.MissionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(OnboardingMissionStatus.Locked);

        builder.Property(m => m.CreatedAt)
            .IsRequired();
    }
}
