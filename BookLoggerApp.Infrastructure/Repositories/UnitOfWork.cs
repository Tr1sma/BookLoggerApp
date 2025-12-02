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

    // Lazy-initialized repositories to ensure they all share the SAME DbContext
    private IBookRepository? _books;
    private IReadingSessionRepository? _readingSessions;
    private IReadingGoalRepository? _readingGoals;
    private IUserPlantRepository? _userPlants;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // Create repositories lazily with the SAME DbContext instance
    public IBookRepository Books => _books ??= new BookRepository(_context);
    public IReadingSessionRepository ReadingSessions => _readingSessions ??= new ReadingSessionRepository(_context);
    public IReadingGoalRepository ReadingGoals => _readingGoals ??= new ReadingGoalRepository(_context);
    public IUserPlantRepository UserPlants => _userPlants ??= new UserPlantRepository(_context);

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
