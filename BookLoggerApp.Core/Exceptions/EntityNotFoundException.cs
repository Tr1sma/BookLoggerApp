namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Exception thrown when an entity is not found in the database.
/// </summary>
public class EntityNotFoundException : BookLoggerException
{
    public EntityNotFoundException(Type entityType, Guid id)
        : base($"{entityType.Name} with ID {id} not found")
    {
        EntityType = entityType;
        EntityId = id;
    }

    public Type EntityType { get; }
    public Guid EntityId { get; }
}
