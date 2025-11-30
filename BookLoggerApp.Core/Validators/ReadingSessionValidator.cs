using FluentValidation;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for ReadingSession model.
/// Ensures reading session data is valid and consistent.
/// </summary>
public class ReadingSessionValidator : AbstractValidator<ReadingSession>
{
    public ReadingSessionValidator()
    {
        RuleFor(s => s.BookId)
            .NotEmpty().WithMessage("Book ID is required");

        RuleFor(s => s.StartedAt)
            .NotEmpty().WithMessage("Start time is required")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Start time cannot be in the future");

        RuleFor(s => s.EndedAt)
            .GreaterThan(s => s.StartedAt).WithMessage("End time must be after start time")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("End time cannot be in the future")
            .When(s => s.EndedAt.HasValue);

        RuleFor(s => s.Minutes)
            .GreaterThan(0).WithMessage("Reading duration must be greater than 0")
            .LessThanOrEqualTo(1440).WithMessage("Reading duration cannot exceed 24 hours (1440 minutes)");

        RuleFor(s => s.PagesRead)
            .GreaterThanOrEqualTo(0).WithMessage("Pages read cannot be negative")
            .LessThanOrEqualTo(10000).WithMessage("Pages read cannot exceed 10,000")
            .When(s => s.PagesRead.HasValue);

        RuleFor(s => s.XpEarned)
            .GreaterThanOrEqualTo(0).WithMessage("XP earned cannot be negative");
    }
}
