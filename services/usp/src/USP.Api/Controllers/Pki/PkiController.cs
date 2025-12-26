using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Pki;
using USP.Core.Services.Authentication;
using USP.Core.Services.Secrets;

namespace USP.Api.Controllers.Pki;

/// <summary>
/// PKI Engine controller for Certificate Authority management and certificate lifecycle operations
/// </summary>
[ApiController]
[Route("api/v1/pki")]
[Authorize]
public class PkiController : ControllerBase
{
    private readonly IPkiEngine _pkiEngine;
    private readonly IJwtService _jwtService;
    private readonly ILogger<PkiController> _logger;

    public PkiController(
        IPkiEngine pkiEngine,
        IJwtService jwtService,
        ILogger<PkiController> logger)
    {
        _pkiEngine = pkiEngine;
        _jwtService = jwtService;
        _logger = logger;
    }

    // ====================
    // CA Management Endpoints
    // ====================

    /// <summary>
    /// Create a new root Certificate Authority
    /// </summary>
    /// <param name="request">Root CA creation parameters</param>
    /// <returns>Created CA information</returns>
    [HttpPost("ca/root")]
    [ProducesResponseType(typeof(CertificateAuthorityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CertificateAuthorityResponse>> CreateRootCa([FromBody] CreateRootCaRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var ca = await _pkiEngine.CreateRootCaAsync(request, userId);
            return CreatedAtAction(nameof(GetCa), new { name = ca.Name }, ca);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create root CA: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Parameters",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating root CA");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while creating the root CA"
            });
        }
    }

    /// <summary>
    /// Create a new intermediate Certificate Authority
    /// </summary>
    /// <param name="request">Intermediate CA creation parameters</param>
    /// <returns>Created CA information</returns>
    [HttpPost("ca/intermediate")]
    [ProducesResponseType(typeof(CertificateAuthorityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CertificateAuthorityResponse>> CreateIntermediateCa([FromBody] CreateIntermediateCaRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var ca = await _pkiEngine.CreateIntermediateCaAsync(request, userId);
            return CreatedAtAction(nameof(GetCa), new { name = ca.Name }, ca);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create intermediate CA: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Parameters",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating intermediate CA");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while creating the intermediate CA"
            });
        }
    }

    /// <summary>
    /// Get Certificate Authority information by name
    /// </summary>
    /// <param name="name">CA name</param>
    /// <returns>CA information</returns>
    [HttpGet("ca/{name}")]
    [ProducesResponseType(typeof(CertificateAuthorityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CertificateAuthorityResponse>> GetCa(string name)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var ca = await _pkiEngine.ReadCaAsync(name, userId);
            return Ok(ca);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "CA not found: {Name}", name);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading CA: {Name}", name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while reading the CA"
            });
        }
    }

    /// <summary>
    /// List all Certificate Authorities
    /// </summary>
    /// <returns>List of CA names</returns>
    [HttpGet("ca")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> ListCas()
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var cas = await _pkiEngine.ListCasAsync(userId);
            return Ok(cas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing CAs");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while listing CAs"
            });
        }
    }

    /// <summary>
    /// Delete a Certificate Authority and all its issued certificates
    /// </summary>
    /// <param name="name">CA name to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("ca/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteCa(string name)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            await _pkiEngine.DeleteCaAsync(name, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete CA: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting CA: {Name}", name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while deleting the CA"
            });
        }
    }

    /// <summary>
    /// Revoke a Certificate Authority
    /// </summary>
    /// <param name="name">CA name to revoke</param>
    /// <returns>No content on success</returns>
    [HttpPost("ca/{name}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RevokeCa(string name)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            await _pkiEngine.RevokeCaAsync(name, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to revoke CA: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error revoking CA: {Name}", name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while revoking the CA"
            });
        }
    }

    // ====================
    // Role Management Endpoints
    // ====================

    /// <summary>
    /// Create a new certificate role/template
    /// </summary>
    /// <param name="request">Role creation parameters</param>
    /// <returns>Created role information</returns>
    [HttpPost("roles")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoleResponse>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var role = await _pkiEngine.CreateRoleAsync(request, userId);
            return CreatedAtAction(nameof(GetRole), new { name = role.Name }, role);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create role: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Parameters",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating role");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while creating the role"
            });
        }
    }

    /// <summary>
    /// Get certificate role information by name
    /// </summary>
    /// <param name="name">Role name</param>
    /// <returns>Role information</returns>
    [HttpGet("roles/{name}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoleResponse>> GetRole(string name)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var role = await _pkiEngine.ReadRoleAsync(name, userId);
            return Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Role not found: {Name}", name);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading role: {Name}", name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while reading the role"
            });
        }
    }

    /// <summary>
    /// List all certificate roles
    /// </summary>
    /// <returns>List of role names</returns>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> ListRoles()
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var roles = await _pkiEngine.ListRolesAsync(userId);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing roles");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while listing roles"
            });
        }
    }

    /// <summary>
    /// Delete a certificate role
    /// </summary>
    /// <param name="name">Role name to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("roles/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRole(string name)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            await _pkiEngine.DeleteRoleAsync(name, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Role not found: {Name}", name);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting role: {Name}", name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while deleting the role"
            });
        }
    }

    // ====================
    // Certificate Issuance Endpoints
    // ====================

    /// <summary>
    /// Issue a new certificate using a role
    /// Generates both certificate and private key
    /// </summary>
    /// <param name="roleName">Role to use for certificate issuance</param>
    /// <param name="request">Certificate issuance parameters</param>
    /// <returns>Issued certificate with private key</returns>
    [HttpPost("issue/{roleName}")]
    [ProducesResponseType(typeof(IssueCertificateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IssueCertificateResponse>> IssueCertificate(string roleName, [FromBody] IssueCertificateRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _pkiEngine.IssueCertificateAsync(roleName, request, userId);
            return Created($"/api/v1/pki/certificates/{response.SerialNumber}", response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to issue certificate: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Parameters",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error issuing certificate");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while issuing the certificate"
            });
        }
    }

    /// <summary>
    /// Sign a Certificate Signing Request using a role
    /// User provides the CSR with their own private key
    /// </summary>
    /// <param name="roleName">Role to use for signing</param>
    /// <param name="request">CSR signing parameters</param>
    /// <returns>Signed certificate</returns>
    [HttpPost("sign/{roleName}")]
    [ProducesResponseType(typeof(IssueCertificateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IssueCertificateResponse>> SignCsr(string roleName, [FromBody] SignCsrRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _pkiEngine.SignCsrAsync(roleName, request, userId);
            return Created($"/api/v1/pki/certificates/{response.SerialNumber}", response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to sign CSR: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid CSR or parameters: {Message}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Parameters",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error signing CSR");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while signing the CSR"
            });
        }
    }

    // ====================
    // Certificate Operations Endpoints
    // ====================

    /// <summary>
    /// Revoke a certificate by serial number
    /// </summary>
    /// <param name="request">Revocation parameters</param>
    /// <returns>Revocation status</returns>
    [HttpPost("revoke")]
    [ProducesResponseType(typeof(RevokeCertificateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RevokeCertificateResponse>> RevokeCertificate([FromBody] RevokeCertificateRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _pkiEngine.RevokeCertificateAsync(request, userId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to revoke certificate: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error revoking certificate");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while revoking the certificate"
            });
        }
    }

    /// <summary>
    /// List certificates issued by a CA or all certificates
    /// </summary>
    /// <param name="caName">Optional CA name filter</param>
    /// <returns>List of certificates</returns>
    [HttpGet("certificates")]
    [ProducesResponseType(typeof(ListCertificatesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ListCertificatesResponse>> ListCertificates([FromQuery] string? caName = null)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _pkiEngine.ListCertificatesAsync(caName, userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing certificates");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while listing certificates"
            });
        }
    }

    /// <summary>
    /// Get certificate information by serial number
    /// </summary>
    /// <param name="serialNumber">Certificate serial number</param>
    /// <returns>Certificate information</returns>
    [HttpGet("certificates/{serialNumber}")]
    [ProducesResponseType(typeof(CertificateInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CertificateInfo>> GetCertificate(string serialNumber)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var cert = await _pkiEngine.ReadCertificateAsync(serialNumber, userId);
            return Ok(cert);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Certificate not found: {SerialNumber}", serialNumber);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading certificate: {SerialNumber}", serialNumber);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while reading the certificate"
            });
        }
    }

    // ====================
    // CRL Management Endpoint
    // ====================

    /// <summary>
    /// Generate a Certificate Revocation List for a CA
    /// </summary>
    /// <param name="caName">CA name</param>
    /// <returns>CRL in PEM format</returns>
    [HttpGet("crl/{caName}")]
    [ProducesResponseType(typeof(GetCrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetCrlResponse>> GetCrl(string caName)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var crl = await _pkiEngine.GenerateCrlAsync(caName, userId);
            return Ok(crl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to generate CRL: {Message}", ex.Message);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating CRL for CA: {CaName}", caName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while generating the CRL"
            });
        }
    }

    // ====================
    // Helper Methods
    // ====================

    private Guid GetAuthenticatedUserId()
    {
        var userId = _jwtService.GetUserIdFromClaims(User);

        if (userId == null || userId == Guid.Empty)
        {
            _logger.LogWarning("Unable to extract user ID from JWT claims");
            throw new UnauthorizedAccessException("Invalid authentication token");
        }

        return userId.Value;
    }
}
