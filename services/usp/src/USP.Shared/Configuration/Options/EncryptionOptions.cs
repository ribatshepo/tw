using System.ComponentModel.DataAnnotations;

namespace USP.Shared.Configuration.Options;

/// <summary>
/// Encryption service configuration for master key management
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// Master key source: Environment, File, HSM
    /// ENV: ENCRYPTION__KEY_SOURCE (default: Environment)
    /// </summary>
    [Required(ErrorMessage = "Key source is required")]
    public string KeySource { get; set; } = "Environment";

    /// <summary>
    /// Base64-encoded master encryption key (256-bit)
    /// ENV: ENCRYPTION__MASTER_KEY
    /// Required when KeySource = Environment
    /// </summary>
    public string? MasterKey { get; set; }

    /// <summary>
    /// Path to master key file
    /// ENV: ENCRYPTION__KEY_FILE_PATH
    /// Required when KeySource = File
    /// </summary>
    public string? KeyFilePath { get; set; }

    /// <summary>
    /// Whether to auto-generate and persist key file if missing (development only)
    /// ENV: ENCRYPTION__AUTO_GENERATE_KEY_FILE (default: false)
    /// </summary>
    public bool AutoGenerateKeyFile { get; set; } = false;

    /// <summary>
    /// Enable HSM integration for master key operations
    /// ENV: ENCRYPTION__HSM_ENABLED (default: false)
    /// </summary>
    public bool HsmEnabled { get; set; } = false;

    /// <summary>
    /// PKCS#11 library path for HSM access
    /// ENV: ENCRYPTION__HSM_PKCS11_LIBRARY
    /// Required when HsmEnabled = true
    /// </summary>
    public string? HsmPkcs11Library { get; set; }

    /// <summary>
    /// HSM slot ID
    /// ENV: ENCRYPTION__HSM_SLOT_ID (default: 0)
    /// </summary>
    public int HsmSlotId { get; set; } = 0;

    /// <summary>
    /// HSM key label/identifier
    /// ENV: ENCRYPTION__HSM_KEY_LABEL
    /// </summary>
    public string? HsmKeyLabel { get; set; }

    /// <summary>
    /// Enable key rotation support
    /// ENV: ENCRYPTION__ENABLE_KEY_ROTATION (default: false)
    /// </summary>
    public bool EnableKeyRotation { get; set; } = false;

    /// <summary>
    /// Validate configuration based on selected key source
    /// </summary>
    public void Validate()
    {
        switch (KeySource.ToLowerInvariant())
        {
            case "environment":
                if (string.IsNullOrWhiteSpace(MasterKey))
                {
                    throw new InvalidOperationException(
                        "ENCRYPTION__MASTER_KEY environment variable is required when KeySource=Environment");
                }

                // Validate base64 and length
                try
                {
                    var keyBytes = Convert.FromBase64String(MasterKey);
                    if (keyBytes.Length != 32)
                    {
                        throw new InvalidOperationException(
                            $"Master key must be exactly 32 bytes (256 bits). Current length: {keyBytes.Length} bytes");
                    }
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException("Master key must be valid base64-encoded string");
                }
                break;

            case "file":
                if (string.IsNullOrWhiteSpace(KeyFilePath))
                {
                    throw new InvalidOperationException(
                        "ENCRYPTION__KEY_FILE_PATH is required when KeySource=File");
                }
                break;

            case "hsm":
                if (!HsmEnabled)
                {
                    throw new InvalidOperationException(
                        "HsmEnabled must be true when KeySource=HSM");
                }
                if (string.IsNullOrWhiteSpace(HsmPkcs11Library))
                {
                    throw new InvalidOperationException(
                        "ENCRYPTION__HSM_PKCS11_LIBRARY is required when KeySource=HSM");
                }
                if (string.IsNullOrWhiteSpace(HsmKeyLabel))
                {
                    throw new InvalidOperationException(
                        "ENCRYPTION__HSM_KEY_LABEL is required when KeySource=HSM");
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Invalid KeySource: {KeySource}. Must be Environment, File, or HSM");
        }
    }
}
