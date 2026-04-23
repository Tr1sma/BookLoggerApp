using FluentValidation;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for ReadingGoal model.
/// Ensures goal parameters are valid and achievable.
/// </summary>
public class ReadingGoalValidator : AbstractValidator<ReadingGoal>
{
    public ReadingGoalValidator(IStringLocalizer<AppResources>? localizer = null)
    {
        string Tr(string key) => localizer?[key].Value ?? key;

        RuleFor(g => g.Title)
            .NotEmpty().WithMessage(_ => Tr("Validator_Goal_TitleRequired"))
            .MaximumLength(200).WithMessage(_ => Tr("Validator_Goal_TitleTooLong"));

        RuleFor(g => g.Description)
            .MaximumLength(1000).WithMessage(_ => Tr("Validator_Goal_DescriptionTooLong"))
            .When(g => !string.IsNullOrEmpty(g.Description));

        RuleFor(g => g.Target)
            .GreaterThan(0).WithMessage(_ => Tr("Validator_Goal_TargetPositive"))
            .LessThanOrEqualTo(100000).WithMessage(_ => Tr("Validator_Goal_TargetMax"));

        RuleFor(g => g.Current)
            .GreaterThanOrEqualTo(0).WithMessage(_ => Tr("Validator_Goal_CurrentNonNegative"));

        RuleFor(g => g.StartDate)
            .NotEmpty().WithMessage(_ => Tr("Validator_Goal_StartDateRequired"));

        RuleFor(g => g.EndDate)
            .GreaterThan(g => g.StartDate).WithMessage(_ => Tr("Validator_Goal_EndAfterStart"));

        // Validate that goal period is not too long (max 1 year)
        RuleFor(g => g.EndDate)
            .Must((goal, endDate) => (endDate - goal.StartDate).TotalDays <= 365)
            .WithMessage(_ => Tr("Validator_Goal_PeriodTooLong"));
    }
}
