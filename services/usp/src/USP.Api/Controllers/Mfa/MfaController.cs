using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Mfa;
using USP.Core.Services.Authentication;
using USP.Core.Services.Mfa;

namespace USP.Api.Controllers.Mfa;

/// <summary>
/// Multi-Factor Authentication management endpoints
/// </summary>
[ApiController]
[Route("api/mfa")]
[Authorize]
[Produces("application/json")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<MfaController> _logger;

    public MfaController(
        IMfaService mfaService,
        IJwtService jwtService,
        ILogger<MfaController> logger)
    {
        _mfaService = mfaService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Enroll in TOTP (Time-based One-Time Password) authentication
    /// </summary>
    [HttpPost("totp/enroll")]
    [ProducesResponseType(typeof(EnrollTotpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnrollTotpResponse>> EnrollTotp([FromBody] EnrollTotpRequest request)
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

            var response = await _mfaService.EnrollTotpAsync(userId.Value, request.DeviceName);

            _logger.LogInformation("TOTP enrollment initiated for user {UserId}", userId);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Enrollment failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TOTP enrollment");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred during TOTP enrollment"
            });
        }
    }

    /// <summary>
    /// Verify TOTP code and complete enrollment
    /// </summary>
    [HttpPost("totp/verify")]
    [ProducesResponseType(typeof(VerifyTotpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VerifyTotpResponse>> VerifyTotp([FromBody] VerifyTotpRequest request)
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

            var response = await _mfaService.VerifyAndCompleteTotpEnrollmentAsync(userId.Value, request.Code);

            _logger.LogInformation("TOTP enrollment completed for user {UserId}", userId);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Verification failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TOTP verification");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred during TOTP verification"
            });
        }
    }

    /// <summary>
    /// Get user's MFA devices
    /// </summary>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(IEnumerable<MfaDeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<MfaDeviceDto>>> GetDevices()
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

            var devices = await _mfaService.GetUserMfaDevicesAsync(userId.Value);
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MFA devices");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while retrieving MFA devices"
            });
        }
    }

    /// <summary>
    /// Generate new backup codes
    /// </summary>
    [HttpPost("backup-codes/generate")]
    [ProducesResponseType(typeof(GenerateBackupCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerateBackupCodesResponse>> GenerateBackupCodes()
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

            var response = await _mfaService.GenerateBackupCodesAsync(userId.Value);

            _logger.LogInformation("Backup codes generated for user {UserId}", userId);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Failed to generate backup codes",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating backup codes");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while generating backup codes"
            });
        }
    }

    /// <summary>
    /// Disable MFA for current user
    /// </summary>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest request)
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

            var result = await _mfaService.DisableMfaAsync(userId.Value, request.Password);

            if (!result)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Failed to disable MFA",
                    Detail = "Could not disable MFA"
                });
            }

            _logger.LogInformation("MFA disabled for user {UserId}", userId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Failed to disable MFA",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling MFA");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while disabling MFA"
            });
        }
    }

    /// <summary>
    /// Remove an MFA device
    /// </summary>
    [HttpDelete("devices/{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDevice(Guid deviceId)
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

            var result = await _mfaService.RemoveMfaDeviceAsync(userId.Value, deviceId);

            if (!result)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Device not found",
                    Detail = $"MFA device {deviceId} not found"
                });
            }

            _logger.LogInformation("MFA device {DeviceId} removed for user {UserId}", deviceId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing MFA device {DeviceId}", deviceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while removing the MFA device"
            });
        }
    }

    /// <summary>
    /// Set an MFA device as primary
    /// </summary>
    [HttpPost("devices/{deviceId:guid}/set-primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimaryDevice(Guid deviceId)
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

            var result = await _mfaService.SetPrimaryMfaDeviceAsync(userId.Value, deviceId);

            if (!result)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Device not found",
                    Detail = $"MFA device {deviceId} not found"
                });
            }

            _logger.LogInformation("MFA device {DeviceId} set as primary for user {UserId}", deviceId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary MFA device {DeviceId}", deviceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while setting the primary MFA device"
            });
        }
    }
}
