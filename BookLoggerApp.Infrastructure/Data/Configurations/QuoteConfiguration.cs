using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for Quote entity.
/// </summary>
public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.BookId)
            .IsRequired();

        builder.Property(q => q.Text)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(q => q.Context)
            .HasMaxLength(500);

        builder.Property(q => q.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(q => q.BookId);
        builder.HasIndex(q => q.IsFavorite);

        // Relationship configured in BookConfiguration
    }
}
