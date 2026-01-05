using FluentValidation.TestHelper;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Validators;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Validators;

public class ReadingGoalValidatorTests
{
    private readonly ReadingGoalValidator _validator;

    public ReadingGoalValidatorTests()
    {
        _validator = new ReadingGoalValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        var model = new ReadingGoal { Title = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_Max_Length()
    {
        var model = new ReadingGoal { Title = new string('a', 201) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_Max_Length()
    {
        var model = new ReadingGoal { Description = new string('a', 1001) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Have_Error_When_Target_Is_Zero_Or_Less()
    {
        var model = new ReadingGoal { Target = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Target);
    }

    [Fact]
    public void Should_Have_Error_When_Target_Exceeds_Max()
    {
        var model = new ReadingGoal { Target = 100001 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Target);
    }

    [Fact]
    public void Should_Have_Error_When_Current_Is_Negative()
    {
        var model = new ReadingGoal { Current = -1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Current);
    }

    [Fact]
    public void Should_Have_Error_When_StartDate_Is_Empty()
    {
        var model = new ReadingGoal { StartDate = default };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public void Should_Have_Error_When_EndDate_Is_Before_StartDate()
    {
        var model = new ReadingGoal 
        { 
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-1)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public void Should_Have_Error_When_Goal_Period_Exceeds_One_Year()
    {
        var model = new ReadingGoal 
        { 
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(366)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Goal_Period_Is_Valid()
    {
        var now = DateTime.UtcNow;
        var model = new ReadingGoal 
        { 
            StartDate = now,
            EndDate = now.AddDays(365),
            Title = "Valid Goal",
            Target = 10
        };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
    }
}
