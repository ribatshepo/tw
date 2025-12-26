namespace USP.API.DTOs.Authentication;

/// <summary>
/// Response model for user profile information.
/// </summary>
public class UserProfileResponse
{
    /// <summary>
    /// User's unique identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// User's email address.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// User's username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Whether MFA is enabled for this user.
    /// </summary>
    public bool MfaEnabled { get; set; }

    /// <summary>
    /// User's assigned roles.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// User's current status.
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// User's risk score (0-100).
    /// </summary>
    public decimal? RiskScore { get; set; }

    /// <summary>
    /// When the user last logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// When the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
