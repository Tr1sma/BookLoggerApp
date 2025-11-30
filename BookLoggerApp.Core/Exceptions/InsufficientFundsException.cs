namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Exception thrown when the user doesn't have enough coins for a purchase.
/// </summary>
public class InsufficientFundsException : BookLoggerException
{
    public InsufficientFundsException(int required, int available)
        : base($"Insufficient coins: need {required}, have {available}")
    {
        Required = required;
        Available = available;
    }

    public int Required { get; }
    public int Available { get; }
}
