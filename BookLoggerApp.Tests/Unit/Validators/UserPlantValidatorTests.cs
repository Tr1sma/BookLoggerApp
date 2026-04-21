using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Validators;

public class UserPlantValidatorTests
{
    private readonly UserPlantValidator _validator = new();

    private static UserPlant Valid() => new()
    {
        Name = "Story Seedling",
        SpeciesId = Guid.NewGuid(),
        CurrentLevel = 1,
        Experience = 0,
        PlantedAt = DateTime.UtcNow.AddDays(-1),
        LastWatered = DateTime.UtcNow.AddHours(-1)
    };

    [Fact]
    public void ValidPlant_Passes()
    {
        var result = _validator.TestValidate(Valid());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        var plant = Valid();
        plant.Name = "";

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.Name);
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        var plant = Valid();
        plant.Name = new string('A', 101);

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.Name);
    }

    [Fact]
    public void EmptySpeciesId_Fails()
    {
        var plant = Valid();
        plant.SpeciesId = Guid.Empty;

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.SpeciesId);
    }

    [Fact]
    public void LevelTooLow_Fails()
    {
        var plant = Valid();
        plant.CurrentLevel = 0;

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.CurrentLevel);
    }

    [Fact]
    public void LevelTooHigh_Fails()
    {
        var plant = Valid();
        plant.CurrentLevel = 101;

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.CurrentLevel);
    }

    [Fact]
    public void NegativeExperience_Fails()
    {
        var plant = Valid();
        plant.Experience = -1;

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.Experience);
    }

    [Fact]
    public void FuturePlantedAt_Fails()
    {
        var plant = Valid();
        plant.PlantedAt = DateTime.UtcNow.AddDays(1);

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.PlantedAt);
    }

    [Fact]
    public void DefaultPlantedAt_Fails()
    {
        var plant = Valid();
        plant.PlantedAt = default;

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.PlantedAt);
    }

    [Fact]
    public void FutureLastWatered_Fails()
    {
        var plant = Valid();
        plant.LastWatered = DateTime.UtcNow.AddDays(1);

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.LastWatered);
    }

    [Fact]
    public void DefaultLastWatered_Fails()
    {
        var plant = Valid();
        plant.LastWatered = default;

        var result = _validator.TestValidate(plant);

        result.ShouldHaveValidationErrorFor(p => p.LastWatered);
    }
}
