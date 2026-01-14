using System.Text.RegularExpressions;

namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Represents a securely hashed password using BCrypt.
/// Enforces password complexity requirements.
/// </summary>
public sealed class PasswordHash
{
    private const int MinLength = 8;
    private static readonly Regex UppercaseRegex = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex LowercaseRegex = new(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new(@"\d", RegexOptions.Compiled);
    private static readonly Regex SpecialCharRegex = new(@"[!@#$%^&*(),.?""':{}|<>]", RegexOptions.Compiled);

    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a password hash from plain text (for registration).
    /// </summary>
    public static Result<PasswordHash> Create(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
        {
            return Result<PasswordHash>.Failure("Password cannot be empty");
        }

        if (plainPassword.Length < MinLength)
        {
            return Result<PasswordHash>.Failure($"Password must be at least {MinLength} characters");
        }

        if (!UppercaseRegex.IsMatch(plainPassword))
        {
            return Result<PasswordHash>.Failure("Password must contain at least one uppercase letter");
        }

        if (!LowercaseRegex.IsMatch(plainPassword))
        {
            return Result<PasswordHash>.Failure("Password must contain at least one lowercase letter");
        }

        if (!DigitRegex.IsMatch(plainPassword))
        {
            return Result<PasswordHash>.Failure("Password must contain at least one digit");
        }

        if (!SpecialCharRegex.IsMatch(plainPassword))
        {
            return Result<PasswordHash>.Failure("Password must contain at least one special character");
        }

        try
        {
            var hash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainPassword));
            return Result<PasswordHash>.Success(new PasswordHash(hash));
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            return Result<PasswordHash>.Failure($"Failed to hash password: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return Result<PasswordHash>.Failure($"Failed to hash password: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a PasswordHash from an already-hashed value (for loading from database).
    /// </summary>
    public static PasswordHash FromHash(string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            throw new ArgumentException("Hashed password cannot be empty", nameof(hashedPassword));
        }

        return new PasswordHash(hashedPassword);
    }

    /// <summary>
    /// Verifies a plain password against this hash.
    /// </summary>
    public bool Verify(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
        {
            return false;
        }

        try
        {
            var testHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainPassword));
            return testHash == Value;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public override string ToString() => "[REDACTED]";
}

/// <summary>
/// Shared result type for operations that can succeed or fail.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string Error { get; }

    private Result(bool isSuccess, T? value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, string.Empty);
    public static Result<T> Failure(string error) => new(false, default, error);
}
