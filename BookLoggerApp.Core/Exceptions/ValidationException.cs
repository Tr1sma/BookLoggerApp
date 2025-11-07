namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : BookLoggerException
{
    public ValidationException(IEnumerable<string> errors)
        : base($"Validation failed: {string.Join(", ", errors)}")
    {
        Errors = errors.ToList();
    }

    public IReadOnlyList<string> Errors { get; }
}
