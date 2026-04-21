using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit;

public class ExceptionsTests
{
    [Fact]
    public void BookLoggerException_WithMessage_SetsMessage()
    {
        var ex = new BookLoggerException("boom");

        ex.Message.Should().Be("boom");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void BookLoggerException_WithInnerException_PreservesInner()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new BookLoggerException("wrapper", inner);

        ex.Message.Should().Be("wrapper");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void EntityNotFoundException_SetsEntityTypeAndId()
    {
        var id = Guid.NewGuid();

        var ex = new EntityNotFoundException(typeof(Book), id);

        ex.EntityType.Should().Be<Book>();
        ex.EntityId.Should().Be(id);
        ex.Message.Should().Contain("Book");
        ex.Message.Should().Contain(id.ToString());
    }

    [Fact]
    public void ConcurrencyException_WithMessage_SetsMessage()
    {
        var ex = new ConcurrencyException("conflict");

        ex.Message.Should().Be("conflict");
    }

    [Fact]
    public void ConcurrencyException_WithInner_PreservesInner()
    {
        var inner = new Exception("db");
        var ex = new ConcurrencyException("conflict", inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void ValidationException_CollectsErrorsInOrder()
    {
        var errors = new[] { "Title required", "Author required" };

        var ex = new ValidationException(errors);

        ex.Errors.Should().BeEquivalentTo(errors);
        ex.Message.Should().Contain("Title required");
        ex.Message.Should().Contain("Author required");
    }

    [Fact]
    public void ValidationException_EmptyErrors_HasBaseMessage()
    {
        var ex = new ValidationException(Array.Empty<string>());

        ex.Errors.Should().BeEmpty();
        ex.Message.Should().Contain("Validation failed");
    }

    [Fact]
    public void InsufficientFundsException_SetsRequiredAndAvailable()
    {
        var ex = new InsufficientFundsException(required: 150, available: 50);

        ex.Required.Should().Be(150);
        ex.Available.Should().Be(50);
        ex.Message.Should().Contain("150");
        ex.Message.Should().Contain("50");
    }
}
