using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Manages annotations. Free tier is capped at 3 notes per book via <see cref="IFeatureGuard"/>.
/// </summary>
public class AnnotationService : IAnnotationService
{
    private const int FreeTierPerBookCap = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureGuard? _featureGuard;

    public AnnotationService(IUnitOfWork unitOfWork, IFeatureGuard? featureGuard = null)
    {
        _unitOfWork = unitOfWork;
        _featureGuard = featureGuard;
    }

    public async Task<IReadOnlyList<Annotation>> GetAllAsync(CancellationToken ct = default)
    {
        var annotations = await _unitOfWork.Annotations.GetAllAsync();
        return annotations.ToList();
    }

    public async Task<Annotation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Annotations.GetByIdAsync(id);
    }

    public async Task<Annotation> AddAsync(Annotation annotation, CancellationToken ct = default)
    {
        // Trim and reject blank notes before the cap check so whitespace can't burn a free-tier slot.
        annotation.Note = annotation.Note?.Trim() ?? string.Empty;
        annotation.Title = string.IsNullOrWhiteSpace(annotation.Title) ? null : annotation.Title.Trim();
        if (string.IsNullOrWhiteSpace(annotation.Note))
            throw new ValidationException(new[] { "Note text cannot be empty." });

        if (_featureGuard is not null)
        {
            int currentCountForBook = (await _unitOfWork.Annotations.FindAsync(a => a.BookId == annotation.BookId)).Count();
            _featureGuard.EnforceSoftLimit(
                FeatureKey.UnlimitedNotesAndQuotes,
                currentCountForBook,
                FreeTierPerBookCap,
                $"Free tier is limited to {FreeTierPerBookCap} notes per book. Upgrade to Plus for unlimited notes.");
        }

        if (annotation.CreatedAt == default)
            annotation.CreatedAt = DateTime.UtcNow;

        var result = await _unitOfWork.Annotations.AddAsync(annotation);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task UpdateAsync(Annotation annotation, CancellationToken ct = default)
    {
        annotation.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Annotations.UpdateAsync(annotation);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var annotation = await _unitOfWork.Annotations.GetByIdAsync(id);
        if (annotation != null)
        {
            await _unitOfWork.Annotations.DeleteAsync(annotation);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Annotation>> GetAnnotationsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var annotations = await _unitOfWork.Annotations.FindAsync(a => a.BookId == bookId);
        return annotations.ToList();
    }

    public async Task<IReadOnlyList<Annotation>> SearchAnnotationsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(ct);

        var lowerQuery = query.ToLower();
        var annotations = await _unitOfWork.Annotations.FindAsync(a =>
            a.Note.ToLower().Contains(lowerQuery) ||
            (a.Title != null && a.Title.ToLower().Contains(lowerQuery)));
        return annotations.ToList();
    }
}
