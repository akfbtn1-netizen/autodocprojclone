
namespace Enterprise.Documentation.Core.Domain.Exceptions;

/// <summary>
/// Exception thrown when attempting an operation that is invalid for the current version status.
/// </summary>
public class InvalidVersionStatusException : DomainException
{
    public InvalidVersionStatusException(string message) : base("INVALID_VERSION_STATUS", message) { }
    
    public InvalidVersionStatusException(string message, Exception innerException) 
        : base("INVALID_VERSION_STATUS", message, innerException) { }
}