using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Author)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(b => b.ISBN)
            .HasMaxLength(20);

        builder.Property(b => b.CoverImagePath)
            .HasMaxLength(500);

        builder.Property(b => b.Publisher)
            .HasMaxLength(200);

        builder.Property(b => b.Language)
            .HasMaxLength(50);

        builder.Property(b => b.Description)
            .HasMaxLength(2000);

        builder.HasIndex(b => b.Title);
        builder.HasIndex(b => b.ISBN);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.DateAdded);

        builder.HasMany(b => b.BookGenres)
            .WithOne(bg => bg.Book)
            .HasForeignKey(bg => bg.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.ReadingSessions)
            .WithOne(rs => rs.Book)
            .HasForeignKey(rs => rs.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Quotes)
            .WithOne(q => q.Book)
            .HasForeignKey(q => q.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Annotations)
            .WithOne(a => a.Book)
            .HasForeignKey(a => a.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(b => b.ProgressPercentage); // computed, not mapped
    }
}
