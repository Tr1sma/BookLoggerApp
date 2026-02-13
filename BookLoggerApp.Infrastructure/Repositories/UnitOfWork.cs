using BookLoggerApp.Core.Models;
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

    // Lazy-initialized specific repositories
    private IBookRepository? _books;
    private IReadingSessionRepository? _readingSessions;
    private IReadingGoalRepository? _readingGoals;
    private IUserPlantRepository? _userPlants;

    // Lazy-initialized generic repositories
    private IRepository<Genre>? _genres;
    private IRepository<BookGenre>? _bookGenres;
    private IRepository<Quote>? _quotes;
    private IRepository<Annotation>? _annotations;
    private IRepository<PlantSpecies>? _plantSpecies;
    private IRepository<AppSettings>? _appSettings;
    private IRepository<Trope>? _tropes;
    private IRepository<BookTrope>? _bookTropes;
    private IRepository<WishlistInfo>? _wishlistInfos;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // ===== Specific Repositories =====
    public IBookRepository Books => _books ??= new BookRepository(_context);
    public IReadingSessionRepository ReadingSessions => _readingSessions ??= new ReadingSessionRepository(_context);
    public IReadingGoalRepository ReadingGoals => _readingGoals ??= new ReadingGoalRepository(_context);
    public IUserPlantRepository UserPlants => _userPlants ??= new UserPlantRepository(_context);

    // ===== Generic Repositories =====
    public IRepository<Genre> Genres => _genres ??= new Repository<Genre>(_context);
    public IRepository<BookGenre> BookGenres => _bookGenres ??= new Repository<BookGenre>(_context);
    public IRepository<Quote> Quotes => _quotes ??= new Repository<Quote>(_context);
    public IRepository<Annotation> Annotations => _annotations ??= new Repository<Annotation>(_context);
    public IRepository<PlantSpecies> PlantSpecies => _plantSpecies ??= new Repository<PlantSpecies>(_context);
    public IRepository<AppSettings> AppSettingsRepo => _appSettings ??= new Repository<AppSettings>(_context);
    public IRepository<Trope> Tropes => _tropes ??= new Repository<Trope>(_context);
    public IRepository<BookTrope> BookTropes => _bookTropes ??= new Repository<BookTrope>(_context);
    public IRepository<WishlistInfo> WishlistInfos => _wishlistInfos ??= new Repository<WishlistInfo>(_context);

    // ===== Direct Context Access =====
    public AppDbContext Context => _context;

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
