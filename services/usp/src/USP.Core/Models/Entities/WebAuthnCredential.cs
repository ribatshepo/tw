namespace USP.Core.Models.Entities;

/// <summary>
/// WebAuthn/FIDO2 credential for passwordless authentication
/// </summary>
public class WebAuthnCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public uint SignatureCounter { get; set; }
    public string AaGuid { get; set; } = string.Empty;
    public string CredentialType { get; set; } = "public-key";
    public List<string> Transports { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}

/// <summary>
/// OAuth 2.0 client application
/// </summary>
public class OAuth2Client
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty; // Hashed
    public string ClientName { get; set; } = string.Empty;
    public string ClientType { get; set; } = "confidential"; // confidential, public
    public List<string> RedirectUris { get; set; } = new();
    public List<string> AllowedScopes { get; set; } = new();
    public List<string> AllowedGrantTypes { get; set; } = new();
    public bool RequirePkce { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<OAuth2AuthorizationCode> AuthorizationCodes { get; set; } = new List<OAuth2AuthorizationCode>();
}

/// <summary>
/// OAuth 2.0 authorization code
/// </summary>
public class OAuth2AuthorizationCode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Navigation properties
    public OAuth2Client Client { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

/// <summary>
/// Magic link for passwordless authentication
/// </summary>
public class MagicLink
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}

/// <summary>
/// SAML 2.0 identity provider configuration
/// </summary>
public class SamlIdentityProvider
{
    public Guid Id { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SingleSignOnUrl { get; set; } = string.Empty;
    public string SingleLogoutUrl { get; set; } = string.Empty;
    public string Certificate { get; set; } = string.Empty; // X.509 certificate
    public string? MetadataUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// LDAP server configuration
/// </summary>
public class LdapConfiguration
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; } = true;
    public string BaseDn { get; set; } = string.Empty;
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty; // Encrypted
    public string UserSearchFilter { get; set; } = "(sAMAccountName={0})";
    public string? GroupSearchFilter { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Risk assessment log
/// </summary>
public class RiskAssessment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public string RiskLevel { get; set; } = string.Empty; // low, medium, high, critical
    public int RiskScore { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public string Action { get; set; } = string.Empty; // allowed, blocked, mfa_required
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}
