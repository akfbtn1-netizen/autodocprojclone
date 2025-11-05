using System.Collections.Generic;
using System.Linq;

namespace Shared.Contracts
{
    /// <summary>
    /// Result of input validation.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Indicates if validation passed.
        /// </summary>
        public bool IsValid => !Errors.Any();
        
        /// <summary>
        /// List of validation errors.
        /// </summary>
        public List<ValidationError> Errors { get; init; } = new();
        
        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static ValidationResult Ok() => new();
        
        /// <summary>
        /// Creates a failed validation result.
        /// </summary>
        public static ValidationResult Fail(params ValidationError[] errors)
        {
            return new ValidationResult
            {
                Errors = errors.ToList()
            };
        }
    }
    
    /// <summary>
    /// Represents a single validation error.
    /// </summary>
    public record ValidationError(string PropertyName, string ErrorMessage);
}