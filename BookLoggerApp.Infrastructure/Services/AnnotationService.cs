using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing annotations.
/// </summary>
public class AnnotationService : IAnnotationService
{
    private readonly IRepository<Annotation> _annotationRepository;

    public AnnotationService(IRepository<Annotation> annotationRepository)
    {
        _annotationRepository = annotationRepository;
    }

    public async Task<IReadOnlyList<Annotation>> GetAllAsync(CancellationToken ct = default)
    {
        var annotations = await _annotationRepository.GetAllAsync();
        return annotations.ToList();
    }

    public async Task<Annotation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _annotationRepository.GetByIdAsync(id);
    }

    public async Task<Annotation> AddAsync(Annotation annotation, CancellationToken ct = default)
    {
        if (annotation.CreatedAt == default)
            annotation.CreatedAt = DateTime.UtcNow;

        return await _annotationRepository.AddAsync(annotation);
    }

    public async Task UpdateAsync(Annotation annotation, CancellationToken ct = default)
    {
        annotation.UpdatedAt = DateTime.UtcNow;
        await _annotationRepository.UpdateAsync(annotation);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var annotation = await _annotationRepository.GetByIdAsync(id);
        if (annotation != null)
        {
            await _annotationRepository.DeleteAsync(annotation);
        }
    }

    public async Task<IReadOnlyList<Annotation>> GetAnnotationsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var annotations = await _annotationRepository.FindAsync(a => a.BookId == bookId);
        return annotations.ToList();
    }

    public async Task<IReadOnlyList<Annotation>> SearchAnnotationsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(ct);

        var lowerQuery = query.ToLower();
        var annotations = await _annotationRepository.FindAsync(a =>
            a.Note.ToLower().Contains(lowerQuery) ||
            (a.Title != null && a.Title.ToLower().Contains(lowerQuery)));
        return annotations.ToList();
    }
}
