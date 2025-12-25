using System.Text.Json;

namespace USP.Core.Models.Entities;

/// <summary>
/// Secret entity for Vault KV v2 storage with versioning and encryption
/// </summary>
public class Secret
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public byte[] EncryptedValue { get; set; } = Array.Empty<byte>();
    public string EncryptedData { get; set; } = string.Empty; // Base64 encoded encrypted data for KV v2
    public int EncryptionKeyVersion { get; set; }
    public JsonDocument? Metadata { get; set; }
    public int Version { get; set; } = 1;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDestroyed { get; set; }

    // Navigation properties
    public virtual ApplicationUser? Creator { get; set; }
    public virtual ICollection<SecretAccessLog> AccessLogs { get; set; } = new List<SecretAccessLog>();
}
