using System.Security.Cryptography;
using System.Text;
using USP.Core.Services.Secrets;

namespace USP.Infrastructure.Services.Secrets.DatabaseConnectors;

/// <summary>
/// Base class for database connectors with common utility methods
/// </summary>
public abstract class BaseDatabaseConnector : IDatabaseConnector
{
    public abstract string PluginName { get; }

    public abstract Task<bool> VerifyConnectionAsync(string connectionUrl, string? username, string? password);

    public abstract Task<(string username, string password)> CreateDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string creationStatements,
        int ttlSeconds);

    public abstract Task<bool> RevokeDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string username,
        string? revocationStatements);

    public abstract Task<string> RotateRootCredentialsAsync(
        string connectionUrl,
        string currentUsername,
        string currentPassword,
        string newPassword);

    public virtual Task<bool> RenewDynamicUserAsync(
        string connectionUrl,
        string adminUsername,
        string adminPassword,
        string username,
        string? renewStatements,
        int additionalTtlSeconds)
    {
        // Default implementation: renewal not supported
        return Task.FromResult(false);
    }

    public virtual string GenerateUsername(string roleName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var randomSuffix = GenerateRandomString(8);
        var sanitizedRole = SanitizeIdentifier(roleName);

        // Format: v_{rolename}_{timestamp}_{random}
        return $"v_{sanitizedRole}_{timestamp}_{randomSuffix}".ToLower();
    }

    public virtual string GeneratePassword(int length = 32)
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";
        const string allChars = upperCase + lowerCase + digits + specialChars;

        using var rng = RandomNumberGenerator.Create();
        var password = new StringBuilder(length);

        // Ensure at least one character from each category
        password.Append(GetRandomChar(upperCase, rng));
        password.Append(GetRandomChar(lowerCase, rng));
        password.Append(GetRandomChar(digits, rng));
        password.Append(GetRandomChar(specialChars, rng));

        // Fill the rest randomly
        for (int i = 4; i < length; i++)
        {
            password.Append(GetRandomChar(allChars, rng));
        }

        // Shuffle the password
        return ShuffleString(password.ToString(), rng);
    }

    protected string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        using var rng = RandomNumberGenerator.Create();
        var result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = GetRandomChar(chars, rng);
        }

        return new string(result);
    }

    protected char GetRandomChar(string chars, RandomNumberGenerator rng)
    {
        var randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        var randomIndex = BitConverter.ToUInt32(randomBytes, 0) % chars.Length;
        return chars[(int)randomIndex];
    }

    protected string ShuffleString(string input, RandomNumberGenerator rng)
    {
        var array = input.ToCharArray();
        int n = array.Length;

        while (n > 1)
        {
            var randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            int k = (int)(BitConverter.ToUInt32(randomBytes, 0) % n);
            n--;
            (array[k], array[n]) = (array[n], array[k]);
        }

        return new string(array);
    }

    protected string SanitizeIdentifier(string identifier)
    {
        // Remove invalid characters for database identifiers
        var sanitized = new StringBuilder();

        foreach (char c in identifier)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sanitized.Append(c);
            }
        }

        var result = sanitized.ToString();

        // Ensure it's not empty and doesn't start with a digit
        if (string.IsNullOrEmpty(result))
        {
            return "user";
        }

        if (char.IsDigit(result[0]))
        {
            return "u_" + result;
        }

        return result;
    }

    protected string ReplacePlaceholders(string statements, string username, string password)
    {
        return statements
            .Replace("{{username}}", username)
            .Replace("{{password}}", password)
            .Replace("{{name}}", username);
    }
}
