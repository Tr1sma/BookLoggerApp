namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Coordinates a retry of the background database initialization after a prior
/// run failed or timed out. Introduced so Core/UI code can trigger a retry
/// without depending on the Infrastructure layer directly.
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Clears any failed state on <see cref="Infrastructure.DatabaseInitializationHelper"/>
    /// and re-runs the initialization in the background. Returns immediately;
    /// callers should await <c>EnsureInitializedAsync</c> (typically via
    /// <c>ExecuteSafelyWithDbAsync</c>) to observe completion.
    /// </summary>
    void Retry();
}
