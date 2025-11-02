using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for Annotation entity.
/// </summary>
public class AnnotationConfiguration : IEntityTypeConfiguration<Annotation>
{
    public void Configure(EntityTypeBuilder<Annotation> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.BookId)
            .IsRequired();

        builder.Property(a => a.Note)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(a => a.Title)
            .HasMaxLength(200);

        builder.Property(a => a.ColorHex)
            .HasMaxLength(7);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(a => a.BookId);
        builder.HasIndex(a => a.PageNumber);

        // Relationship configured in BookConfiguration
    }
}
