using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Models.DTOs.CloudSync;
using USP.Core.Services.CloudSync;

namespace USP.Api.Controllers.CloudSync;

/// <summary>
/// Controller for cloud secrets synchronization management
/// </summary>
[ApiController]
[Route("api/v1/cloud-sync")]
[Authorize]
[Produces("application/json")]
public class CloudSyncController : ControllerBase
{
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<CloudSyncController> _logger;

    public CloudSyncController(
        ICloudSyncService cloudSyncService,
        ILogger<CloudSyncController> logger)
    {
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Configure cloud sync
    /// </summary>
    /// <param name="request">Cloud sync configuration request</param>
    /// <returns>Created configuration</returns>
    [HttpPost("configure")]
    [ProducesResponseType(typeof(CloudSyncConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CloudSyncConfigurationDto>> ConfigureSync([FromBody] CreateCloudSyncConfigurationRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var configuration = await _cloudSyncService.ConfigureAsync(request, currentUserId);

            _logger.LogInformation("Cloud sync configured: {ConfigurationId}, Provider: {Provider}",
                configuration.Id, configuration.Provider);

            return CreatedAtAction(nameof(GetConfiguration), new { id = configuration.Id }, configuration);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to configure cloud sync: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cloud sync configuration failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring cloud sync");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while configuring cloud sync"
            });
        }
    }

    /// <summary>
    /// Get cloud sync configuration
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <returns>Cloud sync configuration</returns>
    [HttpGet("configurations/{id:guid}")]
    [ProducesResponseType(typeof(CloudSyncConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CloudSyncConfigurationDto>> GetConfiguration(Guid id)
    {
        try
        {
            var configuration = await _cloudSyncService.GetConfigurationAsync(id);

            if (configuration == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Configuration not found",
                    Detail = $"Cloud sync configuration with ID '{id}' was not found"
                });
            }

            return Ok(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cloud sync configuration: {ConfigurationId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while retrieving the configuration"
            });
        }
    }

    /// <summary>
    /// List cloud sync configurations
    /// </summary>
    /// <returns>List of configurations</returns>
    [HttpGet("configurations")]
    [ProducesResponseType(typeof(List<CloudSyncConfigurationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CloudSyncConfigurationDto>>> ListConfigurations()
    {
        try
        {
            var configurations = await _cloudSyncService.ListConfigurationsAsync();

            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing cloud sync configurations");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while listing configurations"
            });
        }
    }

    /// <summary>
    /// Trigger sync manually
    /// </summary>
    /// <param name="configurationId">Configuration ID (optional, if not in body)</param>
    /// <param name="request">Trigger sync request</param>
    /// <returns>Sync history entry</returns>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(CloudSyncHistoryDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CloudSyncHistoryDto>> TriggerSync(
        [FromQuery] Guid? configurationId,
        [FromBody] TriggerSyncRequest request)
    {
        try
        {
            if (!configurationId.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request",
                    Detail = "Configuration ID is required"
                });
            }

            var currentUserId = GetCurrentUserId();
            var history = await _cloudSyncService.TriggerSyncAsync(configurationId.Value, request, currentUserId);

            _logger.LogInformation("Sync triggered: ConfigurationId={ConfigurationId}, HistoryId={HistoryId}",
                configurationId.Value, history.Id);

            return Accepted(history);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to trigger sync: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Configuration not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Trigger sync failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering sync");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while triggering sync"
            });
        }
    }

    /// <summary>
    /// Get sync status
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <returns>Latest sync status</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(CloudSyncHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CloudSyncHistoryDto>> GetSyncStatus([FromQuery] Guid configurationId)
    {
        try
        {
            var status = await _cloudSyncService.GetSyncStatusAsync(configurationId);

            if (status == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "No sync history found",
                    Detail = "No sync history found for this configuration"
                });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sync status: {ConfigurationId}", configurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while retrieving sync status"
            });
        }
    }

    /// <summary>
    /// List sync conflicts
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <param name="status">Filter by conflict status</param>
    /// <returns>List of conflicts</returns>
    [HttpGet("conflicts")]
    [ProducesResponseType(typeof(List<CloudSyncConflictDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CloudSyncConflictDto>>> ListConflicts(
        [FromQuery] Guid configurationId,
        [FromQuery] string? status = null)
    {
        try
        {
            var conflicts = await _cloudSyncService.ListConflictsAsync(configurationId, status);

            return Ok(conflicts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing sync conflicts: {ConfigurationId}", configurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while listing conflicts"
            });
        }
    }

    /// <summary>
    /// Resolve sync conflict
    /// </summary>
    /// <param name="id">Conflict ID</param>
    /// <param name="request">Resolution request</param>
    /// <returns>Resolved conflict</returns>
    [HttpPost("conflicts/{id:guid}/resolve")]
    [ProducesResponseType(typeof(CloudSyncConflictDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CloudSyncConflictDto>> ResolveConflict(
        Guid id,
        [FromBody] ResolveConflictRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var conflict = await _cloudSyncService.ResolveConflictAsync(id, request, currentUserId);

            _logger.LogInformation("Conflict resolved: {ConflictId}, Strategy: {Strategy}",
                id, request.Resolution);

            return Ok(conflict);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to resolve conflict {ConflictId}: {Message}", id, ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Conflict not found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Conflict resolution failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict: {ConflictId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while resolving the conflict"
            });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }
}
