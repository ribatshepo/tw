namespace USP.Core.Services.Cryptography;

/// <summary>
/// Data masking and tokenization service for protecting sensitive data
/// </summary>
public interface IDataMaskingService
{
    /// <summary>
    /// Apply format-preserving encryption to data
    /// </summary>
    Task<string> FormatPreservingEncryptAsync(string plaintext, string format);

    /// <summary>
    /// Decrypt format-preserving encrypted data
    /// </summary>
    Task<string> FormatPreservingDecryptAsync(string ciphertext, string format);

    /// <summary>
    /// Apply static data masking (irreversible)
    /// </summary>
    string StaticMask(string data, MaskingStrategy strategy);

    /// <summary>
    /// Apply dynamic data masking based on user permissions
    /// </summary>
    Task<string> DynamicMaskAsync(string data, Guid userId, string dataClassification);

    /// <summary>
    /// Tokenize sensitive data (reversible)
    /// </summary>
    Task<string> TokenizeAsync(string sensitiveData, string tokenNamespace);

    /// <summary>
    /// Detokenize to retrieve original data
    /// </summary>
    Task<string> DetokenizeAsync(string token, string tokenNamespace);

    /// <summary>
    /// Tokenize irreversibly (one-way hash-based tokenization)
    /// </summary>
    string IrreversibleTokenize(string sensitiveData, string salt);

    /// <summary>
    /// Apply redaction rules to text
    /// </summary>
    string Redact(string text, RedactionRule[] rules);

    /// <summary>
    /// Pseudonymize data for analytics while preserving statistical properties
    /// </summary>
    Task<string> PseudonymizeAsync(string data, string purpose);

    /// <summary>
    /// Anonymize data (irreversible removal of PII)
    /// </summary>
    string Anonymize(string data, AnonymizationStrategy strategy);

    /// <summary>
    /// Mask credit card number (show last 4 digits)
    /// </summary>
    string MaskCreditCard(string cardNumber);

    /// <summary>
    /// Mask email address
    /// </summary>
    string MaskEmail(string email);

    /// <summary>
    /// Mask phone number
    /// </summary>
    string MaskPhoneNumber(string phoneNumber);

    /// <summary>
    /// Mask SSN/National ID
    /// </summary>
    string MaskSsn(string ssn);

    /// <summary>
    /// Check if token is valid and not expired
    /// </summary>
    Task<bool> ValidateTokenAsync(string token, string tokenNamespace);
}

/// <summary>
/// Masking strategy for static data masking
/// </summary>
public enum MaskingStrategy
{
    Full,              // Replace all characters with *
    Partial,           // Show first and last few characters
    Hash,              // Replace with one-way hash
    Shuffle,           // Randomize character order
    Nullify,           // Replace with null/empty
    Random,            // Replace with random data of same format
    Email,             // Email-specific masking
    PhoneNumber,       // Phone-specific masking
    CreditCard,        // Credit card-specific masking
    Ssn                // SSN-specific masking
}

/// <summary>
/// Anonymization strategy
/// </summary>
public enum AnonymizationStrategy
{
    Generalization,    // Replace specific values with general categories
    Suppression,       // Remove data entirely
    Perturbation,      // Add noise to numerical data
    Aggregation,       // Combine multiple records
    KAnonymity          // Ensure k records share same attributes
}

/// <summary>
/// Redaction rule for pattern-based text redaction
/// </summary>
public class RedactionRule
{
    public string Pattern { get; set; } = string.Empty;
    public string Replacement { get; set; } = "[REDACTED]";
    public bool IsRegex { get; set; }
}
