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

        builder.Property(a => a.CreatedAt)
            .IsRequired();
    }
}
