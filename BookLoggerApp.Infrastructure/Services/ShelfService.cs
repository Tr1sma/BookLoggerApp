using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

public class ShelfService : IShelfService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ShelfService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Shelf>> GetAllShelvesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Shelves
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<Shelf?> GetShelfByIdAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Shelves
            .Include(s => s.BookShelves)
            .ThenInclude(bs => bs.Book)
            .Include(s => s.PlantShelves)
            .ThenInclude(ps => ps.Plant)
            .ThenInclude(p => p.Species)
            .Include(s => s.DecorationShelves)
            .ThenInclude(ds => ds.Decoration)
            .ThenInclude(d => d.ShopItem)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Shelf> CreateShelfAsync(Shelf shelf)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Assign sort order to be last
        var maxSortOrder = await context.Shelves.MaxAsync(s => (int?)s.SortOrder) ?? 0;
        shelf.SortOrder = maxSortOrder + 1;

        context.Shelves.Add(shelf);
        await context.SaveChangesAsync();
        return shelf;
    }

    public async Task UpdateShelfAsync(Shelf shelf)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Shelves.Update(shelf);
        await context.SaveChangesAsync();
    }

    public async Task DeleteShelfAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var shelf = await context.Shelves.FindAsync(id);
            if (shelf == null) return;

            // Find or create the Main shelf (SortOrder == 0)
            var mainShelf = await context.Shelves
                .FirstOrDefaultAsync(s => s.SortOrder == 0 && s.Id != id);

            if (mainShelf == null)
            {
                mainShelf = new Shelf { Name = "Main Shelf", SortOrder = 0 };
                context.Shelves.Add(mainShelf);
                await context.SaveChangesAsync();
            }

            // Get current max position on the Main shelf
            var maxBookPos = await context.BookShelves.Where(bs => bs.ShelfId == mainShelf.Id)
                .MaxAsync(bs => (int?)bs.Position) ?? -1;
            var maxPlantPos = await context.PlantShelves.Where(ps => ps.ShelfId == mainShelf.Id)
                .MaxAsync(ps => (int?)ps.Position) ?? -1;
            var maxDecoPos = await context.DecorationShelves.Where(ds => ds.ShelfId == mainShelf.Id)
                .MaxAsync(ds => (int?)ds.Position) ?? -1;
            var maxPos = Math.Max(maxBookPos, Math.Max(maxPlantPos, maxDecoPos));

            // Move books to Main shelf (skip duplicates already on Main shelf)
            var booksToMove = await context.BookShelves
                .Where(bs => bs.ShelfId == id)
                .ToListAsync();

            var existingBookIds = await context.BookShelves
                .Where(bs => bs.ShelfId == mainShelf.Id)
                .Select(bs => bs.BookId)
                .ToHashSetAsync();

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
                .ToListAsync();

            var existingPlantIds = await context.PlantShelves
                .Where(ps => ps.ShelfId == mainShelf.Id)
                .Select(ps => ps.PlantId)
                .ToHashSetAsync();

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
                .ToListAsync();

            var existingDecorationIds = await context.DecorationShelves
                .Where(ds => ds.ShelfId == mainShelf.Id)
                .Select(ds => ds.DecorationId)
                .ToHashSetAsync();

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
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddBookToShelfAsync(Guid shelfId, Guid bookId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.BookShelves
            .AnyAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (!exists)
        {
            // Shift all existing items forward so the new book goes to position 0 (first)
            var existingEntries = await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .ToListAsync();

            foreach (var entry in existingEntries)
            {
                entry.Position += 1;
            }

            context.BookShelves.Add(new BookShelf
            {
                ShelfId = shelfId,
                BookId = bookId,
                Position = 0
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveBookFromShelfAsync(Guid shelfId, Guid bookId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bookShelf = await context.BookShelves
            .FirstOrDefaultAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (bookShelf != null)
        {
            context.BookShelves.Remove(bookShelf);
            await context.SaveChangesAsync();
        }
    }

    public async Task ReorderShelvesAsync(List<Guid> shelfIdsInOrder)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var shelves = await context.Shelves
            .Where(s => shelfIdsInOrder.Contains(s.Id))
            .ToListAsync();

        foreach (var shelf in shelves)
        {
            var index = shelfIdsInOrder.IndexOf(shelf.Id);
            if (index != -1)
            {
                shelf.SortOrder = index;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateBookPositionAsync(Guid shelfId, Guid bookId, int newPosition)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var targetEntry = await context.BookShelves
            .FirstOrDefaultAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (targetEntry != null)
        {
            targetEntry.Position = newPosition;
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Book>> GetBooksForShelfAsync(Guid shelfId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var shelf = await context.Shelves.FindAsync(shelfId);
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
            return await query.OrderByDescending(b => b.DateAdded).ToListAsync();
        }
        else
        {
            // Standard manual shelf
            return await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .OrderBy(bs => bs.Position)
                .Select(bs => bs.Book)
                .ToListAsync();
        }
    }

    public async Task AddPlantToShelfAsync(Guid shelfId, Guid plantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.PlantShelves
            .AnyAsync(ps => ps.ShelfId == shelfId && ps.PlantId == plantId);

        if (!exists)
        {
            var maxPos = await GetMaxPositionOnShelfAsync(context, shelfId);

            context.PlantShelves.Add(new PlantShelf
            {
                ShelfId = shelfId,
                PlantId = plantId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemovePlantFromShelfAsync(Guid shelfId, Guid plantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plantShelf = await context.PlantShelves
            .FirstOrDefaultAsync(ps => ps.ShelfId == shelfId && ps.PlantId == plantId);

        if (plantShelf != null)
        {
            context.PlantShelves.Remove(plantShelf);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<UserPlant>> GetPlantsForShelfAsync(Guid shelfId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PlantShelves
            .Include(ps => ps.Plant)
                .ThenInclude(p => p.Species)
            .Where(ps => ps.ShelfId == shelfId)
            .OrderBy(ps => ps.Position)
            .Select(ps => ps.Plant)
            .ToListAsync();
    }

    public async Task UpdateShelfPositionsAsync(Guid shelfId, Dictionary<Guid, int> bookPositions, Dictionary<Guid, int> plantPositions, Dictionary<Guid, int> decorationPositions)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            if (bookPositions.Any())
            {
                var bookIds = bookPositions.Keys.ToList();
                var bookShelves = await context.BookShelves
                    .Where(bs => bs.ShelfId == shelfId && bookIds.Contains(bs.BookId))
                    .ToListAsync();

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
                    .ToListAsync();

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
                    .ToListAsync();

                foreach (var ds in decorationShelves)
                {
                    if (decorationPositions.TryGetValue(ds.DecorationId, out var newPos))
                    {
                        ds.Position = newPos;
                    }
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task MoveBookBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid bookId, int targetPosition)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var sourceEntry = await context.BookShelves
                .FirstOrDefaultAsync(bs => bs.ShelfId == sourceShelfId && bs.BookId == bookId);
            if (sourceEntry != null)
                context.BookShelves.Remove(sourceEntry);

            var existsOnTarget = await context.BookShelves
                .AnyAsync(bs => bs.ShelfId == targetShelfId && bs.BookId == bookId);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    insertPos = await GetMaxPositionOnShelfAsync(context, targetShelfId) + 1;
                }
                else
                {
                    await ShiftItemsOnShelfAsync(context, targetShelfId, insertPos);
                }

                context.BookShelves.Add(new BookShelf
                {
                    ShelfId = targetShelfId,
                    BookId = bookId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task MovePlantBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid plantId, int targetPosition)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var sourceEntry = await context.PlantShelves
                .FirstOrDefaultAsync(ps => ps.ShelfId == sourceShelfId && ps.PlantId == plantId);
            if (sourceEntry != null)
                context.PlantShelves.Remove(sourceEntry);

            var existsOnTarget = await context.PlantShelves
                .AnyAsync(ps => ps.ShelfId == targetShelfId && ps.PlantId == plantId);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    insertPos = await GetMaxPositionOnShelfAsync(context, targetShelfId) + 1;
                }
                else
                {
                    await ShiftItemsOnShelfAsync(context, targetShelfId, insertPos);
                }

                context.PlantShelves.Add(new PlantShelf
                {
                    ShelfId = targetShelfId,
                    PlantId = plantId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddDecorationToShelfAsync(Guid shelfId, Guid decorationId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.DecorationShelves
            .AnyAsync(ds => ds.ShelfId == shelfId && ds.DecorationId == decorationId);

        if (!exists)
        {
            var maxPos = await GetMaxPositionOnShelfAsync(context, shelfId);

            context.DecorationShelves.Add(new DecorationShelf
            {
                ShelfId = shelfId,
                DecorationId = decorationId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveDecorationFromShelfAsync(Guid shelfId, Guid decorationId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entry = await context.DecorationShelves
            .FirstOrDefaultAsync(ds => ds.ShelfId == shelfId && ds.DecorationId == decorationId);

        if (entry != null)
        {
            context.DecorationShelves.Remove(entry);
            await context.SaveChangesAsync();
        }
    }

    public async Task MoveDecorationBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid decorationId, int targetPosition)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var sourceEntry = await context.DecorationShelves
                .FirstOrDefaultAsync(ds => ds.ShelfId == sourceShelfId && ds.DecorationId == decorationId);
            if (sourceEntry != null)
                context.DecorationShelves.Remove(sourceEntry);

            var existsOnTarget = await context.DecorationShelves
                .AnyAsync(ds => ds.ShelfId == targetShelfId && ds.DecorationId == decorationId);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    insertPos = await GetMaxPositionOnShelfAsync(context, targetShelfId) + 1;
                }
                else
                {
                    await ShiftItemsOnShelfAsync(context, targetShelfId, insertPos);
                }

                context.DecorationShelves.Add(new DecorationShelf
                {
                    ShelfId = targetShelfId,
                    DecorationId = decorationId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Returns the maximum Position across books, plants, and decorations on a shelf.
    /// Returns -1 if the shelf is empty.
    /// </summary>
    private static async Task<int> GetMaxPositionOnShelfAsync(AppDbContext context, Guid shelfId)
    {
        var maxBookPos = await context.BookShelves
            .Where(bs => bs.ShelfId == shelfId)
            .MaxAsync(bs => (int?)bs.Position) ?? -1;
        var maxPlantPos = await context.PlantShelves
            .Where(ps => ps.ShelfId == shelfId)
            .MaxAsync(ps => (int?)ps.Position) ?? -1;
        var maxDecoPos = await context.DecorationShelves
            .Where(ds => ds.ShelfId == shelfId)
            .MaxAsync(ds => (int?)ds.Position) ?? -1;
        return Math.Max(maxBookPos, Math.Max(maxPlantPos, maxDecoPos));
    }

    /// <summary>
    /// Shifts all items at or beyond a given position on a shelf by +1.
    /// </summary>
    private static async Task ShiftItemsOnShelfAsync(AppDbContext context, Guid shelfId, int fromPosition)
    {
        var booksToShift = await context.BookShelves
            .Where(bs => bs.ShelfId == shelfId && bs.Position >= fromPosition)
            .ToListAsync();
        foreach (var b in booksToShift) b.Position++;

        var plantsToShift = await context.PlantShelves
            .Where(ps => ps.ShelfId == shelfId && ps.Position >= fromPosition)
            .ToListAsync();
        foreach (var p in plantsToShift) p.Position++;

        var decorationsToShift = await context.DecorationShelves
            .Where(ds => ds.ShelfId == shelfId && ds.Position >= fromPosition)
            .ToListAsync();
        foreach (var d in decorationsToShift) d.Position++;
    }
}
