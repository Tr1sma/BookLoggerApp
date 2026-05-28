namespace BookLoggerApp.Core.Enums;

/// <summary>
/// The reveal animation played when a Blind Date card is unwrapped. One of these is
/// chosen at random on every reveal so the moment feels varied.
/// </summary>
public enum BlindDateRevealAnimation
{
    /// <summary>Wrapping paper peels open along the ribbon and the bow pops off.</summary>
    Unwrap = 0,

    /// <summary>The card shakes with tension, then the wrapping bursts away.</summary>
    Burst = 1
}
