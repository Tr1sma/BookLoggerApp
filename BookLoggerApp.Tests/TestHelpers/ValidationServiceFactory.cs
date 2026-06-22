using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Validators;
using BookLoggerApp.Infrastructure.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Builds a real <see cref="ValidationService"/> wired to the production FluentValidation
/// validators (Book, ReadingSession, ReadingGoal, UserPlant). Used by service tests that
/// assert the service actually enforces validation (CODE_REVIEW BUG-05) — the production
/// validators were dead code until the services started calling them.
/// </summary>
public static class ValidationServiceFactory
{
    public static IValidationService CreateReal()
    {
        var services = new ServiceCollection();
        services.AddTransient<IValidator<Book>, BookValidator>();
        services.AddTransient<IValidator<ReadingSession>, ReadingSessionValidator>();
        services.AddTransient<IValidator<ReadingGoal>, ReadingGoalValidator>();
        services.AddTransient<IValidator<UserPlant>, UserPlantValidator>();
        return new ValidationService(services.BuildServiceProvider());
    }
}
