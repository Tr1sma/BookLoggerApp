using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for UserPlant entity.
/// </summary>
public class UserPlantConfiguration : IEntityTypeConfiguration<UserPlant>
{
    public void Configure(EntityTypeBuilder<UserPlant> builder)
    {
        builder.HasKey(up => up.Id);

        builder.Property(up => up.SpeciesId)
            .IsRequired();

        builder.Property(up => up.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(up => up.CurrentLevel)
            .IsRequired();

        builder.Property(up => up.Experience)
            .IsRequired();

        builder.Property(up => up.Status)
            .IsRequired();

        builder.Property(up => up.LastWatered)
            .IsRequired();

        builder.Property(up => up.PlantedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(up => up.SpeciesId);
        builder.HasIndex(up => up.IsActive);

        // Relationship configured in PlantSpeciesConfiguration
    }
}
