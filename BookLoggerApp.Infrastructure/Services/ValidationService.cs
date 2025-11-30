using FluentValidation;
using FluentValidation.Results;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service for validating entities using FluentValidation.
/// Automatically resolves the appropriate validator for each entity type.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ValidationResult Validate<T>(T entity) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var validator = GetValidator<T>();
        if (validator == null)
        {
            // No validator registered - return success
            return new ValidationResult();
        }

        return validator.Validate(entity);
    }

    public void ValidateAndThrow<T>(T entity) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var validator = GetValidator<T>();
        if (validator == null)
        {
            // No validator registered - skip validation
            return;
        }

        validator.ValidateAndThrow(entity);
    }

    public async Task<ValidationResult> ValidateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var validator = GetValidator<T>();
        if (validator == null)
        {
            // No validator registered - return success
            return new ValidationResult();
        }

        return await validator.ValidateAsync(entity, ct);
    }

    public async Task ValidateAndThrowAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var validator = GetValidator<T>();
        if (validator == null)
        {
            // No validator registered - skip validation
            return;
        }

        await validator.ValidateAndThrowAsync(entity, ct);
    }

    private IValidator<T>? GetValidator<T>() where T : class
    {
        return _serviceProvider.GetService(typeof(IValidator<T>)) as IValidator<T>;
    }
}
