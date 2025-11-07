using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using Microsoft.EntityFrameworkCore.Storage;

namespace BookLoggerApp.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation coordinating multiple repository operations
/// and managing transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(
        AppDbContext context,
        IBookRepository books,
        IReadingSessionRepository readingSessions,
        IReadingGoalRepository readingGoals,
        IUserPlantRepository userPlants)
    {
        _context = context;
        Books = books;
        ReadingSessions = readingSessions;
        ReadingGoals = readingGoals;
        UserPlants = userPlants;
    }

    public IBookRepository Books { get; }
    public IReadingSessionRepository ReadingSessions { get; }
    public IReadingGoalRepository ReadingGoals { get; }
    public IUserPlantRepository UserPlants { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction is in progress.");
        }

        try
        {
            await _transaction.CommitAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction is in progress.");
        }

        try
        {
            await _transaction.RollbackAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            // Note: We don't dispose the context here because it's managed by DI
            _disposed = true;
        }
    }
}
