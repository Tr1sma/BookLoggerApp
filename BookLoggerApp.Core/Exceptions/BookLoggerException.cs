namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Base exception for all BookLoggerApp-specific exceptions.
/// </summary>
public class BookLoggerException : Exception
{
    public BookLoggerException(string message) : base(message)
    {
    }

    public BookLoggerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
