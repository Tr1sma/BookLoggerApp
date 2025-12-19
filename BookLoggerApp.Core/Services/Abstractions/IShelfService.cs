using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IShelfService
{
    Task<List<Shelf>> GetAllShelvesAsync();
    Task<Shelf?> GetShelfByIdAsync(Guid id);
    Task<Shelf> CreateShelfAsync(Shelf shelf);
    Task UpdateShelfAsync(Shelf shelf);
    Task DeleteShelfAsync(Guid id);
    
    // Management
    Task AddBookToShelfAsync(Guid shelfId, Guid bookId);
    Task RemoveBookFromShelfAsync(Guid shelfId, Guid bookId);
    Task ReorderShelvesAsync(List<Guid> shelfIdsInOrder);
    Task UpdateBookPositionAsync(Guid shelfId, Guid bookId, int newPosition);
    Task<List<Book>> GetBooksForShelfAsync(Guid shelfId);
}
