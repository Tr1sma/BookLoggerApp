namespace BookLoggerApp.Core.Models;

/// <summary>
/// Outcome of <see cref="Services.Abstractions.IBookService.SaveBookWithRelationsAsync"/>.
/// Lets the editor ViewModel react to a completed book (celebration UI) without
/// re-deriving the completion decision it delegated to the service.
/// </summary>
public record BookSaveResult(Book Book, bool ShowCompletionCelebration, bool CompletedFromExisting);
