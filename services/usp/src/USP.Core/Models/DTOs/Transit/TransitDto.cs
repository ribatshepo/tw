using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Transit;

/// <summary>
/// Request to create a new transit key
/// </summary>
public class CreateKeyRequest
{
    [Required]
    [RegularExpression("^(aes256-gcm96|chacha20-poly1305|rsa-2048|rsa-4096|ed25519|ecdsa-p256)$",
        ErrorMessage = "Type must be aes256-gcm96, chacha20-poly1305, rsa-2048, rsa-4096, ed25519, or ecdsa-p256")]
    public string Type { get; set; } = "aes256-gcm96";

    public bool DeletionAllowed { get; set; } = false;
    public bool Exportable { get; set; } = false;
    public bool AllowPlaintextBackup { get; set; } = false;
    public bool ConvergentEncryption { get; set; } = false;
    public bool Derived { get; set; } = false;
}

/// <summary>
/// Response after creating a transit key
/// </summary>
public class CreateKeyResponse
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int LatestVersion { get; set; }
}

/// <summary>
/// Response with key metadata
/// </summary>
public class ReadKeyResponse
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int LatestVersion { get; set; }
    public int MinDecryptionVersion { get; set; }
    public int MinEncryptionVersion { get; set; }
    public bool DeletionAllowed { get; set; }
    public bool Exportable { get; set; }
    public bool AllowPlaintextBackup { get; set; }
    public bool ConvergentEncryption { get; set; }
    public bool Derived { get; set; }
    public long EncryptionCount { get; set; }
    public long DecryptionCount { get; set; }
    public long SigningCount { get; set; }
    public long VerificationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<int, KeyVersionInfo> Versions { get; set; } = new();
}

/// <summary>
/// Key version information
/// </summary>
public class KeyVersionInfo
{
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DestroyedAt { get; set; }
    public string? PublicKey { get; set; }
}

/// <summary>
/// Response with list of keys
/// </summary>
public class ListKeysResponse
{
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// Request to update key configuration
/// </summary>
public class UpdateKeyConfigRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Min decryption version must be at least 1")]
    public int? MinDecryptionVersion { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Min encryption version must be at least 1")]
    public int? MinEncryptionVersion { get; set; }

    public bool? DeletionAllowed { get; set; }
}

/// <summary>
/// Response after updating key configuration
/// </summary>
public class UpdateKeyConfigResponse
{
    public string Name { get; set; } = string.Empty;
    public int MinDecryptionVersion { get; set; }
    public int MinEncryptionVersion { get; set; }
    public bool DeletionAllowed { get; set; }
}

/// <summary>
/// Response after rotating a key
/// </summary>
public class RotateKeyResponse
{
    public string Name { get; set; } = string.Empty;
    public int LatestVersion { get; set; }
}

/// <summary>
/// Request to encrypt data
/// </summary>
public class EncryptRequest
{
    [Required(ErrorMessage = "Plaintext is required")]
    public string Plaintext { get; set; } = string.Empty; // Base64-encoded

    public string? Context { get; set; } // Base64-encoded AEAD context

    [Range(1, int.MaxValue)]
    public int? KeyVersion { get; set; } // Optional specific version to use
}

/// <summary>
/// Response after encrypting data
/// </summary>
public class EncryptResponse
{
    public string Ciphertext { get; set; } = string.Empty; // Format: vault:v{version}:{base64_ciphertext}
    public int KeyVersion { get; set; }
}

/// <summary>
/// Request to decrypt data
/// </summary>
public class DecryptRequest
{
    [Required(ErrorMessage = "Ciphertext is required")]
    public string Ciphertext { get; set; } = string.Empty; // Format: vault:v{version}:{base64_ciphertext}

    public string? Context { get; set; } // Base64-encoded AEAD context
}

/// <summary>
/// Response after decrypting data
/// </summary>
public class DecryptResponse
{
    public string Plaintext { get; set; } = string.Empty; // Base64-encoded
    public int KeyVersion { get; set; }
}

/// <summary>
/// Request to rewrap data (re-encrypt with latest version)
/// </summary>
public class RewrapRequest
{
    [Required(ErrorMessage = "Ciphertext is required")]
    public string Ciphertext { get; set; } = string.Empty; // Format: vault:v{version}:{base64_ciphertext}

    public string? Context { get; set; } // Base64-encoded AEAD context
}

/// <summary>
/// Response after rewrapping data
/// </summary>
public class RewrapResponse
{
    public string Ciphertext { get; set; } = string.Empty; // Format: vault:v{version}:{base64_ciphertext}
    public int KeyVersion { get; set; }
}

/// <summary>
/// Batch encrypt request item
/// </summary>
public class BatchEncryptItem
{
    [Required]
    public string Plaintext { get; set; } = string.Empty;

    public string? Context { get; set; }
}

/// <summary>
/// Request to batch encrypt multiple items
/// </summary>
public class BatchEncryptRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required")]
    [MaxLength(1000, ErrorMessage = "Maximum 1000 items allowed")]
    public List<BatchEncryptItem> BatchInput { get; set; } = new();
}

/// <summary>
/// Batch encrypt response item
/// </summary>
public class BatchEncryptResponseItem
{
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Response after batch encrypting
/// </summary>
public class BatchEncryptResponse
{
    public List<BatchEncryptResponseItem> BatchResults { get; set; } = new();
}

/// <summary>
/// Batch decrypt request item
/// </summary>
public class BatchDecryptItem
{
    [Required]
    public string Ciphertext { get; set; } = string.Empty;

    public string? Context { get; set; }
}

/// <summary>
/// Request to batch decrypt multiple items
/// </summary>
public class BatchDecryptRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required")]
    [MaxLength(1000, ErrorMessage = "Maximum 1000 items allowed")]
    public List<BatchDecryptItem> BatchInput { get; set; } = new();
}

/// <summary>
/// Batch decrypt response item
/// </summary>
public class BatchDecryptResponseItem
{
    public string Plaintext { get; set; } = string.Empty;
    public int KeyVersion { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Response after batch decrypting
/// </summary>
public class BatchDecryptResponse
{
    public List<BatchDecryptResponseItem> BatchResults { get; set; } = new();
}

/// <summary>
/// Request to generate a data encryption key
/// </summary>
public class GenerateDataKeyRequest
{
    [Range(128, 4096, ErrorMessage = "Key size must be between 128 and 4096 bits")]
    public int Bits { get; set; } = 256; // Key size in bits (256, 512)

    public string? Context { get; set; } // Base64-encoded AEAD context
}

/// <summary>
/// Response after generating a data key
/// </summary>
public class GenerateDataKeyResponse
{
    public string Plaintext { get; set; } = string.Empty; // Base64-encoded plaintext key
    public string Ciphertext { get; set; } = string.Empty; // Encrypted key (vault format)
    public int KeyVersion { get; set; }
}

/// <summary>
/// Request to sign data
/// </summary>
public class SignRequest
{
    [Required(ErrorMessage = "Input is required")]
    public string Input { get; set; } = string.Empty; // Base64-encoded data to sign

    public string? Context { get; set; } // Base64-encoded AEAD context

    [Required]
    [RegularExpression("^(sha2-256|sha2-512)$", ErrorMessage = "Hash algorithm must be sha2-256 or sha2-512")]
    public string HashAlgorithm { get; set; } = "sha2-256"; // sha2-256, sha2-512
}

/// <summary>
/// Response after signing data
/// </summary>
public class SignResponse
{
    public string Signature { get; set; } = string.Empty; // Base64-encoded signature
    public int KeyVersion { get; set; }
}

/// <summary>
/// Request to verify a signature
/// </summary>
public class VerifyRequest
{
    [Required(ErrorMessage = "Input is required")]
    public string Input { get; set; } = string.Empty; // Base64-encoded data

    [Required(ErrorMessage = "Signature is required")]
    public string Signature { get; set; } = string.Empty; // Base64-encoded signature

    public string? Context { get; set; } // Base64-encoded AEAD context

    [Required]
    [RegularExpression("^(sha2-256|sha2-512)$", ErrorMessage = "Hash algorithm must be sha2-256 or sha2-512")]
    public string HashAlgorithm { get; set; } = "sha2-256";
}

/// <summary>
/// Response after verifying a signature
/// </summary>
public class VerifyResponse
{
    public bool Valid { get; set; }
    public int KeyVersion { get; set; }
}
