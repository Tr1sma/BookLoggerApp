using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing book annotations and notes.
/// </summary>
public interface IAnnotationService
{
    // Annotation CRUD
    Task<IReadOnlyList<Annotation>> GetAllAsync(CancellationToken ct = default);
    Task<Annotation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Annotation> AddAsync(Annotation annotation, CancellationToken ct = default);
    Task UpdateAsync(Annotation annotation, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Query Annotations
    Task<IReadOnlyList<Annotation>> GetAnnotationsByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<IReadOnlyList<Annotation>> SearchAnnotationsAsync(string query, CancellationToken ct = default);
}
