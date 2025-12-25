using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.ApiKey;
using USP.Core.Services.ApiKey;

namespace USP.Api.Controllers.ApiKey;

/// <summary>
/// API key management endpoints
/// </summary>
[ApiController]
[Route("api/v1/api-keys")]
[Authorize]
public class ApiKeyController : ControllerBase
{
    private readonly IApiKeyManagementService _apiKeyService;
    private readonly ILogger<ApiKeyController> _logger;

    public ApiKeyController(
        IApiKeyManagementService apiKeyService,
        ILogger<ApiKeyController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new API key
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateApiKeyResponse>> Create([FromBody] CreateApiKeyRequest request)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var result = await _apiKeyService.CreateApiKeyAsync(userId, request);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all API keys for current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetAll()
    {
        var userId = GetUserIdFromClaims();
        var apiKeys = await _apiKeyService.GetUserApiKeysAsync(userId);

        return Ok(apiKeys);
    }

    /// <summary>
    /// Get API key by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiKeyDto>> GetById(Guid id)
    {
        var userId = GetUserIdFromClaims();
        var apiKey = await _apiKeyService.GetApiKeyByIdAsync(userId, id);

        if (apiKey == null)
        {
            return NotFound(new { error = "API key not found" });
        }

        return Ok(apiKey);
    }

    /// <summary>
    /// Update API key
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApiKeyRequest request)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var updated = await _apiKeyService.UpdateApiKeyAsync(userId, id, request);

            if (!updated)
            {
                return NotFound(new { error = "API key not found" });
            }

            return Ok(new { message = "API key updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API key {ApiKeyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Revoke API key
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var revoked = await _apiKeyService.RevokeApiKeyAsync(userId, id);

            if (!revoked)
            {
                return NotFound(new { error = "API key not found" });
            }

            return Ok(new { message = "API key revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API key {ApiKeyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rotate API key (create new, revoke old)
    /// </summary>
    [HttpPost("{id:guid}/rotate")]
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateApiKeyResponse>> Rotate(Guid id)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var result = await _apiKeyService.RotateApiKeyAsync(userId, id);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating API key {ApiKeyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get API key usage statistics
    /// </summary>
    [HttpGet("{id:guid}/usage")]
    [ProducesResponseType(typeof(ApiKeyUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiKeyUsageDto>> GetUsage(Guid id)
    {
        var userId = GetUserIdFromClaims();
        var usage = await _apiKeyService.GetUsageStatisticsAsync(userId, id);

        if (usage == null)
        {
            return NotFound(new { error = "API key not found" });
        }

        return Ok(usage);
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in claims");
        }

        return userId;
    }
}
