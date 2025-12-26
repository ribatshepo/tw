using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Cryptography;

/// <summary>
/// Production-ready data masking and tokenization service
/// </summary>
public class DataMaskingService : IDataMaskingService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<DataMaskingService> _logger;
    private const string TokenPrefix = "tok_";
    private const int TokenLength = 32;

    public DataMaskingService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<DataMaskingService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<string> FormatPreservingEncryptAsync(string plaintext, string format)
    {
        try
        {
            if (string.IsNullOrEmpty(plaintext))
                throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

            var encrypted = _encryptionService.Encrypt(plaintext);

            return format.ToLower() switch
            {
                "numeric" => ConvertToNumericFormat(encrypted, plaintext.Length),
                "alphanumeric" => ConvertToAlphanumericFormat(encrypted, plaintext.Length),
                "alphabetic" => ConvertToAlphabeticFormat(encrypted, plaintext.Length),
                "email" => await FormatPreservingEncryptEmailAsync(plaintext),
                "phone" => FormatPreservingEncryptPhone(plaintext, encrypted),
                _ => encrypted
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in format-preserving encryption");
            throw;
        }
    }

    public async Task<string> FormatPreservingDecryptAsync(string ciphertext, string format)
    {
        try
        {
            if (string.IsNullOrEmpty(ciphertext))
                throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));

            if (format.ToLower() == "email" || format.ToLower() == "phone")
            {
                var token = await _context.Set<DataToken>()
                    .FirstOrDefaultAsync(t => t.TokenValue == ciphertext && !t.IsExpired);

                if (token != null)
                {
                    return _encryptionService.Decrypt(token.OriginalValue);
                }
            }

            return _encryptionService.Decrypt(ciphertext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in format-preserving decryption");
            throw;
        }
    }

    public string StaticMask(string data, MaskingStrategy strategy)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        return strategy switch
        {
            MaskingStrategy.Full => new string('*', data.Length),
            MaskingStrategy.Partial => MaskPartial(data),
            MaskingStrategy.Hash => ComputeHash(data),
            MaskingStrategy.Shuffle => ShuffleString(data),
            MaskingStrategy.Nullify => string.Empty,
            MaskingStrategy.Random => GenerateRandomData(data.Length),
            MaskingStrategy.Email => MaskEmail(data),
            MaskingStrategy.PhoneNumber => MaskPhoneNumber(data),
            MaskingStrategy.CreditCard => MaskCreditCard(data),
            MaskingStrategy.Ssn => MaskSsn(data),
            _ => new string('*', data.Length)
        };
    }

    public async Task<string> DynamicMaskAsync(string data, Guid userId, string dataClassification)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for dynamic masking", userId);
                return StaticMask(data, MaskingStrategy.Full);
            }

            var hasPermission = dataClassification.ToLower() switch
            {
                "public" => true,
                "internal" => true,
                "confidential" => await HasConfidentialAccessAsync(userId),
                "restricted" => await HasRestrictedAccessAsync(userId),
                "secret" => await HasSecretAccessAsync(userId),
                _ => false
            };

            if (hasPermission)
            {
                return data;
            }

            var maskingStrategy = dataClassification.ToLower() switch
            {
                "confidential" => MaskingStrategy.Partial,
                "restricted" => MaskingStrategy.Hash,
                "secret" => MaskingStrategy.Full,
                _ => MaskingStrategy.Partial
            };

            return StaticMask(data, maskingStrategy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in dynamic masking for user {UserId}", userId);
            return StaticMask(data, MaskingStrategy.Full);
        }
    }

    public async Task<string> TokenizeAsync(string sensitiveData, string tokenNamespace)
    {
        try
        {
            if (string.IsNullOrEmpty(sensitiveData))
                throw new ArgumentException("Sensitive data cannot be null or empty", nameof(sensitiveData));

            var existingToken = await _context.Set<DataToken>()
                .FirstOrDefaultAsync(t => t.Namespace == tokenNamespace &&
                                          t.OriginalValueHash == ComputeHash(sensitiveData) &&
                                          !t.IsExpired);

            if (existingToken != null)
            {
                _logger.LogDebug("Reusing existing token for namespace {Namespace}", tokenNamespace);
                return existingToken.TokenValue;
            }

            var token = GenerateSecureToken();
            var encryptedValue = _encryptionService.Encrypt(sensitiveData);

            var dataToken = new DataToken
            {
                Id = Guid.NewGuid(),
                TokenValue = token,
                OriginalValue = encryptedValue,
                OriginalValueHash = ComputeHash(sensitiveData),
                Namespace = tokenNamespace,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                IsReversible = true
            };

            _context.Set<DataToken>().Add(dataToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created reversible token in namespace {Namespace}", tokenNamespace);
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tokenizing data");
            throw;
        }
    }

    public async Task<string> DetokenizeAsync(string token, string tokenNamespace)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("Token cannot be null or empty", nameof(token));

            var dataToken = await _context.Set<DataToken>()
                .FirstOrDefaultAsync(t => t.TokenValue == token &&
                                          t.Namespace == tokenNamespace &&
                                          !t.IsExpired);

            if (dataToken == null)
            {
                _logger.LogWarning("Token not found or expired in namespace {Namespace}", tokenNamespace);
                throw new InvalidOperationException("Invalid or expired token");
            }

            if (!dataToken.IsReversible)
            {
                _logger.LogWarning("Attempted to detokenize irreversible token");
                throw new InvalidOperationException("Token is not reversible");
            }

            dataToken.LastAccessedAt = DateTime.UtcNow;
            dataToken.AccessCount++;
            await _context.SaveChangesAsync();

            return _encryptionService.Decrypt(dataToken.OriginalValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detokenizing data");
            throw;
        }
    }

    public string IrreversibleTokenize(string sensitiveData, string salt)
    {
        if (string.IsNullOrEmpty(sensitiveData))
            throw new ArgumentException("Sensitive data cannot be null or empty", nameof(sensitiveData));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(sensitiveData));
        return $"{TokenPrefix}{Convert.ToBase64String(hashBytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, TokenLength)}";
    }

    public string Redact(string text, RedactionRule[] rules)
    {
        if (string.IsNullOrEmpty(text) || rules == null || rules.Length == 0)
            return text;

        var redacted = text;

        foreach (var rule in rules)
        {
            if (rule.IsRegex)
            {
                try
                {
                    redacted = Regex.Replace(redacted, rule.Pattern, rule.Replacement);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid regex pattern: {Pattern}", rule.Pattern);
                }
            }
            else
            {
                redacted = redacted.Replace(rule.Pattern, rule.Replacement);
            }
        }

        return redacted;
    }

    public async Task<string> PseudonymizeAsync(string data, string purpose)
    {
        try
        {
            var pseudonym = GenerateSecureToken();
            var mapping = new PseudonymMapping
            {
                Id = Guid.NewGuid(),
                Pseudonym = pseudonym,
                OriginalDataHash = ComputeHash(data),
                Purpose = purpose,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<PseudonymMapping>().Add(mapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created pseudonym for purpose: {Purpose}", purpose);
            return pseudonym;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pseudonymizing data");
            throw;
        }
    }

    public string Anonymize(string data, AnonymizationStrategy strategy)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        return strategy switch
        {
            AnonymizationStrategy.Generalization => Generalize(data),
            AnonymizationStrategy.Suppression => string.Empty,
            AnonymizationStrategy.Perturbation => Perturb(data),
            AnonymizationStrategy.Aggregation => "[AGGREGATED]",
            AnonymizationStrategy.KAnonymity => $"[K-ANONYMOUS_{ComputeHash(data).Substring(0, 8)}]",
            _ => "[ANONYMIZED]"
        };
    }

    public string MaskCreditCard(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber))
            return cardNumber;

        var digits = Regex.Replace(cardNumber, @"\D", "");
        if (digits.Length < 4)
            return new string('*', cardNumber.Length);

        var last4 = digits.Substring(digits.Length - 4);
        var masked = new string('*', digits.Length - 4) + last4;

        if (cardNumber.Contains("-"))
        {
            return Regex.Replace(cardNumber, @"\d", m =>
            {
                var index = cardNumber.IndexOf(m.Value);
                var digitIndex = cardNumber.Substring(0, index + 1).Count(char.IsDigit) - 1;
                return digitIndex < masked.Length ? masked[digitIndex].ToString() : m.Value;
            });
        }

        return masked;
    }

    public string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return new string('*', email.Length);

        var parts = email.Split('@');
        var localPart = parts[0];
        var domain = parts[1];

        var maskedLocal = localPart.Length <= 2
            ? new string('*', localPart.Length)
            : localPart[0] + new string('*', localPart.Length - 2) + localPart[^1];

        var domainParts = domain.Split('.');
        var maskedDomain = domainParts.Length > 1
            ? domainParts[0][0] + new string('*', domainParts[0].Length - 1) + "." + domainParts[^1]
            : new string('*', domain.Length);

        return $"{maskedLocal}@{maskedDomain}";
    }

    public string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
            return phoneNumber;

        var digits = Regex.Replace(phoneNumber, @"\D", "");
        if (digits.Length < 4)
            return new string('*', phoneNumber.Length);

        var last4 = digits.Substring(digits.Length - 4);
        var masked = new string('*', digits.Length - 4) + last4;

        var resultIndex = 0;
        var result = new StringBuilder();
        foreach (var c in phoneNumber)
        {
            if (char.IsDigit(c))
            {
                result.Append(masked[resultIndex++]);
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    public string MaskSsn(string ssn)
    {
        if (string.IsNullOrEmpty(ssn))
            return ssn;

        var digits = Regex.Replace(ssn, @"\D", "");
        if (digits.Length < 4)
            return new string('*', ssn.Length);

        var last4 = digits.Substring(digits.Length - 4);
        return $"***-**-{last4}";
    }

    public async Task<bool> ValidateTokenAsync(string token, string tokenNamespace)
    {
        try
        {
            var dataToken = await _context.Set<DataToken>()
                .FirstOrDefaultAsync(t => t.TokenValue == token && t.Namespace == tokenNamespace);

            return dataToken != null && !dataToken.IsExpired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return false;
        }
    }

    #region Private Helper Methods

    private string MaskPartial(string data)
    {
        if (data.Length <= 4)
            return new string('*', data.Length);

        var showChars = Math.Min(2, data.Length / 4);
        var start = data.Substring(0, showChars);
        var end = data.Substring(data.Length - showChars);
        var masked = new string('*', data.Length - (showChars * 2));

        return $"{start}{masked}{end}";
    }

    private string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes).Substring(0, Math.Min(16, Convert.ToBase64String(hashBytes).Length));
    }

    private string ShuffleString(string input)
    {
        var chars = input.ToCharArray();
        var random = new Random(input.GetHashCode());

        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private string GenerateRandomData(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        using var rng = RandomNumberGenerator.Create();
        var data = new byte[length];
        rng.GetBytes(data);

        return new string(data.Select(b => chars[b % chars.Length]).ToArray());
    }

    private string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var tokenBytes = new byte[TokenLength];
        rng.GetBytes(tokenBytes);
        return $"{TokenPrefix}{Convert.ToBase64String(tokenBytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, TokenLength)}";
    }

    private async Task<bool> HasConfidentialAccessAsync(Guid userId)
    {
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        return userRoles.Any(ur =>
            ur.Role.RolePermissions.Any(rp =>
                rp.Permission.Name.Contains("confidential", StringComparison.OrdinalIgnoreCase) ||
                rp.Permission.Name.Contains("admin", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<bool> HasRestrictedAccessAsync(Guid userId)
    {
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        return userRoles.Any(ur =>
            ur.Role.RolePermissions.Any(rp =>
                rp.Permission.Name.Contains("restricted", StringComparison.OrdinalIgnoreCase) ||
                rp.Permission.Name.Contains("security", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<bool> HasSecretAccessAsync(Guid userId)
    {
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        return userRoles.Any(ur =>
            ur.Role.RolePermissions.Any(rp =>
                rp.Permission.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                rp.Permission.Name == "admin:all"));
    }

    private string ConvertToNumericFormat(string encrypted, int targetLength)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(encrypted));
        var numericString = new StringBuilder();

        foreach (var b in hashBytes)
        {
            numericString.Append((b % 10).ToString());
            if (numericString.Length >= targetLength)
                break;
        }

        return numericString.ToString().Substring(0, Math.Min(targetLength, numericString.Length));
    }

    private string ConvertToAlphanumericFormat(string encrypted, int targetLength)
    {
        const string alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(encrypted));
        var result = new StringBuilder();

        foreach (var b in hashBytes)
        {
            result.Append(alphanumeric[b % alphanumeric.Length]);
            if (result.Length >= targetLength)
                break;
        }

        return result.ToString().Substring(0, Math.Min(targetLength, result.Length));
    }

    private string ConvertToAlphabeticFormat(string encrypted, int targetLength)
    {
        const string alphabetic = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(encrypted));
        var result = new StringBuilder();

        foreach (var b in hashBytes)
        {
            result.Append(alphabetic[b % alphabetic.Length]);
            if (result.Length >= targetLength)
                break;
        }

        return result.ToString().Substring(0, Math.Min(targetLength, result.Length));
    }

    private async Task<string> FormatPreservingEncryptEmailAsync(string email)
    {
        var token = await TokenizeAsync(email, "email");
        return token;
    }

    private string FormatPreservingEncryptPhone(string phone, string encrypted)
    {
        var digits = Regex.Replace(phone, @"\D", "");
        var encryptedDigits = ConvertToNumericFormat(encrypted, digits.Length);

        var resultIndex = 0;
        var result = new StringBuilder();
        foreach (var c in phone)
        {
            if (char.IsDigit(c) && resultIndex < encryptedDigits.Length)
            {
                result.Append(encryptedDigits[resultIndex++]);
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private string Generalize(string data)
    {
        if (int.TryParse(data, out int numValue))
        {
            var rounded = (int)(Math.Round(numValue / 10.0) * 10);
            return $"{rounded}-{rounded + 9}";
        }

        if (DateTime.TryParse(data, out DateTime dateValue))
        {
            return dateValue.ToString("yyyy-MM");
        }

        return data.Length > 10 ? data.Substring(0, 10) + "..." : data;
    }

    private string Perturb(string data)
    {
        if (int.TryParse(data, out int numValue))
        {
            var random = new Random(data.GetHashCode());
            var noise = random.Next(-10, 11);
            return (numValue + noise).ToString();
        }

        if (double.TryParse(data, out double doubleValue))
        {
            var random = new Random(data.GetHashCode());
            var noise = (random.NextDouble() - 0.5) * 10;
            return (doubleValue + noise).ToString("F2");
        }

        return data;
    }

    #endregion
}

/// <summary>
/// Entity for storing tokenized data
/// </summary>
public class DataToken
{
    public Guid Id { get; set; }
    public string TokenValue { get; set; } = string.Empty;
    public string OriginalValue { get; set; } = string.Empty;
    public string OriginalValueHash { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
    public bool IsReversible { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// Entity for pseudonym mappings
/// </summary>
public class PseudonymMapping
{
    public Guid Id { get; set; }
    public string Pseudonym { get; set; } = string.Empty;
    public string OriginalDataHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
