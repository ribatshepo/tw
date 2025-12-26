using System.ComponentModel.DataAnnotations;

namespace USP.API.DTOs.Authentication;

/// <summary>
/// Request model for user login.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Email or username for authentication.
    /// </summary>
    [Required(ErrorMessage = "Email or username is required")]
    public string EmailOrUsername { get; set; } = null!;

    /// <summary>
    /// User's password.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Optional flag to extend refresh token expiration.
    /// </summary>
    public bool RememberMe { get; set; } = false;
}
