using FluentValidation;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for ReadingGoal model.
/// Ensures goal parameters are valid and achievable.
/// </summary>
public class ReadingGoalValidator : AbstractValidator<ReadingGoal>
{
    public ReadingGoalValidator()
    {
        RuleFor(g => g.Title)
            .NotEmpty().WithMessage("Goal title is required")
            .MaximumLength(200).WithMessage("Goal title cannot exceed 200 characters");

        RuleFor(g => g.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1,000 characters")
            .When(g => !string.IsNullOrEmpty(g.Description));

        RuleFor(g => g.Target)
            .GreaterThan(0).WithMessage("Target value must be greater than 0")
            .LessThanOrEqualTo(100000).WithMessage("Target value cannot exceed 100,000");

        RuleFor(g => g.Current)
            .GreaterThanOrEqualTo(0).WithMessage("Current value cannot be negative");

        RuleFor(g => g.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(g => g.EndDate)
            .GreaterThan(g => g.StartDate).WithMessage("End date must be after start date");

        // Validate that goal period is not too long (max 1 year)
        RuleFor(g => g.EndDate)
            .Must((goal, endDate) => (endDate - goal.StartDate).TotalDays <= 365)
            .WithMessage("Goal period cannot exceed 1 year");
    }
}
