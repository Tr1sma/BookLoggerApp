using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

public class ShelfService : IShelfService
{
    private const int FreeTierShelfCap = 3;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFeatureGuard? _featureGuard;

    public ShelfService(IDbContextFactory<AppDbContext> contextFactory, IFeatureGuard? featureGuard = null)
    {
        _contextFactory = contextFactory;
        _featureGuard = featureGuard;
    }

    public async Task<List<Shelf>> GetAllShelvesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Shelves
            .Where(s => !s.IsHiddenByEntitlement)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<Shelf?> GetShelfByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Shelves
            .Include(s => s.BookShelves)
            .ThenInclude(bs => bs.Book)
            .Include(s => s.PlantShelves)
            .ThenInclude(ps => ps.Plant)
            .ThenInclude(p => p.Species)
            .Include(s => s.DecorationShelves)
            .ThenInclude(ds => ds.Decoration)
            .ThenInclude(d => d.ShopItem)
            // Same entitlement filter as GetAllShelvesAsync, so deep-links can't surface a hidden shelf.
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsHiddenByEntitlement, ct);
    }

    public async Task<Shelf> CreateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        if (_featureGuard is not null)
        {
            int visibleShelfCount = await context.Shelves.CountAsync(s => !s.IsHiddenByEntitlement, ct);
            _featureGuard.EnforceSoftLimit(
                FeatureKey.UnlimitedShelves,
                visibleShelfCount,
                FreeTierShelfCap,
                $"Free tier is limited to {FreeTierShelfCap} shelves. Upgrade to Plus for unlimited shelves.");
        }

        // Sort last
        var maxSortOrder = await context.Shelves.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0;
        shelf.SortOrder = maxSortOrder + 1;

        context.Shelves.Add(shelf);
        await context.SaveChangesAsync(ct);
        return shelf;
    }

    public async Task UpdateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Shelves.Update(shelf);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteShelfAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var shelf = await context.Shelves.FindAsync(new object?[] { id }, ct);
            if (shelf == null) return;

            // Find or create the Main shelf (SortOrder == 0)
            var mainShelf = await context.Shelves
                .FirstOrDefaultAsync(s => s.SortOrder == 0 && s.Id != id, ct);

            if (mainShelf == null)
            {
                mainShelf = new Shelf { Name = "Main Shelf", SortOrder = 0 };
                context.Shelves.Add(mainShelf);
                await context.SaveChangesAsync(ct);
            }

            // Current max position on the Main shelf
            var maxBookPos = await context.BookShelves.Where(bs => bs.ShelfId == mainShelf.Id)
                .MaxAsync(bs => (int?)bs.Position, ct) ?? -1;
            var maxPlantPos = await context.PlantShelves.Where(ps => ps.ShelfId == mainShelf.Id)
                .MaxAsync(ps => (int?)ps.Position, ct) ?? -1;
            var maxDecoPos = await context.DecorationShelves.Where(ds => ds.ShelfId == mainShelf.Id)
                .MaxAsync(ds => (int?)ds.Position, ct) ?? -1;
            var maxPos = Math.Max(maxBookPos, Math.Max(maxPlantPos, maxDecoPos));

            // Move books to Main shelf (skip duplicates already on Main shelf)
            var booksToMove = await context.BookShelves
                .Where(bs => bs.ShelfId == id)
                .ToListAsync(ct);

            var existingBookIds = await context.BookShelves
                .Where(bs => bs.ShelfId == mainShelf.Id)
                .Select(bs => bs.BookId)
                .ToHashSetAsync(ct);

            foreach (var bs in booksToMove)
            {
                context.BookShelves.Remove(bs);
                if (!existingBookIds.Contains(bs.BookId))
                {
                    maxPos++;
                    context.BookShelves.Add(new BookShelf
                    {
                        ShelfId = mainShelf.Id,
                        BookId = bs.BookId,
                        Position = maxPos
                    });
                }
            }

            // Move plants to Main shelf (skip duplicates already on Main shelf)
            var plantsToMove = await context.PlantShelves
                .Where(ps => ps.ShelfId == id)
                .ToListAsync(ct);

            var existingPlantIds = await context.PlantShelves
                .Where(ps => ps.ShelfId == mainShelf.Id)
                .Select(ps => ps.PlantId)
                .ToHashSetAsync(ct);

            foreach (var ps in plantsToMove)
            {
                context.PlantShelves.Remove(ps);
                if (!existingPlantIds.Contains(ps.PlantId))
                {
                    maxPos++;
                    context.PlantShelves.Add(new PlantShelf
                    {
                        ShelfId = mainShelf.Id,
                        PlantId = ps.PlantId,
                        Position = maxPos
                    });
                }
            }

            // Move decorations to Main shelf (skip duplicates already on Main shelf)
            var decorationsToMove = await context.DecorationShelves
                .Where(ds => ds.ShelfId == id)
                .ToListAsync(ct);

            var existingDecorationIds = await context.DecorationShelves
                .Where(ds => ds.ShelfId == mainShelf.Id)
                .Select(ds => ds.DecorationId)
                .ToHashSetAsync(ct);

            foreach (var ds in decorationsToMove)
            {
                context.DecorationShelves.Remove(ds);
                if (!existingDecorationIds.Contains(ds.DecorationId))
                {
                    maxPos++;
                    context.DecorationShelves.Add(new DecorationShelf
                    {
                        ShelfId = mainShelf.Id,
                        DecorationId = ds.DecorationId,
                        Position = maxPos
                    });
                }
            }

            context.Shelves.Remove(shelf);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Roll back with None so cleanup runs even if ct triggered the failure.
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AddBookToShelfAsync(Guid shelfId, Guid bookId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var exists = await context.BookShelves
            .AnyAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId, ct);

        if (!exists)
        {
            // Append to the shelf's unified Position sequence (books/plants/decorations share it);
            // inserting at 0 collided with existing items and gave unstable ordering. (INK-02)
            var maxPos = await GetMaxPositionOnShelfAsync(context, shelfId, ct);

            context.BookShelves.Add(new BookShelf
            {
                ShelfId = shelfId,
                BookId = bookId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveBookFromShelfAsync(Guid shelfId, Guid bookId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var bookShelf = await context.BookShelves
            .FirstOrDefaultAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId, ct);

        if (bookShelf != null)
        {
            context.BookShelves.Remove(bookShelf);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task ReorderShelvesAsync(List<Guid> shelfIdsInOrder, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var shelves = await context.Shelves
            .Where(s => shelfIdsInOrder.Contains(s.Id))
            .ToListAsync(ct);

        foreach (var shelf in shelves)
        {
            var index = shelfIdsInOrder.IndexOf(shelf.Id);
            if (index != -1)
            {
                shelf.SortOrder = index;
            }
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateBookPositionAsync(Guid shelfId, Guid bookId, int newPosition, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var targetEntry = await context.BookShelves
            .FirstOrDefaultAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId, ct);

        if (targetEntry != null)
        {
            targetEntry.Position = newPosition;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<Book>> GetBooksForShelfAsync(Guid shelfId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var shelf = await context.Shelves.FindAsync(new object?[] { shelfId }, ct);
        if (shelf == null) return new List<Book>();

        if (shelf.AutoSortRule != ShelfAutoSortRule.None)
        {
            // Dynamic query based on rule
            var query = context.Books.AsQueryable();
            switch (shelf.AutoSortRule)
            {
                case ShelfAutoSortRule.StatusPlanned:
                    query = query.Where(b => b.Status == ReadingStatus.Planned);
                    break;
                case ShelfAutoSortRule.StatusReading:
                    query = query.Where(b => b.Status == ReadingStatus.Reading);
                    break;
                case ShelfAutoSortRule.StatusCompleted:
                    query = query.Where(b => b.Status == ReadingStatus.Completed);
                    break;
                case ShelfAutoSortRule.StatusAbandoned:
                    query = query.Where(b => b.Status == ReadingStatus.Abandoned);
                    break;
                case ShelfAutoSortRule.StatusWishlist:
                    query = query.Where(b => b.Status == ReadingStatus.Wishlist);
                    break;
            }
            return await query.OrderByDescending(b => b.DateAdded).ToListAsync(ct);
        }
        else
        {
            // Standard manual shelf
            return await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .OrderBy(bs => bs.Position)
                .Select(bs => bs.Book)
                .ToListAsync(ct);
        }
    }

    public async Task AddPlantToShelfAsync(Guid shelfId, Guid plantId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var exists = await context.PlantShelves
            .AnyAsync(ps => ps.ShelfId == shelfId && ps.PlantId == plantId, ct);

        if (!exists)
        {
            var maxPos = await GetMaxPositionOnShelfAsync(context, shelfId, ct);

            context.PlantShelves.Add(new PlantShelf
            {
                ShelfId = shelfId,
                PlantId = plantId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task RemovePlantFromShelfAsync(Guid shelfId, Guid plantId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var plantShelf = await context.PlantShelves
            .FirstOrDefaultAsync(ps => ps.ShelfId == shelfId && ps.PlantId == plantId, ct);

        if (plantShelf != null)
        {
            context.PlantShelves.Remove(plantShelf);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<UserPlant>> GetPlantsForShelfAsync(Guid shelfId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.PlantShelves
            .Include(ps => ps.Plant)
                .ThenInclude(p => p.Species)
            .Where(ps => ps.ShelfId == shelfId)
            .OrderBy(ps => ps.Position)
            .Select(ps => ps.Plant)
            .ToListAsync(ct);
    }

    public async Task UpdateShelfPositionsAsync(Guid shelfId, Dictionary<Guid, int> bookPositions, Dictionary<Guid, int> plantPositions, Dictionary<Guid, int> decorationPositions, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            if (bookPositions.Any())
            {
                var bookIds = bookPositions.Keys.ToList();
                var bookShelves = await context.BookShelves
                    .Where(bs => bs.ShelfId == shelfId && bookIds.Contains(bs.BookId))
                    .ToListAsync(ct);

                foreach (var bs in bookShelves)
                {
                    if (bookPositions.TryGetValue(bs.BookId, out var newPos))
                    {
                        bs.Position = newPos;
                    }
                }
            }

            if (plantPositions.Any())
            {
                var plantIds = plantPositions.Keys.ToList();
                var plantShelves = await context.PlantShelves
                    .Where(ps => ps.ShelfId == shelfId && plantIds.Contains(ps.PlantId))
                    .ToListAsync(ct);

                foreach (var ps in plantShelves)
                {
                    if (plantPositions.TryGetValue(ps.PlantId, out var newPos))
                    {
                        ps.Position = newPos;
                    }
                }
            }

            if (decorationPositions.Any())
            {
                var decorationIds = decorationPositions.Keys.ToList();
                var decorationShelves = await context.DecorationShelves
                    .Where(ds => ds.ShelfId == shelfId && decorationIds.Contains(ds.DecorationId))
                    .ToListAsync(ct);

                foreach (var ds in decorationShelves)
                {
                    if (decorationPositions.TryGetValue(ds.DecorationId, out var newPos))
                    {
                        ds.Position = newPos;
                    }
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task MoveBookBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid bookId, int targetPosition, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var sourceEntry = await context.BookShelves
                .FirstOrDefaultAsync(bs => bs.ShelfId == sourceShelfId && bs.BookId == bookId, ct);
            if (sourceEntry != null)
                context.BookShelves.Remove(sourceEntry);

            var existsOnTarget = await context.BookShelves
                .AnyAsync(bs => bs.ShelfId == targetShelfId && bs.BookId == bookId, ct);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    insertPos = await GetMaxPositionOnShelfAsync(context, targetShelfId, ct) + 1;
                }
                else
                {
                    await ShiftItemsOnShelfAsync(context, targetShelfId, insertPos, ct);
                }

                context.BookShelves.Add(new BookShelf
                {
                    ShelfId = targetShelfId,
                    BookId = bookId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task MovePlantBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid plantId, int targetPosition, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var sourceEntry = await context.PlantShelves
                .FirstOrDefaultAsync(ps => ps.ShelfId == sourceShelfId && ps.PlantId == plantId, ct);
            if (sourceEntry != null)
                context.PlantShelves.Remove(sourceEntry);

            var existsOnTarget = await context.PlantShelves
                .AnyAsync(ps => ps.ShelfId == targetShelfId && ps.PlantId == plantId, ct);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    insertPos = await GetMaxPositionOnShelfAsync(context, targetShelfId, ct) + 1;
                }
                else
                {
                    await ShiftItemsOnShelfAsync(context, targetShelfId, insertPos, ct);
                }

                context.PlantShelves.Add(new PlantShelf
                {
                    ShelfId = targetShelfId,
                    PlantId = plantId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AddDecorationToShelfAsync(Guid shelfId, Guid decorationId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var exists = await context.DecorationShelves
            .AnyAsync(ds => ds.ShelfId == shelfId && ds.DecorationId == decorationId, ct);

        if (!exists)
        {
            var maxPos = await GetMaxPositionOnShelfAsync(context, shelfId, ct);

            context.DecorationShelves.Add(new DecorationShelf
            {
                ShelfId = shelfId,
                DecorationId = decorationId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveDecorationFromShelfAsync(Guid shelfId, Guid decorationId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entry = await context.DecorationShelves
            .FirstOrDefaultAsync(ds => ds.ShelfId == shelfId && ds.DecorationId == decorationId, ct);

        if (entry != null)
        {
            context.DecorationShelves.Remove(entry);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task MoveDecorationBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid decorationId, int targetPosition, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var sourceEntry = await context.DecorationShelves
                .FirstOrDefaultAsync(ds => ds.ShelfId == sourceShelfId && ds.DecorationId == decorationId, ct);
            if (sourceEntry != null)
                context.DecorationShelves.Remove(sourceEntry);

            var existsOnTarget = await context.DecorationShelves
                .AnyAsync(ds => ds.ShelfId == targetShelfId && ds.DecorationId == decorationId, ct);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    insertPos = await GetMaxPositionOnShelfAsync(context, targetShelfId, ct) + 1;
                }
                else
                {
                    await ShiftItemsOnShelfAsync(context, targetShelfId, insertPos, ct);
                }

                context.DecorationShelves.Add(new DecorationShelf
                {
                    ShelfId = targetShelfId,
                    DecorationId = decorationId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Returns the maximum Position across books, plants, and decorations on a shelf.
    /// Returns -1 if the shelf is empty.
    /// </summary>
    private static async Task<int> GetMaxPositionOnShelfAsync(AppDbContext context, Guid shelfId, CancellationToken ct)
    {
        var maxBookPos = await context.BookShelves
            .Where(bs => bs.ShelfId == shelfId)
            .MaxAsync(bs => (int?)bs.Position, ct) ?? -1;
        var maxPlantPos = await context.PlantShelves
            .Where(ps => ps.ShelfId == shelfId)
            .MaxAsync(ps => (int?)ps.Position, ct) ?? -1;
        var maxDecoPos = await context.DecorationShelves
            .Where(ds => ds.ShelfId == shelfId)
            .MaxAsync(ds => (int?)ds.Position, ct) ?? -1;
        return Math.Max(maxBookPos, Math.Max(maxPlantPos, maxDecoPos));
    }

    /// <summary>
    /// Shifts all items at or beyond a given position on a shelf by +1.
    /// </summary>
    private static async Task ShiftItemsOnShelfAsync(AppDbContext context, Guid shelfId, int fromPosition, CancellationToken ct)
    {
        var booksToShift = await context.BookShelves
            .Where(bs => bs.ShelfId == shelfId && bs.Position >= fromPosition)
            .ToListAsync(ct);
        foreach (var b in booksToShift) b.Position++;

        var plantsToShift = await context.PlantShelves
            .Where(ps => ps.ShelfId == shelfId && ps.Position >= fromPosition)
            .ToListAsync(ct);
        foreach (var p in plantsToShift) p.Position++;

        var decorationsToShift = await context.DecorationShelves
            .Where(ds => ds.ShelfId == shelfId && ds.Position >= fromPosition)
            .ToListAsync(ct);
        foreach (var d in decorationsToShift) d.Position++;
    }
}
