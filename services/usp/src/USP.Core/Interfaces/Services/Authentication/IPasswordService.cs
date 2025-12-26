namespace USP.Core.Interfaces.Services.Authentication;

/// <summary>
/// Provides password hashing, verification, and strength validation services.
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Hashes a plaintext password using Argon2id.
    /// Parameters: 64MB memory, 4 iterations, 4 parallelism
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>The hashed password (Base64 encoded)</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a hashed password.
    /// </summary>
    /// <param name="hashedPassword">The hashed password</param>
    /// <param name="providedPassword">The plaintext password to verify</param>
    /// <returns>True if the password matches, false otherwise</returns>
    bool VerifyPassword(string hashedPassword, string providedPassword);

    /// <summary>
    /// Validates password strength against security requirements.
    /// Requirements: Min 12 chars, uppercase, lowercase, digit, special char
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>Validation result with error messages if invalid</returns>
    PasswordStrengthResult ValidatePasswordStrength(string password);
}

/// <summary>
/// Result of password strength validation.
/// </summary>
public class PasswordStrengthResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
