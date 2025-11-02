using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for Genre entity.
/// </summary>
public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.Description)
            .HasMaxLength(500);

        builder.Property(g => g.Icon)
            .HasMaxLength(10);

        builder.Property(g => g.ColorHex)
            .HasMaxLength(7);

        // Unique index on Name
        builder.HasIndex(g => g.Name)
            .IsUnique();

        // Relationship configured in BookGenreConfiguration
    }
}
