using FluentValidation.TestHelper;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Validators;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Validators;

public class ReadingSessionValidatorTests
{
    private readonly ReadingSessionValidator _validator;

    public ReadingSessionValidatorTests()
    {
        _validator = new ReadingSessionValidator();
    }

    [Fact]
    public void Should_Have_Error_When_BookId_Is_Empty()
    {
        var model = new ReadingSession { BookId = Guid.Empty };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.BookId);
    }

    [Fact]
    public void Should_Have_Error_When_StartedAt_Is_Empty()
    {
        var model = new ReadingSession { StartedAt = default };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.StartedAt);
    }

    [Fact]
    public void Should_Have_Error_When_StartedAt_Is_In_Future()
    {
        var model = new ReadingSession { StartedAt = DateTime.UtcNow.AddDays(1) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.StartedAt);
    }

    [Fact]
    public void Should_Have_Error_When_EndedAt_Is_Before_StartedAt()
    {
        var model = new ReadingSession 
        { 
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EndedAt);
    }

    [Fact]
    public void Should_Have_Error_When_EndedAt_Is_In_Future()
    {
        var model = new ReadingSession 
        { 
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddDays(1)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EndedAt);
    }

    [Fact]
    public void Should_Have_Error_When_Minutes_Is_Zero_Or_Less()
    {
        var model = new ReadingSession { Minutes = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Minutes);
    }

    [Fact]
    public void Should_Have_Error_When_Minutes_Exceeds_Max()
    {
        var model = new ReadingSession { Minutes = 1441 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Minutes);
    }

    [Fact]
    public void Should_Have_Error_When_PagesRead_Is_Negative()
    {
        var model = new ReadingSession { PagesRead = -1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PagesRead);
    }

    [Fact]
    public void Should_Have_Error_When_PagesRead_Exceeds_Max()
    {
        var model = new ReadingSession { PagesRead = 10001 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PagesRead);
    }

    [Fact]
    public void Should_Have_Error_When_XpEarned_Is_Negative()
    {
        var model = new ReadingSession { XpEarned = -1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.XpEarned);
    }
}
