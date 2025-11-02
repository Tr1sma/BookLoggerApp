using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for ReadingSession entity.
/// </summary>
public class ReadingSessionConfiguration : IEntityTypeConfiguration<ReadingSession>
{
    public void Configure(EntityTypeBuilder<ReadingSession> builder)
    {
        builder.HasKey(rs => rs.Id);

        builder.Property(rs => rs.BookId)
            .IsRequired();

        builder.Property(rs => rs.StartedAt)
            .IsRequired();

        builder.Property(rs => rs.Minutes)
            .IsRequired();

        builder.Property(rs => rs.Notes)
            .HasMaxLength(1000);

        // Indexes
        builder.HasIndex(rs => rs.BookId);
        builder.HasIndex(rs => rs.StartedAt);

        // Relationship configured in BookConfiguration
    }
}
