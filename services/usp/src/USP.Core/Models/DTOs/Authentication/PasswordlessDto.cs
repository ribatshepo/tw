namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Request to send passwordless authentication
/// </summary>
public class PasswordlessAuthenticationRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URL after successful authentication
    /// </summary>
    public string? RedirectUrl { get; set; }
}

/// <summary>
/// Passwordless authentication response
/// </summary>
public class PasswordlessAuthenticationResponse
{
    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify magic link token
/// </summary>
public class VerifyMagicLinkRequest
{
    /// <summary>
    /// Magic link token
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Request to send QR code for passwordless authentication
/// </summary>
public class QrCodeAuthRequest
{
    /// <summary>
    /// User's email or username
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Session ID to link QR code to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Response containing QR code for passwordless authentication
/// </summary>
public class QrCodeAuthResponse
{
    /// <summary>
    /// QR code as base64 data URL
    /// </summary>
    public string QrCodeDataUrl { get; set; } = string.Empty;

    /// <summary>
    /// QR code token for verification
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Expiration time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Session ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify QR code scan
/// </summary>
public class VerifyQrCodeRequest
{
    /// <summary>
    /// QR code token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Session ID from the browser
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to send SMS authentication link
/// </summary>
public class SmsLinkAuthRequest
{
    /// <summary>
    /// Phone number to send SMS to
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URL after successful authentication
    /// </summary>
    public string? RedirectUrl { get; set; }
}

/// <summary>
/// Response for SMS link authentication
/// </summary>
public class SmsLinkAuthResponse
{
    /// <summary>
    /// Whether the SMS was sent successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
