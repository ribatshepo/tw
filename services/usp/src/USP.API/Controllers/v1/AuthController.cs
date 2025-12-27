using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.API.Controllers.v1;

/// <summary>
/// Authentication and user management endpoints
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = await _authService.RegisterAsync(
                request.Email,
                request.Username,
                request.Password);

            _logger.LogInformation("User registered successfully: {UserId}", user.Id);

            return CreatedAtAction(
                nameof(GetCurrentUser),
                new { },
                new RegisterResponse
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Username = user.UserName!,
                    Message = "Registration successful. Please check your email to verify your account."
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Authenticate user and create session
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _authService.LoginAsync(
                request.EmailOrUsername,
                request.Password,
                ipAddress,
                userAgent,
                request.DeviceFingerprint);

            _logger.LogInformation("User logged in successfully: {UserId}", result.User.Id);

            return Ok(new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                TokenType = result.TokenType,
                User = new UserInfo
                {
                    Id = result.User.Id,
                    Email = result.User.Email!,
                    Username = result.User.UserName!,
                    MfaEnabled = result.User.MfaEnabled
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed: {Error}", ex.Message);
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);

            _logger.LogInformation("Token refreshed for user: {UserId}", result.User.Id);

            return Ok(new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                TokenType = result.TokenType,
                User = new UserInfo
                {
                    Id = result.User.Id,
                    Email = result.User.Email!,
                    Username = result.User.UserName!,
                    MfaEnabled = result.User.MfaEnabled
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Token refresh failed: {Error}", ex.Message);
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Logout from current session
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _authService.LogoutAsync(request.SessionId);
        _logger.LogInformation("User logged out: Session {SessionId}", request.SessionId);
        return NoContent();
    }

    /// <summary>
    /// Logout from all sessions
    /// </summary>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _authService.LogoutAllAsync(userId);
        _logger.LogInformation("User logged out from all sessions: {UserId}", userId);
        return NoContent();
    }

    /// <summary>
    /// Get current authenticated user profile
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetCurrentUserAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            Email = user.Email!,
            Username = user.UserName!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            MfaEnabled = user.MfaEnabled,
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            LastLoginIp = user.LastLoginIp
        });
    }

    /// <summary>
    /// Change user password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _authService.ChangePasswordAsync(
                userId,
                request.CurrentPassword,
                request.NewPassword);

            _logger.LogInformation("Password changed for user: {UserId}", userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Password change failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request password reset email
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request.Email);
        // Always return success to prevent email enumeration
        return NoContent();
    }

    /// <summary>
    /// Reset password using reset token
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
            _logger.LogInformation("Password reset successful for email: {Email}", request.Email);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Password reset failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Request/Response DTOs
public record RegisterRequest(string Email, string Username, string Password);
public record RegisterResponse
{
    public required string UserId { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string Message { get; set; }
}

public record LoginRequest(string EmailOrUsername, string Password, string? DeviceFingerprint);
public record LoginResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required int ExpiresIn { get; set; }
    public required string TokenType { get; set; }
    public required UserInfo User { get; set; }
}

public record UserInfo
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public required bool MfaEnabled { get; set; }
}

public record RefreshTokenRequest(string RefreshToken);
public record LogoutRequest(string SessionId);

public record UserProfileResponse
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public required bool MfaEnabled { get; set; }
    public required string Status { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
