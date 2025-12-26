using System.ComponentModel.DataAnnotations;

namespace USP.API.DTOs.Authentication;

/// <summary>
/// Request model for refreshing access token.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token to use for generating a new access token.
    /// </summary>
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = null!;
}
