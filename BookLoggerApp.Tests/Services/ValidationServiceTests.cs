using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ValidationServiceTests
{
    public class DummyEntity
    {
        public string? Name { get; set; }
    }

    public class DummyValidator : AbstractValidator<DummyEntity>
    {
        public DummyValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        }
    }

    public class UnregisteredEntity
    {
        public int Value { get; set; }
    }

    private static ValidationService CreateServiceWithValidator()
    {
        var services = new ServiceCollection();
        services.AddTransient<IValidator<DummyEntity>, DummyValidator>();
        var provider = services.BuildServiceProvider();
        return new ValidationService(provider);
    }

    private static ValidationService CreateServiceWithoutValidators()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        return new ValidationService(provider);
    }

    [Fact]
    public void Validate_NullEntity_ThrowsArgumentNullException()
    {
        var service = CreateServiceWithoutValidators();

        Action act = () => service.Validate<DummyEntity>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_NoValidatorRegistered_ReturnsSuccess()
    {
        var service = CreateServiceWithoutValidators();
        var entity = new UnregisteredEntity { Value = 42 };

        var result = service.Validate(entity);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidEntity_ReturnsSuccess()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "OK" };

        var result = service.Validate(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidEntity_ReturnsFailure()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "" };

        var result = service.Validate(entity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().Be("Name is required");
    }

    [Fact]
    public void ValidateAndThrow_NullEntity_ThrowsArgumentNullException()
    {
        var service = CreateServiceWithoutValidators();

        Action act = () => service.ValidateAndThrow<DummyEntity>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateAndThrow_NoValidator_DoesNotThrow()
    {
        var service = CreateServiceWithoutValidators();
        var entity = new UnregisteredEntity();

        Action act = () => service.ValidateAndThrow(entity);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_ValidEntity_DoesNotThrow()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "ok" };

        Action act = () => service.ValidateAndThrow(entity);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_InvalidEntity_ThrowsValidationException()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "" };

        Action act = () => service.ValidateAndThrow(entity);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public async Task ValidateAsync_NullEntity_ThrowsArgumentNullException()
    {
        var service = CreateServiceWithoutValidators();

        Func<Task> act = async () => await service.ValidateAsync<DummyEntity>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_NoValidator_ReturnsSuccess()
    {
        var service = CreateServiceWithoutValidators();
        var entity = new UnregisteredEntity();

        var result = await service.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ValidEntity_ReturnsSuccess()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "ok" };

        var result = await service.ValidateAsync(entity);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_InvalidEntity_ReturnsFailure()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "" };

        var result = await service.ValidateAsync(entity);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndThrowAsync_NullEntity_ThrowsArgumentNullException()
    {
        var service = CreateServiceWithoutValidators();

        Func<Task> act = async () => await service.ValidateAndThrowAsync<DummyEntity>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAndThrowAsync_NoValidator_DoesNotThrow()
    {
        var service = CreateServiceWithoutValidators();
        var entity = new UnregisteredEntity();

        Func<Task> act = async () => await service.ValidateAndThrowAsync(entity);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAndThrowAsync_ValidEntity_DoesNotThrow()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "ok" };

        Func<Task> act = async () => await service.ValidateAndThrowAsync(entity);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAndThrowAsync_InvalidEntity_ThrowsValidationException()
    {
        var service = CreateServiceWithValidator();
        var entity = new DummyEntity { Name = "" };

        Func<Task> act = async () => await service.ValidateAndThrowAsync(entity);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
