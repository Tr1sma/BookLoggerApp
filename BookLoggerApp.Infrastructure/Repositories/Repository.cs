using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Infrastructure.Data;
using System.Linq.Expressions;

namespace BookLoggerApp.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation using EF Core.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking().ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _dbSet.AddRangeAsync(entities, ct);
    }

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        // Tracker-only mutation (I/O is in SaveChangesAsync); honour ct before touching it.
        ct.ThrowIfCancellationRequested();

        // If a different instance with the same key is already tracked, detach it first so
        // _dbSet.Update doesn't throw an identity conflict.
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            var keyProperty = _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty != null)
            {
                var currentId = keyProperty.GetGetter().GetClrValue(entity);
                var trackedEntity = _dbSet.Local.FirstOrDefault(e =>
                    keyProperty.GetGetter().GetClrValue(e)?.Equals(currentId) == true);

                if (trackedEntity != null && !ReferenceEquals(trackedEntity, entity))
                {
                    _context.Entry(trackedEntity).State = EntityState.Detached;
                }
            }
        }

        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _dbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(ct);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(predicate, ct);
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(predicate, ct);
    }
}
