using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.API.Controllers.v1;

/// <summary>
/// Multi-Factor Authentication management endpoints
/// </summary>
[ApiController]
[Route("api/v1/mfa")]
[Produces("application/json")]
[Authorize]
public class MFAController : ControllerBase
{
    private readonly IMFAService _mfaService;
    private readonly ITOTPService _totpService;
    private readonly ILogger<MFAController> _logger;

    public MFAController(
        IMFAService mfaService,
        ITOTPService totpService,
        ILogger<MFAController> logger)
    {
        _mfaService = mfaService;
        _totpService = totpService;
        _logger = logger;
    }

    /// <summary>
    /// Get all enrolled MFA devices for current user
    /// </summary>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(List<MFADeviceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var devices = await _mfaService.GetUserMFADevicesAsync(userId);

        var response = devices.Select(d => new MFADeviceResponse
        {
            Id = d.Id,
            Method = d.Method.ToString(),
            DeviceName = d.DeviceName,
            IsVerified = d.IsVerified,
            IsPrimary = d.IsPrimary,
            LastUsedAt = d.LastUsedAt,
            EnrolledAt = d.EnrolledAt,
            ExpiresAt = d.ExpiresAt
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Enroll a new MFA device
    /// </summary>
    [HttpPost("enroll")]
    [ProducesResponseType(typeof(EnrollMFAResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnrollDevice([FromBody] EnrollMFARequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (!Enum.TryParse<MFAMethod>(request.Method, out var method))
            {
                return BadRequest(new { error = "Invalid MFA method" });
            }

            var device = await _mfaService.EnrollDeviceAsync(userId, method, request.DeviceName);

            var response = new EnrollMFAResponse
            {
                DeviceId = device.Id,
                Method = device.Method.ToString(),
                DeviceName = device.DeviceName,
                IsVerified = device.IsVerified
            };

            // For TOTP, include provisioning URI for QR code
            if (method == MFAMethod.TOTP && device.DeviceData != null)
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? userId;
                response.ProvisioningUri = _totpService.GenerateProvisioningUri(email, device.DeviceData);
                response.Secret = device.DeviceData;
            }

            _logger.LogInformation("MFA device enrollment initiated for user {UserId}: {Method}", userId, method);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA enrollment failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Verify MFA device enrollment
    /// </summary>
    [HttpPost("verify-enrollment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEnrollment([FromBody] VerifyEnrollmentRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var isValid = await _mfaService.VerifyEnrollmentAsync(userId, request.DeviceId, request.Code);

            if (!isValid)
            {
                _logger.LogWarning("MFA enrollment verification failed for user {UserId}", userId);
                return BadRequest(new { error = "Invalid verification code" });
            }

            _logger.LogInformation("MFA device verified for user {UserId}: {DeviceId}", userId, request.DeviceId);

            return Ok(new { message = "MFA device verified successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA verification failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove an MFA device
    /// </summary>
    [HttpDelete("devices/{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDevice(string deviceId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _mfaService.RemoveMFADeviceAsync(userId, deviceId);

            _logger.LogInformation("MFA device removed for user {UserId}: {DeviceId}", userId, deviceId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("MFA device removal failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA device removal failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set a device as primary MFA method
    /// </summary>
    [HttpPost("devices/{deviceId}/set-primary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimaryDevice(string deviceId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _mfaService.SetPrimaryDeviceAsync(userId, deviceId);

            _logger.LogInformation("Primary MFA device set for user {UserId}: {DeviceId}", userId, deviceId);

            return Ok(new { message = "Primary device set successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Set primary device failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Set primary device failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate new backup codes
    /// </summary>
    [HttpPost("backup-codes")]
    [ProducesResponseType(typeof(BackupCodesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateBackupCodes()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var codes = await _mfaService.GenerateBackupCodesAsync(userId);

            _logger.LogInformation("Backup codes generated for user {UserId}", userId);

            return Ok(new BackupCodesResponse
            {
                Codes = codes,
                Message = "Store these codes in a safe place. Each code can only be used once."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup codes generation failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all trusted devices
    /// </summary>
    [HttpGet("trusted-devices")]
    [ProducesResponseType(typeof(List<TrustedDeviceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrustedDevices()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var devices = await _mfaService.GetTrustedDevicesAsync(userId);

        var response = devices.Select(d => new TrustedDeviceResponse
        {
            Id = d.Id,
            DeviceName = d.DeviceName,
            DeviceType = d.DeviceType.ToString(),
            IpAddress = d.IpAddress,
            Location = d.Location,
            LastUsedAt = d.LastUsedAt,
            TrustedAt = d.TrustedAt,
            ExpiresAt = d.ExpiresAt
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Trust current device
    /// </summary>
    [HttpPost("trust-device")]
    [ProducesResponseType(typeof(TrustedDeviceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrustDevice([FromBody] TrustDeviceRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var device = await _mfaService.TrustDeviceAsync(
                userId,
                request.DeviceFingerprint,
                request.DeviceName,
                ipAddress,
                userAgent,
                request.TrustDays);

            _logger.LogInformation("Device trusted for user {UserId}: {DeviceId}", userId, device.Id);

            return Ok(new TrustedDeviceResponse
            {
                Id = device.Id,
                DeviceName = device.DeviceName,
                DeviceType = device.DeviceType.ToString(),
                IpAddress = device.IpAddress,
                Location = device.Location,
                TrustedAt = device.TrustedAt,
                ExpiresAt = device.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trust device failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Revoke trust for a device
    /// </summary>
    [HttpDelete("trusted-devices/{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeTrustedDevice(string deviceId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _mfaService.RevokeTrustedDeviceAsync(userId, deviceId);

            _logger.LogInformation("Trusted device revoked for user {UserId}: {DeviceId}", userId, deviceId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Revoke trusted device failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revoke trusted device failed");
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Request/Response DTOs

public record EnrollMFARequest(string Method, string DeviceName);

public record EnrollMFAResponse
{
    public required string DeviceId { get; set; }
    public required string Method { get; set; }
    public required string DeviceName { get; set; }
    public required bool IsVerified { get; set; }
    public string? ProvisioningUri { get; set; }
    public string? Secret { get; set; }
}

public record VerifyEnrollmentRequest(string DeviceId, string Code);

public record MFADeviceResponse
{
    public required string Id { get; set; }
    public required string Method { get; set; }
    public required string DeviceName { get; set; }
    public required bool IsVerified { get; set; }
    public required bool IsPrimary { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public required DateTime EnrolledAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public record BackupCodesResponse
{
    public required List<string> Codes { get; set; }
    public required string Message { get; set; }
}

public record TrustDeviceRequest(string DeviceFingerprint, string DeviceName, int TrustDays = 30);

public record TrustedDeviceResponse
{
    public required string Id { get; set; }
    public required string DeviceName { get; set; }
    public required string DeviceType { get; set; }
    public required string IpAddress { get; set; }
    public string? Location { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public required DateTime TrustedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
