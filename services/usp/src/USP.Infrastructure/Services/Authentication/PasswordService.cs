using System.Text;
using System.Text.RegularExpressions;
using Konscious.Security.Cryptography;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Provides password hashing, verification, and strength validation using Argon2id.
/// </summary>
public class PasswordService : IPasswordService
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 4;
    private const int MemorySize = 65536; // 64 MB (in KB)
    private const int DegreeOfParallelism = 4;

    /// <summary>
    /// Hashes a plaintext password using Argon2id.
    /// Parameters: 64MB memory, 4 iterations, 4 parallelism
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or whitespace", nameof(password));
        }

        // Generate random salt
        var salt = GenerateRandomSalt();

        // Hash password with Argon2id
        var hash = HashPasswordInternal(password, salt);

        // Combine salt and hash, encode as Base64
        var hashBytes = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Verifies a plaintext password against a hashed password.
    /// </summary>
    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            throw new ArgumentException("Hashed password cannot be null or whitespace", nameof(hashedPassword));
        }

        if (string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        try
        {
            // Decode the Base64 hash
            var hashBytes = Convert.FromBase64String(hashedPassword);

            if (hashBytes.Length != SaltSize + HashSize)
            {
                return false;
            }

            // Extract salt and stored hash
            var salt = new byte[SaltSize];
            var storedHash = new byte[HashSize];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(hashBytes, SaltSize, storedHash, 0, HashSize);

            // Hash the provided password with the extracted salt
            var newHash = HashPasswordInternal(providedPassword, salt);

            // Compare hashes in constant time to prevent timing attacks
            return CryptographicEquals(storedHash, newHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates password strength against security requirements.
    /// Requirements: Min 12 chars, uppercase, lowercase, digit, special char
    /// </summary>
    public PasswordStrengthResult ValidatePasswordStrength(string password)
    {
        var result = new PasswordStrengthResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(password))
        {
            result.IsValid = false;
            result.Errors.Add("Password cannot be empty");
            return result;
        }

        if (password.Length < 12)
        {
            result.IsValid = false;
            result.Errors.Add("Password must be at least 12 characters long");
        }

        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            result.IsValid = false;
            result.Errors.Add("Password must contain at least one uppercase letter");
        }

        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            result.IsValid = false;
            result.Errors.Add("Password must contain at least one lowercase letter");
        }

        if (!Regex.IsMatch(password, @"[0-9]"))
        {
            result.IsValid = false;
            result.Errors.Add("Password must contain at least one digit");
        }

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
        {
            result.IsValid = false;
            result.Errors.Add("Password must contain at least one special character");
        }

        return result;
    }

    private byte[] HashPasswordInternal(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };

        return argon2.GetBytes(HashSize);
    }

    private static byte[] GenerateRandomSalt()
    {
        var salt = new byte[SaltSize];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    private static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
