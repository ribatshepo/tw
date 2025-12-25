using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Seal;
using USP.Core.Services.Cryptography;

namespace USP.Api.Controllers.System;

/// <summary>
/// Seal/unseal operations for the security platform
/// Uses Shamir's Secret Sharing to protect the master encryption key
/// </summary>
[ApiController]
[Route("api/sys/seal")]
[Produces("application/json")]
public class SealController : ControllerBase
{
    private readonly ISealManager _sealManager;
    private readonly ILogger<SealController> _logger;

    public SealController(
        ISealManager sealManager,
        ILogger<SealController> logger)
    {
        _sealManager = sealManager;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the seal with Shamir's Secret Sharing
    /// WARNING: This can only be done once. Key shares must be distributed securely.
    /// </summary>
    [HttpPost("init")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InitializeSealResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InitializeSealResponse>> Initialize([FromBody] InitializeSealRequest request)
    {
        try
        {
            var response = await _sealManager.InitializeAsync(request);
            _logger.LogWarning("Seal initialized with {Shares} shares and threshold {Threshold}",
                request.SecretShares, request.SecretThreshold);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Initialization failed",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing seal");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred during seal initialization"
            });
        }
    }

    /// <summary>
    /// Submit an unseal key share
    /// Submit threshold number of shares to unseal the system
    /// </summary>
    [HttpPost("unseal")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SealStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SealStatusResponse>> Unseal([FromBody] UnsealRequest request)
    {
        try
        {
            var response = await _sealManager.UnsealAsync(request);

            if (!response.Sealed)
            {
                _logger.LogWarning("System unsealed successfully");
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unseal failed",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unseal");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred during unseal operation"
            });
        }
    }

    /// <summary>
    /// Seal the system (clear master key from memory)
    /// Requires authentication
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(SealResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SealResponse>> Seal()
    {
        try
        {
            var response = await _sealManager.SealAsync();
            _logger.LogWarning("System sealed by user");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sealing system");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while sealing the system"
            });
        }
    }

    /// <summary>
    /// Get current seal status
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SealStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SealStatusResponse>> GetStatus()
    {
        try
        {
            var response = await _sealManager.GetStatusAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting seal status");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while retrieving seal status"
            });
        }
    }
}
