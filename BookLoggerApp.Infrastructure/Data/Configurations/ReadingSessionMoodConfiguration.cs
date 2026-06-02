using BookLoggerApp.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ReadingSessionMood child entity (per-session mood tags).
/// </summary>
public class ReadingSessionMoodConfiguration : IEntityTypeConfiguration<ReadingSessionMood>
{
    public void Configure(EntityTypeBuilder<ReadingSessionMood> builder)
    {
        builder.HasKey(m => new { m.ReadingSessionId, m.Mood });

        builder.Property(m => m.Mood)
            .HasConversion<int>();

        builder.HasOne(m => m.ReadingSession)
            .WithMany(rs => rs.Moods)
            .HasForeignKey(m => m.ReadingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
