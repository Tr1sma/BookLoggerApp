using FluentValidation;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Validators;

/// <summary>
/// Validator for UserPlant model.
/// Ensures plant data is valid.
/// </summary>
public class UserPlantValidator : AbstractValidator<UserPlant>
{
    public UserPlantValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Plant name is required")
            .MaximumLength(100).WithMessage("Plant name cannot exceed 100 characters");

        RuleFor(p => p.SpeciesId)
            .NotEmpty().WithMessage("Plant species ID is required");

        RuleFor(p => p.CurrentLevel)
            .GreaterThanOrEqualTo(1).WithMessage("Current level must be at least 1")
            .LessThanOrEqualTo(100).WithMessage("Current level cannot exceed 100");

        RuleFor(p => p.Experience)
            .GreaterThanOrEqualTo(0).WithMessage("Experience cannot be negative");

        RuleFor(p => p.PlantedAt)
            .NotEmpty().WithMessage("Planted date is required")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Planted date cannot be in the future");

        RuleFor(p => p.LastWatered)
            .NotEmpty().WithMessage("Last watered date is required")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Last watered date cannot be in the future");
    }
}
