namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Exception thrown when a billing operation fails.
/// </summary>
public class BillingException : BookLoggerException
{
    public BillingException(string message) : base(message)
    {
    }

    public BillingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
