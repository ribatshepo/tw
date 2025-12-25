using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Services.Authentication;

namespace USP.Api.Controllers.Authentication;

/// <summary>
/// Authentication endpoints for login, registration, and token management
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IAuthenticationService authenticationService,
        IJwtService jwtService,
        ILogger<AuthenticationController> logger)
    {
        _authenticationService = authenticationService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user and obtain access token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT access token and refresh token</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var response = await _authenticationService.LoginAsync(request, ipAddress, userAgent);

            _logger.LogInformation("User {Username} logged in successfully from {IpAddress}",
                request.Username, ipAddress);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for user {Username}: {Message}",
                request.Username, ex.Message);
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred during authentication"
            });
        }
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration data</param>
    /// <returns>JWT access token and refresh token</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var response = await _authenticationService.RegisterAsync(request, ipAddress, userAgent);

            _logger.LogInformation("User {Username} registered successfully from {IpAddress}",
                request.Username, ipAddress);

            return CreatedAtAction(nameof(Login), new { username = request.Username }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for user {Username}: {Message}",
                request.Username, ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Registration failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred during registration"
            });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New JWT access token and refresh token</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var response = await _authenticationService.RefreshTokenAsync(request, ipAddress, userAgent);

            _logger.LogInformation("Token refreshed for user {UserId} from {IpAddress}",
                response.User.Id, ipAddress);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Token refresh failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred during token refresh"
            });
        }
    }

    /// <summary>
    /// Logout and revoke current session
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = _jwtService.GetUserIdFromClaims(User);

            if (userId == null)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Invalid user claims"
                });
            }

            var authHeader = HttpContext.Request.Headers.Authorization.ToString();
            var token = authHeader.Replace("Bearer ", string.Empty);

            await _authenticationService.LogoutAsync(userId.Value, token);

            _logger.LogInformation("User {UserId} logged out successfully", userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred during logout"
            });
        }
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    /// <returns>User information</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = _jwtService.GetUserIdFromClaims(User);

        if (userId == null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Invalid user claims"
            });
        }

        var userInfo = new UserInfo
        {
            Id = userId.Value,
            Username = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            FirstName = User.FindFirstValue(ClaimTypes.GivenName),
            LastName = User.FindFirstValue(ClaimTypes.Surname),
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
        };

        return Ok(userInfo);
    }
}
