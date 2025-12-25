namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Request model for user login
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? MfaCode { get; set; }
}
