namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Request to enroll biometric authentication
/// </summary>
public class EnrollBiometricRequest
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Biometric type (Fingerprint, Face, Iris, Voice)
    /// </summary>
    public string BiometricType { get; set; } = string.Empty;

    /// <summary>
    /// Biometric template data (encrypted/hashed)
    /// </summary>
    public string TemplateData { get; set; } = string.Empty;

    /// <summary>
    /// Device ID performing enrollment
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for this biometric
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Public key for template encryption (optional)
    /// </summary>
    public string? PublicKey { get; set; }
}

/// <summary>
/// Response for biometric enrollment
/// </summary>
public class EnrollBiometricResponse
{
    /// <summary>
    /// Enrolled biometric ID
    /// </summary>
    public Guid BiometricId { get; set; }

    /// <summary>
    /// Biometric type
    /// </summary>
    public string BiometricType { get; set; } = string.Empty;

    /// <summary>
    /// Device name
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Enrollment timestamp
    /// </summary>
    public DateTime EnrolledAt { get; set; }

    /// <summary>
    /// Whether enrollment was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request for biometric authentication
/// </summary>
public class BiometricAuthRequest
{
    /// <summary>
    /// User identifier (email or username)
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Biometric type used
    /// </summary>
    public string BiometricType { get; set; } = string.Empty;

    /// <summary>
    /// Biometric template data for matching
    /// </summary>
    public string TemplateData { get; set; } = string.Empty;

    /// <summary>
    /// Device ID performing authentication
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Liveness detection result (optional)
    /// </summary>
    public bool? LivenessDetected { get; set; }

    /// <summary>
    /// Confidence score from biometric SDK (0-100)
    /// </summary>
    public int? ConfidenceScore { get; set; }

    /// <summary>
    /// Device fingerprint for risk assessment
    /// </summary>
    public string? DeviceFingerprint { get; set; }
}

/// <summary>
/// Response for biometric authentication
/// </summary>
public class BiometricAuthResponse
{
    /// <summary>
    /// Whether authentication was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Access token (if successful)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Refresh token (if successful)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether MFA is required
    /// </summary>
    public bool RequireMfa { get; set; }

    /// <summary>
    /// Match confidence score
    /// </summary>
    public int? MatchScore { get; set; }
}

/// <summary>
/// Request to verify biometric with PIN fallback
/// </summary>
public class BiometricPinAuthRequest
{
    /// <summary>
    /// User identifier
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Biometric data (optional if PIN is provided)
    /// </summary>
    public string? BiometricData { get; set; }

    /// <summary>
    /// PIN code (optional fallback)
    /// </summary>
    public string? PinCode { get; set; }

    /// <summary>
    /// Device ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>
/// DTO for enrolled biometric
/// </summary>
public class BiometricDeviceDto
{
    /// <summary>
    /// Biometric ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Biometric type
    /// </summary>
    public string BiometricType { get; set; } = string.Empty;

    /// <summary>
    /// Device name
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this biometric is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Last used timestamp
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Enrollment timestamp
    /// </summary>
    public DateTime EnrolledAt { get; set; }

    /// <summary>
    /// Total authentication count
    /// </summary>
    public int AuthenticationCount { get; set; }
}
