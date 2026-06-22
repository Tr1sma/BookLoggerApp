using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IShelfService
{
    Task<List<Shelf>> GetAllShelvesAsync(CancellationToken ct = default);
    Task<Shelf?> GetShelfByIdAsync(Guid id, CancellationToken ct = default);
    Task<Shelf> CreateShelfAsync(Shelf shelf, CancellationToken ct = default);
    Task UpdateShelfAsync(Shelf shelf, CancellationToken ct = default);
    Task DeleteShelfAsync(Guid id, CancellationToken ct = default);

    // Management
    Task AddBookToShelfAsync(Guid shelfId, Guid bookId, CancellationToken ct = default);
    Task RemoveBookFromShelfAsync(Guid shelfId, Guid bookId, CancellationToken ct = default);
    Task ReorderShelvesAsync(List<Guid> shelfIdsInOrder, CancellationToken ct = default);
    Task UpdateBookPositionAsync(Guid shelfId, Guid bookId, int newPosition, CancellationToken ct = default);
    Task<List<Book>> GetBooksForShelfAsync(Guid shelfId, CancellationToken ct = default);

    // Plant Management
    Task AddPlantToShelfAsync(Guid shelfId, Guid plantId, CancellationToken ct = default);
    Task RemovePlantFromShelfAsync(Guid shelfId, Guid plantId, CancellationToken ct = default);
    Task<List<UserPlant>> GetPlantsForShelfAsync(Guid shelfId, CancellationToken ct = default);

    // Decoration Management
    Task AddDecorationToShelfAsync(Guid shelfId, Guid decorationId, CancellationToken ct = default);
    Task RemoveDecorationFromShelfAsync(Guid shelfId, Guid decorationId, CancellationToken ct = default);

    // Positioning
    Task UpdateShelfPositionsAsync(Guid shelfId, Dictionary<Guid, int> bookPositions, Dictionary<Guid, int> plantPositions, Dictionary<Guid, int> decorationPositions, CancellationToken ct = default);

    // Cross-shelf movement
    Task MoveBookBetweenShelvesAsync(Guid sourceShelfId, Guid targetShelfId, Guid bookId, int targetPosition, CancellationToken ct = default);
    Task MovePlantBetweenShelvesAsync(Guid sourceShelfId, Guid targetShelfId, Guid plantId, int targetPosition, CancellationToken ct = default);
    Task MoveDecorationBetweenShelvesAsync(Guid sourceShelfId, Guid targetShelfId, Guid decorationId, int targetPosition, CancellationToken ct = default);
}
