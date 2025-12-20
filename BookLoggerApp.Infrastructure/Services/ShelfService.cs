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
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Shelves
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<Shelf?> GetShelfByIdAsync(Guid id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Shelves
            .Include(s => s.BookShelves)
            .ThenInclude(bs => bs.Book)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Shelf> CreateShelfAsync(Shelf shelf)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Assign sort order to be last
        var maxSortOrder = await context.Shelves.MaxAsync(s => (int?)s.SortOrder) ?? 0;
        shelf.SortOrder = maxSortOrder + 1;

        context.Shelves.Add(shelf);
        await context.SaveChangesAsync();
        return shelf;
    }

    public async Task UpdateShelfAsync(Shelf shelf)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Shelves.Update(shelf);
        await context.SaveChangesAsync();
    }

    public async Task DeleteShelfAsync(Guid id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var shelf = await context.Shelves.FindAsync(id);
        if (shelf != null)
        {
            context.Shelves.Remove(shelf);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddBookToShelfAsync(Guid shelfId, Guid bookId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var exists = await context.BookShelves
            .AnyAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (!exists)
        {
            // Determine position (last)
            var maxPos = await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .MaxAsync(bs => (int?)bs.Position) ?? -1;

            context.BookShelves.Add(new BookShelf
            {
                ShelfId = shelfId,
                BookId = bookId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveBookFromShelfAsync(Guid shelfId, Guid bookId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
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
        using var context = await _contextFactory.CreateDbContextAsync();
        
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
        // This is a bit complex due to reordering. 
        // For now, a simple swap or shift approach is acceptable, but full list reorder is safer.
        // Assuming we might just update the single item for now or handle list reorder logic.
        // Will implement a rigorous re-indexer if drag-and-drop requires it commonly.
        
        using var context = await _contextFactory.CreateDbContextAsync();
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
         using var context = await _contextFactory.CreateDbContextAsync();
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
             }
             return await query.ToListAsync();
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
}
