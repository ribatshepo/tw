namespace USP.API.DTOs.Authentication;

/// <summary>
/// Response model for successful authentication.
/// </summary>
public class AuthenticationResponse
{
    /// <summary>
    /// JWT access token (short-lived, used for API authentication).
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Refresh token (long-lived, used to obtain new access tokens).
    /// </summary>
    public required string RefreshToken { get; set; }

    /// <summary>
    /// Access token expiration time in seconds.
    /// </summary>
    public required int ExpiresIn { get; set; }

    /// <summary>
    /// Token type (always "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Authenticated user's profile information.
    /// </summary>
    public required UserProfileResponse User { get; set; }
}
