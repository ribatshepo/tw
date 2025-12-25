using System.Security.Cryptography;
using System.Text;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// Base connector with common password generation logic
/// </summary>
public abstract class BaseConnector : ITargetSystemConnector
{
    public abstract string Platform { get; }

    public virtual string GeneratePassword(int length = 32)
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";

        var allChars = upperCase + lowerCase + digits + specialChars;

        var password = new StringBuilder(length);

        // Ensure at least one character from each category
        password.Append(upperCase[RandomNumberGenerator.GetInt32(upperCase.Length)]);
        password.Append(lowerCase[RandomNumberGenerator.GetInt32(lowerCase.Length)]);
        password.Append(digits[RandomNumberGenerator.GetInt32(digits.Length)]);
        password.Append(specialChars[RandomNumberGenerator.GetInt32(specialChars.Length)]);

        // Fill the rest randomly
        for (int i = 4; i < length; i++)
        {
            password.Append(allChars[RandomNumberGenerator.GetInt32(allChars.Length)]);
        }

        // Shuffle the password
        return Shuffle(password.ToString());
    }

    private static string Shuffle(string input)
    {
        var chars = input.ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    public abstract Task<PasswordRotationResult> RotatePasswordAsync(
        string hostAddress,
        int? port,
        string username,
        string currentPassword,
        string newPassword,
        string? databaseName = null,
        string? connectionDetails = null);

    public abstract Task<bool> VerifyCredentialsAsync(
        string hostAddress,
        int? port,
        string username,
        string password,
        string? databaseName = null,
        string? connectionDetails = null);
}
