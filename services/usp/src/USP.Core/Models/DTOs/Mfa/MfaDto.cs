namespace USP.Core.Models.DTOs.Mfa;

/// <summary>
/// TOTP enrollment request
/// </summary>
public class EnrollTotpRequest
{
    public string DeviceName { get; set; } = string.Empty;
}

/// <summary>
/// TOTP enrollment response
/// </summary>
public class EnrollTotpResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeDataUrl { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
}

/// <summary>
/// TOTP verification request
/// </summary>
public class VerifyTotpRequest
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// TOTP verification response
/// </summary>
public class VerifyTotpResponse
{
    public bool IsValid { get; set; }
    public bool MfaEnabled { get; set; }
    public List<string> BackupCodes { get; set; } = new();
}

/// <summary>
/// MFA device info
/// </summary>
public class MfaDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// Disable MFA request
/// </summary>
public class DisableMfaRequest
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Generate backup codes response
/// </summary>
public class GenerateBackupCodesResponse
{
    public List<string> BackupCodes { get; set; } = new();
    public int TotalCodes { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Verify backup code request
/// </summary>
public class VerifyBackupCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// SMS/Email enrollment request
/// </summary>
public class EnrollOtpRequest
{
    public string DeviceType { get; set; } = string.Empty; // SMS or Email
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// OTP verification request
/// </summary>
public class VerifyOtpRequest
{
    public string Code { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
}
