using FluentValidation;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for ReadingSession model.
/// Ensures reading session data is valid and consistent.
/// </summary>
public class ReadingSessionValidator : AbstractValidator<ReadingSession>
{
    public ReadingSessionValidator(IStringLocalizer<AppResources>? localizer = null)
    {
        string Tr(string key) => localizer?[key].Value ?? key;

        RuleFor(s => s.BookId)
            .NotEmpty().WithMessage(_ => Tr("Validator_Session_BookIdRequired"));

        RuleFor(s => s.StartedAt)
            .NotEmpty().WithMessage(_ => Tr("Validator_Session_StartedRequired"))
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage(_ => Tr("Validator_Session_StartedFuture"));

        RuleFor(s => s.EndedAt)
            .GreaterThan(s => s.StartedAt).WithMessage(_ => Tr("Validator_Session_EndAfterStart"))
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage(_ => Tr("Validator_Session_EndFuture"))
            .When(s => s.EndedAt.HasValue);

        RuleFor(s => s.Minutes)
            .GreaterThan(0).WithMessage(_ => Tr("Validator_Session_MinutesPositive"))
            .LessThanOrEqualTo(1440).WithMessage(_ => Tr("Validator_Session_MinutesMax"));

        RuleFor(s => s.PagesRead)
            .GreaterThanOrEqualTo(0).WithMessage(_ => Tr("Validator_Session_PagesNonNegative"))
            .LessThanOrEqualTo(10000).WithMessage(_ => Tr("Validator_Session_PagesMax"))
            .When(s => s.PagesRead.HasValue);

        RuleFor(s => s.XpEarned)
            .GreaterThanOrEqualTo(0).WithMessage(_ => Tr("Validator_Session_XpNonNegative"));
    }
}
