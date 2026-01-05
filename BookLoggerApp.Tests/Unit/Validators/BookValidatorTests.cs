using FluentValidation.TestHelper;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Validators;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Validators;

public class BookValidatorTests
{
    private readonly BookValidator _validator;

    public BookValidatorTests()
    {
        _validator = new BookValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Title_Is_Empty()
    {
        var model = new Book { Title = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Title_Exceeds_Max_Length()
    {
        var model = new Book { Title = new string('a', 501) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Title_Is_Valid()
    {
        var model = new Book { Title = "Valid Title" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_Author_Is_Empty()
    {
        var model = new Book { Author = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Author);
    }

    [Fact]
    public void Should_Have_Error_When_Author_Exceeds_Max_Length()
    {
        var model = new Book { Author = new string('a', 301) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Author);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Author_Is_Valid()
    {
        var model = new Book { Author = "Valid Author" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Author);
    }

    [Fact]
    public void Should_Have_Error_When_ISBN_Exceeds_Max_Length()
    {
        var model = new Book { ISBN = new string('1', 21) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ISBN);
    }

    [Fact]
    public void Should_Not_Have_Error_When_ISBN_Is_Null_Or_Empty()
    {
        var model = new Book { ISBN = null };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.ISBN);
    }

    [Fact]
    public void Should_Have_Error_When_PageCount_Is_Zero_Or_Less()
    {
        var model = new Book { PageCount = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PageCount);
    }

    [Fact]
    public void Should_Have_Error_When_PageCount_Exceeds_Max()
    {
        var model = new Book { PageCount = 50001 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PageCount);
    }

    [Fact]
    public void Should_Have_Error_When_CurrentPage_Is_Negative()
    {
        var model = new Book { CurrentPage = -1, PageCount = 100 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CurrentPage);
    }

    [Fact]
    public void Should_Have_Error_When_CurrentPage_Exceeds_PageCount()
    {
        var model = new Book { PageCount = 100, CurrentPage = 101 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CurrentPage);
    }

    [Fact]
    public void Should_Have_Error_When_DateStarted_Is_In_Future()
    {
        var model = new Book { DateStarted = DateTime.UtcNow.AddDays(1) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DateStarted);
    }

    [Fact]
    public void Should_Have_Error_When_DateCompleted_Is_Before_DateStarted()
    {
        var model = new Book 
        { 
            DateStarted = DateTime.UtcNow,
            DateCompleted = DateTime.UtcNow.AddSeconds(-1)
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DateCompleted);
    }

    [Fact]
    public void Should_Have_Error_When_DateCompleted_Is_In_Future()
    {
        var model = new Book 
        { 
            DateStarted = DateTime.UtcNow,
            DateCompleted = DateTime.UtcNow.AddDays(1) 
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DateCompleted);
    }
}
