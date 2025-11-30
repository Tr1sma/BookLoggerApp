using FluentValidation.Results;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for validating entities using FluentValidation.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates an entity and returns the validation result.
    /// </summary>
    ValidationResult Validate<T>(T entity) where T : class;

    /// <summary>
    /// Validates an entity and throws an exception if invalid.
    /// </summary>
    void ValidateAndThrow<T>(T entity) where T : class;

    /// <summary>
    /// Validates an entity asynchronously and returns the validation result.
    /// </summary>
    Task<ValidationResult> ValidateAsync<T>(T entity, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Validates an entity asynchronously and throws an exception if invalid.
    /// </summary>
    Task ValidateAndThrowAsync<T>(T entity, CancellationToken ct = default) where T : class;
}
