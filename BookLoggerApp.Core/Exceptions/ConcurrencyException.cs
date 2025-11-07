namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Exception thrown when a concurrency conflict occurs during an update operation.
/// </summary>
public class ConcurrencyException : BookLoggerException
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
