using FluentValidation;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for Book model.
/// Ensures all book properties meet business rules.
/// </summary>
public class BookValidator : AbstractValidator<Book>
{
    public BookValidator()
    {
        RuleFor(b => b.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(500).WithMessage("Title cannot exceed 500 characters");

        RuleFor(b => b.Author)
            .NotEmpty().WithMessage("Author is required")
            .MaximumLength(300).WithMessage("Author cannot exceed 300 characters");

        RuleFor(b => b.ISBN)
            .MaximumLength(20).WithMessage("ISBN cannot exceed 20 characters")
            .When(b => !string.IsNullOrEmpty(b.ISBN));

        RuleFor(b => b.PageCount)
            .GreaterThan(0).WithMessage("Page count must be greater than 0")
            .LessThanOrEqualTo(50000).WithMessage("Page count cannot exceed 50,000")
            .When(b => b.PageCount.HasValue);

        RuleFor(b => b.CurrentPage)
            .GreaterThanOrEqualTo(0).WithMessage("Current page cannot be negative")
            .LessThanOrEqualTo(b => b.PageCount ?? int.MaxValue)
                .WithMessage("Current page cannot exceed total page count")
            .When(b => b.PageCount.HasValue);



        RuleFor(b => b.DateStarted)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Start date cannot be in the future")
            .When(b => b.DateStarted.HasValue);

        RuleFor(b => b.DateCompleted)
            .GreaterThanOrEqualTo(b => b.DateStarted ?? DateTime.MinValue)
                .WithMessage("Completion date must be after start date")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Completion date cannot be in the future")
            .When(b => b.DateCompleted.HasValue && b.DateStarted.HasValue);
    }
}
