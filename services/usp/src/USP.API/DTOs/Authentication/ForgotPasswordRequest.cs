using System.ComponentModel.DataAnnotations;

namespace USP.API.DTOs.Authentication;

/// <summary>
/// Request model for initiating password reset.
/// </summary>
public class ForgotPasswordRequest
{
    /// <summary>
    /// User's email address to send password reset link.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;
}
