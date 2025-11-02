using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for ReadingGoal entity.
/// </summary>
public class ReadingGoalConfiguration : IEntityTypeConfiguration<ReadingGoal>
{
    public void Configure(EntityTypeBuilder<ReadingGoal> builder)
    {
        builder.HasKey(rg => rg.Id);

        builder.Property(rg => rg.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(rg => rg.Type)
            .IsRequired();

        builder.Property(rg => rg.Target)
            .IsRequired();

        builder.Property(rg => rg.StartDate)
            .IsRequired();

        builder.Property(rg => rg.EndDate)
            .IsRequired();

        // Indexes
        builder.HasIndex(rg => rg.StartDate);
        builder.HasIndex(rg => rg.EndDate);
        builder.HasIndex(rg => rg.IsCompleted);

        // Ignore computed properties
        builder.Ignore(rg => rg.ProgressPercentage);
        builder.Ignore(rg => rg.IsActive);
    }
}
