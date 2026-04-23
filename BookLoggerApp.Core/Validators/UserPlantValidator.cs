using FluentValidation;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for UserPlant model.
/// Ensures plant data is valid.
/// </summary>
public class UserPlantValidator : AbstractValidator<UserPlant>
{
    public UserPlantValidator(IStringLocalizer<AppResources>? localizer = null)
    {
        string Tr(string key) => localizer?[key].Value ?? key;

        RuleFor(p => p.Name)
            .NotEmpty().WithMessage(_ => Tr("Validator_Plant_NameRequired"))
            .MaximumLength(100).WithMessage(_ => Tr("Validator_Plant_NameTooLong"));

        RuleFor(p => p.SpeciesId)
            .NotEmpty().WithMessage(_ => Tr("Validator_Plant_SpeciesIdRequired"));

        RuleFor(p => p.CurrentLevel)
            .GreaterThanOrEqualTo(1).WithMessage(_ => Tr("Validator_Plant_LevelMin"))
            .LessThanOrEqualTo(100).WithMessage(_ => Tr("Validator_Plant_LevelMax"));

        RuleFor(p => p.Experience)
            .GreaterThanOrEqualTo(0).WithMessage(_ => Tr("Validator_Plant_ExperienceNonNegative"));

        RuleFor(p => p.PlantedAt)
            .NotEmpty().WithMessage(_ => Tr("Validator_Plant_PlantedRequired"))
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage(_ => Tr("Validator_Plant_PlantedFuture"));

        RuleFor(p => p.LastWatered)
            .NotEmpty().WithMessage(_ => Tr("Validator_Plant_LastWateredRequired"))
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage(_ => Tr("Validator_Plant_LastWateredFuture"));
    }
}
