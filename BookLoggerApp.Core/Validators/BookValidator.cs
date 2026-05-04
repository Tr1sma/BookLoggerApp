using FluentValidation;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for Book model.
/// Ensures all book properties meet business rules.
/// </summary>
public class BookValidator : AbstractValidator<Book>
{
    public BookValidator(IStringLocalizer<AppResources>? localizer = null)
    {
        string Tr(string key) => localizer?[key].Value ?? key;

        RuleFor(b => b.Title)
            .NotEmpty().WithMessage(_ => Tr("Validator_Book_TitleRequired"))
            .MaximumLength(500).WithMessage(_ => Tr("Validator_Book_TitleTooLong"));

        RuleFor(b => b.Author)
            .NotEmpty().WithMessage(_ => Tr("Validator_Book_AuthorRequired"))
            .MaximumLength(300).WithMessage(_ => Tr("Validator_Book_AuthorTooLong"));

        RuleFor(b => b.ISBN)
            .MaximumLength(20).WithMessage(_ => Tr("Validator_Book_IsbnTooLong"))
            .When(b => !string.IsNullOrEmpty(b.ISBN));

        RuleFor(b => b.PageCount)
            .GreaterThan(0).WithMessage(_ => Tr("Validator_Book_PageCountPositive"))
            .LessThanOrEqualTo(50000).WithMessage(_ => Tr("Validator_Book_PageCountMax"))
            .When(b => b.PageCount.HasValue);

        RuleFor(b => b.CurrentPage)
            .GreaterThanOrEqualTo(0).WithMessage(_ => Tr("Validator_Book_CurrentPageNonNegative"))
            .LessThanOrEqualTo(b => b.PageCount ?? int.MaxValue)
                .WithMessage(_ => Tr("Validator_Book_CurrentPageMax"))
            .When(b => b.PageCount.HasValue);



        RuleFor(b => b.DateStarted)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage(_ => Tr("Validator_Book_DateStartedFuture"))
            .When(b => b.DateStarted.HasValue);

        // Future-date check on DateCompleted must fire independently of whether DateStarted
        // is set, otherwise imports/lookups that provide only DateCompleted can slip a
        // future date past validation (the older combined When required both fields).
        RuleFor(b => b.DateCompleted)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage(_ => Tr("Validator_Book_DateCompletedFuture"))
            .When(b => b.DateCompleted.HasValue);

        RuleFor(b => b.DateCompleted)
            .GreaterThanOrEqualTo(b => b.DateStarted ?? DateTime.MinValue)
                .WithMessage(_ => Tr("Validator_Book_DateCompletedBeforeStart"))
            .When(b => b.DateCompleted.HasValue && b.DateStarted.HasValue);
    }
}
