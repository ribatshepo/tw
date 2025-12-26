namespace USP.Core.Models.DTOs.SSH;

// ============================================
// SSH Key Pair Generation
// ============================================

public class GenerateSSHKeyPairRequest
{
    public string KeyType { get; set; } = "rsa"; // "rsa", "ed25519"
    public int KeyBits { get; set; } = 4096; // For RSA: 2048, 3072, 4096
    public string? Comment { get; set; }
}

public class GenerateSSHKeyPairResponse
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
}

// ============================================
// SSH Certificate Signing
// ============================================

public class SignSSHCertificateRequest
{
    public string PublicKey { get; set; } = string.Empty;
    public int TtlSeconds { get; set; } = 3600; // Certificate validity period
    public List<string> ValidPrincipals { get; set; } = new(); // Usernames/hostnames
    public string CertificateType { get; set; } = "user"; // "user" or "host"
    public Dictionary<string, string>? CriticalOptions { get; set; }
    public Dictionary<string, string>? Extensions { get; set; }
}

public class SignSSHCertificateResponse
{
    public string SignedCertificate { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime ValidAfter { get; set; }
    public DateTime ValidBefore { get; set; }
    public List<string> ValidPrincipals { get; set; } = new();
}

// ============================================
// SSH Role Management
// ============================================

public class CreateSSHRoleRequest
{
    public string KeyType { get; set; } = "rsa";
    public int DefaultTtlSeconds { get; set; } = 3600;
    public int MaxTtlSeconds { get; set; } = 86400;
    public List<string> AllowedPrincipals { get; set; } = new();
    public string CertificateType { get; set; } = "user";
    public Dictionary<string, string>? DefaultCriticalOptions { get; set; }
    public Dictionary<string, string>? DefaultExtensions { get; set; }
    public bool AllowUserCertificates { get; set; } = true;
    public bool AllowHostCertificates { get; set; } = false;
}

public class CreateSSHRoleResponse
{
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReadSSHRoleResponse
{
    public string RoleName { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public int DefaultTtlSeconds { get; set; }
    public int MaxTtlSeconds { get; set; }
    public List<string> AllowedPrincipals { get; set; } = new();
    public string CertificateType { get; set; } = string.Empty;
    public Dictionary<string, string>? DefaultCriticalOptions { get; set; }
    public Dictionary<string, string>? DefaultExtensions { get; set; }
    public bool AllowUserCertificates { get; set; }
    public bool AllowHostCertificates { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ListSSHRolesResponse
{
    public List<string> Roles { get; set; } = new();
}

// ============================================
// SSH CA Management
// ============================================

public class GenerateSSHCARequest
{
    public string KeyType { get; set; } = "rsa";
    public int KeyBits { get; set; } = 4096;
}

public class GenerateSSHCAResponse
{
    public string PublicKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public class ReadSSHCAResponse
{
    public string PublicKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
}

// ============================================
// SSH OTP (One-Time Password)
// ============================================

public class IssueSSHOTPRequest
{
    public string Username { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int TtlSeconds { get; set; } = 300; // 5 minutes default
}

public class IssueSSHOTPResponse
{
    public string Otp { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class VerifySSHOTPRequest
{
    public string Otp { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}

public class VerifySSHOTPResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

// ============================================
// Host Key Management
// ============================================

public class RegisterHostKeyRequest
{
    public string Hostname { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
}

public class HostKeyInfo
{
    public string Hostname { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
