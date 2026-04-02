namespace BookLoggerApp.Core.Services.Abstractions;

public interface IReviewPromptService
{
    Task<bool> TryStartPromptAsync(CancellationToken ct = default);
    Task DisablePromptAsync(CancellationToken ct = default);
}