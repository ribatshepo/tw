using System.Security.Cryptography;
using System.Text;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Implementation of backup codes service for MFA recovery
/// </summary>
public class BackupCodesService : IBackupCodesService
{
    private const int CodeLength = 8;
    private const string ValidCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude ambiguous characters (0, O, I, 1)

    public List<string> GenerateBackupCodes(int count = 10)
    {
        var codes = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            codes.Add(GenerateSingleCode());
        }

        return codes;
    }

    public string HashBackupCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Backup code cannot be null or empty", nameof(code));
        }

        // Normalize code (uppercase, remove spaces/dashes)
        var normalized = NormalizeCode(code);

        // Use SHA-256 for hashing backup codes
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyBackupCode(string code, string hash)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            var codeHash = HashBackupCode(code);
            return codeHash == hash;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateSingleCode()
    {
        var code = new char[CodeLength];
        var randomBytes = new byte[CodeLength];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        for (int i = 0; i < CodeLength; i++)
        {
            code[i] = ValidCharacters[randomBytes[i] % ValidCharacters.Length];
        }

        // Format as XXXX-XXXX for readability
        var codeString = new string(code);
        return $"{codeString.Substring(0, 4)}-{codeString.Substring(4, 4)}";
    }

    private string NormalizeCode(string code)
    {
        // Remove spaces, dashes, and convert to uppercase
        return code.Replace(" ", "").Replace("-", "").ToUpperInvariant();
    }
}
