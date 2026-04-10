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
    Task<List<UserPlant>> GetPlantsForShelfAsync(Guid shelfId);

    // Decoration Management
    Task AddDecorationToShelfAsync(Guid shelfId, Guid decorationId);
    Task RemoveDecorationFromShelfAsync(Guid shelfId, Guid decorationId);

    // Positioning
    Task UpdateShelfPositionsAsync(Guid shelfId, Dictionary<Guid, int> bookPositions, Dictionary<Guid, int> plantPositions, Dictionary<Guid, int> decorationPositions);

    // Cross-shelf movement
    Task MoveBookBetweenShelvesAsync(Guid sourceShelfId, Guid targetShelfId, Guid bookId, int targetPosition);
    Task MovePlantBetweenShelvesAsync(Guid sourceShelfId, Guid targetShelfId, Guid plantId, int targetPosition);
    Task MoveDecorationBetweenShelvesAsync(Guid sourceShelfId, Guid targetShelfId, Guid decorationId, int targetPosition);
}
