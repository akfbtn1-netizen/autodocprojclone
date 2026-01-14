
namespace Enterprise.Documentation.Core.Domain.Exceptions;

/// <summary>
/// Exception thrown when an entity is not found.
/// </summary>
public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string message) : base("ENTITY_NOT_FOUND", message) { }
    
    public EntityNotFoundException(string message, Exception innerException) 
        : base("ENTITY_NOT_FOUND", message, innerException) { }
}