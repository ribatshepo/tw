namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Request to create a privileged safe
/// </summary>
public class CreateSafeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SafeType { get; set; } = "Generic"; // Database, SSH, Cloud, Windows, Linux, Generic
    public List<SafeAccessControl> AccessControl { get; set; } = new();
    public bool RequireApproval { get; set; } = false;
    public bool RequireDualControl { get; set; } = false;
    public int MaxCheckoutDurationMinutes { get; set; } = 240;
    public bool RotateOnCheckin { get; set; } = false;
    public bool SessionRecordingEnabled { get; set; } = false;
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to update a safe
/// </summary>
public class UpdateSafeRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<SafeAccessControl>? AccessControl { get; set; }
    public bool? RequireApproval { get; set; }
    public bool? RequireDualControl { get; set; }
    public int? MaxCheckoutDurationMinutes { get; set; }
    public bool? RotateOnCheckin { get; set; }
    public bool? SessionRecordingEnabled { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Safe access control entry
/// </summary>
public class SafeAccessControl
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "read"; // read, checkout, manage
}

/// <summary>
/// Privileged safe DTO
/// </summary>
public class PrivilegedSafeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string SafeType { get; set; } = string.Empty;
    public List<SafeAccessControl> AccessControl { get; set; } = new();
    public bool RequireApproval { get; set; }
    public bool RequireDualControl { get; set; }
    public int MaxCheckoutDurationMinutes { get; set; }
    public bool RotateOnCheckin { get; set; }
    public bool SessionRecordingEnabled { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public int AccountCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Request to add a privileged account to a safe
/// </summary>
public class CreatePrivilegedAccountRequest
{
    public string AccountName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // Will be encrypted
    public string Platform { get; set; } = string.Empty; // PostgreSQL, MySQL, Windows, Linux, AWS, Azure, SSH
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }
    public Dictionary<string, string>? ConnectionDetails { get; set; }
    public string RotationPolicy { get; set; } = "manual";
    public int RotationIntervalDays { get; set; } = 90;
    public int PasswordComplexity { get; set; } = 16;
    public bool RequireMfa { get; set; } = false;
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to update a privileged account
/// </summary>
public class UpdatePrivilegedAccountRequest
{
    public string? AccountName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }
    public Dictionary<string, string>? ConnectionDetails { get; set; }
    public string? RotationPolicy { get; set; }
    public int? RotationIntervalDays { get; set; }
    public int? PasswordComplexity { get; set; }
    public bool? RequireMfa { get; set; }
    public string? Status { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Privileged account DTO
/// </summary>
public class PrivilegedAccountDto
{
    public Guid Id { get; set; }
    public Guid SafeId { get; set; }
    public string SafeName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }
    public Dictionary<string, string>? ConnectionDetails { get; set; }
    public string RotationPolicy { get; set; } = string.Empty;
    public int RotationIntervalDays { get; set; }
    public DateTime? LastRotated { get; set; }
    public DateTime? NextRotation { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PasswordComplexity { get; set; }
    public bool RequireMfa { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public bool IsCheckedOut { get; set; }
    public Guid? CurrentCheckoutId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Response when revealing a privileged account password
/// </summary>
public class RevealPasswordResponse
{
    public Guid AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // Decrypted password
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? ConnectionString { get; set; } // Auto-generated connection string
    public string Warning { get; set; } = "This password is sensitive and will only be shown once. Store it securely.";
    public DateTime RevealedAt { get; set; }
    public int ValidForMinutes { get; set; }
}

/// <summary>
/// Safe statistics
/// </summary>
public class SafeStatisticsDto
{
    public int TotalSafes { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveCheckouts { get; set; }
    public int PendingApprovals { get; set; }
    public int RotationsDueThisWeek { get; set; }
    public Dictionary<string, int> AccountsByPlatform { get; set; } = new();
    public Dictionary<string, int> SafesByType { get; set; } = new();
}
