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

    // Plant Management
    Task AddPlantToShelfAsync(Guid shelfId, Guid plantId);
    Task RemovePlantFromShelfAsync(Guid shelfId, Guid plantId);
    Task UpdatePlantPositionAsync(Guid shelfId, Guid plantId, int newPosition);

    // Mixed Item Management
    Task<List<ShelfItemDto>> GetShelfItemsAsync(Guid shelfId);
    Task ReorderShelfItemsAsync(Guid shelfId, List<ShelfItemDto> itemsInOrder);
}
