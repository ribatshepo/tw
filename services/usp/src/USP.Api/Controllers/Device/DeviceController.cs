using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Device;
using USP.Core.Services.Authentication;
using USP.Core.Services.Device;

namespace USP.Api.Controllers.Device;

/// <summary>
/// Device and trusted device management endpoints
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize]
[Produces("application/json")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceFingerprintService _deviceService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(
        IDeviceFingerprintService deviceService,
        IJwtService jwtService,
        ILogger<DeviceController> logger)
    {
        _deviceService = deviceService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Register current device as trusted
    /// </summary>
    [HttpPost("trusted/register")]
    [ProducesResponseType(typeof(RegisterTrustedDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterTrustedDeviceResponse>> RegisterTrustedDevice([FromBody] RegisterTrustedDeviceRequest request)
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

            var userAgent = Request.Headers.UserAgent.ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var deviceFingerprint = _deviceService.GenerateFingerprint(userAgent, ipAddress, request.DeviceInfo);
            var device = await _deviceService.RegisterTrustedDeviceAsync(userId.Value, deviceFingerprint, request.DeviceName);

            _logger.LogInformation("Trusted device registered for user {UserId}", userId);

            return Ok(new RegisterTrustedDeviceResponse
            {
                DeviceId = device.Id,
                DeviceFingerprint = deviceFingerprint,
                RegisteredAt = device.RegisteredAt,
                TrustDurationDays = 30
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering trusted device");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while registering the trusted device"
            });
        }
    }

    /// <summary>
    /// Get user's trusted devices
    /// </summary>
    [HttpGet("trusted")]
    [ProducesResponseType(typeof(IEnumerable<TrustedDeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TrustedDeviceDto>>> GetTrustedDevices()
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

            var devices = await _deviceService.GetTrustedDevicesAsync(userId.Value);
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trusted devices");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while retrieving trusted devices"
            });
        }
    }

    /// <summary>
    /// Remove a trusted device
    /// </summary>
    [HttpDelete("trusted/{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTrustedDevice(Guid deviceId)
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

            var result = await _deviceService.RemoveTrustedDeviceAsync(userId.Value, deviceId);

            if (!result)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Device not found",
                    Detail = $"Trusted device {deviceId} not found"
                });
            }

            _logger.LogInformation("Trusted device {DeviceId} removed for user {UserId}", deviceId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing trusted device {DeviceId}", deviceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while removing the trusted device"
            });
        }
    }

    /// <summary>
    /// Check if current device is trusted
    /// </summary>
    [HttpGet("trusted/check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<bool>> CheckTrustedDevice()
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

            var userAgent = Request.Headers.UserAgent.ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var deviceFingerprint = _deviceService.GenerateFingerprint(userAgent, ipAddress);
            var isTrusted = await _deviceService.IsTrustedDeviceAsync(userId.Value, deviceFingerprint);

            return Ok(isTrusted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking trusted device");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while checking trusted device status"
            });
        }
    }
}
