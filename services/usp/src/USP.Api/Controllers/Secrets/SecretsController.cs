using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Secrets;
using USP.Core.Services.Authentication;
using USP.Core.Services.Secrets;

namespace USP.Api.Controllers.Secrets;

/// <summary>
/// Vault KV v2 compatible secrets management endpoints
/// Provides versioned key-value secret storage with encryption
/// </summary>
[ApiController]
[Route("api/secrets")]
[Authorize]
[Produces("application/json")]
public class SecretsController : ControllerBase
{
    private readonly IKvEngine _kvEngine;
    private readonly IJwtService _jwtService;
    private readonly ILogger<SecretsController> _logger;

    public SecretsController(
        IKvEngine kvEngine,
        IJwtService jwtService,
        ILogger<SecretsController> logger)
    {
        _kvEngine = kvEngine;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Create or update a secret (Vault: POST /v1/{mount}/data/{path})
    /// </summary>
    [HttpPost("data/{*path}")]
    [ProducesResponseType(typeof(CreateSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateSecretResponse>> CreateSecret(string path, [FromBody] CreateSecretRequest request)
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

            var response = await _kvEngine.CreateSecretAsync(path, request, userId.Value);
            return Ok(response);
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operation failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating secret at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while creating the secret"
            });
        }
    }

    /// <summary>
    /// Read a secret (Vault: GET /v1/{mount}/data/{path})
    /// </summary>
    [HttpGet("data/{*path}")]
    [ProducesResponseType(typeof(ReadSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ReadSecretResponse>> ReadSecret(string path, [FromQuery] int? version = null)
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

            var response = await _kvEngine.ReadSecretAsync(path, version, userId.Value);

            if (response == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Secret not found",
                    Detail = $"No secret found at path: {path}"
                });
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operation failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading secret at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while reading the secret"
            });
        }
    }

    /// <summary>
    /// Soft delete secret versions (Vault: POST /v1/{mount}/delete/{path})
    /// </summary>
    [HttpPost("delete/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteSecretVersions(string path, [FromBody] DeleteSecretVersionsRequest request)
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

            await _kvEngine.DeleteSecretVersionsAsync(path, request, userId.Value);
            return NoContent();
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
            _logger.LogError(ex, "Error deleting secret versions at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while deleting secret versions"
            });
        }
    }

    /// <summary>
    /// Undelete soft-deleted secret versions (Vault: POST /v1/{mount}/undelete/{path})
    /// </summary>
    [HttpPost("undelete/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UndeleteSecretVersions(string path, [FromBody] UndeleteSecretVersionsRequest request)
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

            await _kvEngine.UndeleteSecretVersionsAsync(path, request, userId.Value);
            return NoContent();
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
            _logger.LogError(ex, "Error undeleting secret versions at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while undeleting secret versions"
            });
        }
    }

    /// <summary>
    /// Permanently destroy secret versions (Vault: POST /v1/{mount}/destroy/{path})
    /// </summary>
    [HttpPost("destroy/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DestroySecretVersions(string path, [FromBody] DestroySecretVersionsRequest request)
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

            await _kvEngine.DestroySecretVersionsAsync(path, request, userId.Value);
            return NoContent();
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
            _logger.LogError(ex, "Error destroying secret versions at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while destroying secret versions"
            });
        }
    }

    /// <summary>
    /// Read secret metadata (Vault: GET /v1/{mount}/metadata/{path})
    /// </summary>
    [HttpGet("metadata/{*path}")]
    [ProducesResponseType(typeof(SecretMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SecretMetadata>> ReadSecretMetadata(string path)
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

            var metadata = await _kvEngine.ReadSecretMetadataAsync(path, userId.Value);

            if (metadata == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Metadata not found",
                    Detail = $"No metadata found for path: {path}"
                });
            }

            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading metadata at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while reading metadata"
            });
        }
    }

    /// <summary>
    /// Update secret metadata (Vault: POST /v1/{mount}/metadata/{path})
    /// </summary>
    [HttpPost("metadata/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateSecretMetadata(string path, [FromBody] UpdateSecretMetadataRequest request)
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

            await _kvEngine.UpdateSecretMetadataAsync(path, request, userId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metadata at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while updating metadata"
            });
        }
    }

    /// <summary>
    /// Delete all versions and metadata (Vault: DELETE /v1/{mount}/metadata/{path})
    /// </summary>
    [HttpDelete("metadata/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteSecretMetadata(string path)
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

            await _kvEngine.DeleteSecretMetadataAsync(path, userId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting metadata at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while deleting metadata"
            });
        }
    }

    /// <summary>
    /// List secrets at a path (Vault: LIST /v1/{mount}/metadata/{path})
    /// </summary>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ListSecretsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ListSecretsResponse>> ListSecrets([FromQuery] string? path = null)
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

            var response = await _kvEngine.ListSecretsAsync(path ?? string.Empty, userId.Value);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing secrets at path {Path}", path);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An error occurred while listing secrets"
            });
        }
    }
}
