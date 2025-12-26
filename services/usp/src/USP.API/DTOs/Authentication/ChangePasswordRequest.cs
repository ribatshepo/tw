using System.ComponentModel.DataAnnotations;

namespace USP.API.DTOs.Authentication;

/// <summary>
/// Request model for changing user password.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// Current password (for verification).
    /// </summary>
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = null!;

    /// <summary>
    /// New password (min 12 chars, must include uppercase, lowercase, digit, special char).
    /// </summary>
    [Required(ErrorMessage = "New password is required")]
    [MinLength(12, ErrorMessage = "New password must be at least 12 characters")]
    public string NewPassword { get; set; } = null!;

    /// <summary>
    /// New password confirmation (must match NewPassword).
    /// </summary>
    [Required(ErrorMessage = "New password confirmation is required")]
    [Compare(nameof(NewPassword), ErrorMessage = "New passwords do not match")]
    public string ConfirmNewPassword { get; set; } = null!;
}
