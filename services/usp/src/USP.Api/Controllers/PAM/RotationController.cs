using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Controller for password rotation operations
/// </summary>
[ApiController]
[Route("api/v1/pam/rotation")]
[Authorize]
public class RotationController : ControllerBase
{
    private readonly IPasswordRotationService _rotationService;
    private readonly ILogger<RotationController> _logger;

    public RotationController(
        IPasswordRotationService rotationService,
        ILogger<RotationController> logger)
    {
        _rotationService = rotationService;
        _logger = logger;
    }

    /// <summary>
    /// Manually rotate password for an account
    /// </summary>
    [HttpPost("accounts/{accountId:guid}")]
    [ProducesResponseType(typeof(PasswordRotationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PasswordRotationResultDto>> RotatePassword(Guid accountId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var result = await _rotationService.RotatePasswordAsync(accountId, userId);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating password for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to rotate password" });
        }
    }

    /// <summary>
    /// Verify current credentials work
    /// </summary>
    [HttpPost("accounts/{accountId:guid}/verify")]
    [ProducesResponseType(typeof(CredentialVerificationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<CredentialVerificationResult>> VerifyCredentials(Guid accountId)
    {
        try
        {
            var verified = await _rotationService.VerifyCredentialsAsync(accountId);

            return Ok(new CredentialVerificationResult
            {
                AccountId = accountId,
                Verified = verified,
                VerifiedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying credentials for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to verify credentials" });
        }
    }

    /// <summary>
    /// Get rotation history for an account
    /// </summary>
    [HttpGet("accounts/{accountId:guid}/history")]
    [ProducesResponseType(typeof(List<PasswordRotationHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PasswordRotationHistoryDto>>> GetRotationHistory(
        Guid accountId,
        [FromQuery] int? limit = 50)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var history = await _rotationService.GetRotationHistoryAsync(accountId, userId, limit);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rotation history for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to retrieve rotation history" });
        }
    }

    /// <summary>
    /// Get accounts due for rotation
    /// </summary>
    [HttpGet("due")]
    [ProducesResponseType(typeof(List<AccountDueForRotationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccountDueForRotationDto>>> GetAccountsDueForRotation()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var accounts = await _rotationService.GetAccountsDueForRotationAsync(userId);

            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accounts due for rotation");
            return StatusCode(500, new { error = "Failed to retrieve accounts" });
        }
    }

    /// <summary>
    /// Update rotation policy for an account
    /// </summary>
    [HttpPut("accounts/{accountId:guid}/policy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateRotationPolicy(
        Guid accountId,
        [FromBody] UpdateRotationPolicyRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            if (request.RotationIntervalDays < 1)
                return BadRequest(new { error = "Rotation interval must be at least 1 day" });

            var success = await _rotationService.UpdateRotationPolicyAsync(
                accountId,
                userId,
                request.RotationPolicy,
                request.RotationIntervalDays);

            if (!success)
                return NotFound(new { error = "Account not found or insufficient permissions" });

            return Ok(new { message = "Rotation policy updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rotation policy for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to update rotation policy" });
        }
    }

    /// <summary>
    /// Get rotation statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(RotationStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RotationStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var stats = await _rotationService.GetRotationStatisticsAsync(userId);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rotation statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// Request DTOs
public class UpdateRotationPolicyRequest
{
    public string RotationPolicy { get; set; } = "manual"; // manual, on_checkout, scheduled, on_expiration
    public int RotationIntervalDays { get; set; } = 90;
}

// Response DTOs
public class CredentialVerificationResult
{
    public Guid AccountId { get; set; }
    public bool Verified { get; set; }
    public DateTime VerifiedAt { get; set; }
}
