using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace USP.Api.Middleware;

/// <summary>
/// Mutual TLS (mTLS) authentication middleware
/// Validates client certificates and maps them to user/service identities
/// </summary>
public class MTlsAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MTlsAuthenticationMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public MTlsAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<MTlsAuthenticationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip mTLS for public endpoints
        if (ShouldSkipMTls(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            var clientCertificate = await context.Connection.GetClientCertificateAsync();

            if (clientCertificate != null)
            {
                // Validate certificate
                var validationResult = ValidateClientCertificate(clientCertificate);

                if (validationResult.IsValid)
                {
                    // Extract identity from certificate
                    var identity = ExtractIdentityFromCertificate(clientCertificate);

                    if (identity != null)
                    {
                        // Set claims
                        var claims = new List<Claim>
                        {
                            new(ClaimTypes.Name, identity.CommonName),
                            new("cert_subject", clientCertificate.Subject),
                            new("cert_issuer", clientCertificate.Issuer),
                            new("cert_thumbprint", clientCertificate.Thumbprint),
                            new("cert_serial", clientCertificate.SerialNumber),
                            new("auth_type", "mtls")
                        };

                        // Add service identity if this is a service-to-service call
                        if (identity.IsService)
                        {
                            claims.Add(new Claim("service_name", identity.ServiceName));
                            claims.Add(new Claim("service_id", identity.ServiceId));
                        }

                        var claimsIdentity = new ClaimsIdentity(claims, "mTLS");
                        context.User = new ClaimsPrincipal(claimsIdentity);

                        _logger.LogInformation("mTLS authentication successful for {CommonName} (Thumbprint: {Thumbprint})",
                            identity.CommonName, clientCertificate.Thumbprint);
                    }
                    else
                    {
                        _logger.LogWarning("Unable to extract identity from client certificate: {Subject}", clientCertificate.Subject);
                    }
                }
                else
                {
                    _logger.LogWarning("Client certificate validation failed: {Reason}. Subject: {Subject}",
                        validationResult.FailureReason, clientCertificate.Subject);

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Certificate validation failed",
                        message = validationResult.FailureReason
                    });
                    return;
                }
            }
            else
            {
                // No client certificate provided
                // This is acceptable for endpoints that support multiple auth methods
                _logger.LogDebug("No client certificate provided for {Path}", context.Request.Path);
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mTLS authentication");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Internal server error",
                message = "An error occurred during certificate authentication"
            });
        }
    }

    /// <summary>
    /// Validates client certificate
    /// </summary>
    private CertificateValidationResult ValidateClientCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate is expired
            if (certificate.NotAfter < DateTime.UtcNow)
            {
                return new CertificateValidationResult
                {
                    IsValid = false,
                    FailureReason = "Certificate has expired"
                };
            }

            // Check if certificate is not yet valid
            if (certificate.NotBefore > DateTime.UtcNow)
            {
                return new CertificateValidationResult
                {
                    IsValid = false,
                    FailureReason = "Certificate is not yet valid"
                };
            }

            // Verify certificate chain
            var chain = new X509Chain
            {
                ChainPolicy =
                {
                    RevocationMode = X509RevocationMode.Online,
                    RevocationFlag = X509RevocationFlag.EntireChain,
                    VerificationFlags = X509VerificationFlags.NoFlag
                }
            };

            var isChainValid = chain.Build(certificate);

            if (!isChainValid)
            {
                var chainErrors = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation));
                _logger.LogWarning("Certificate chain validation failed: {Errors}", chainErrors);

                // In development, we might allow self-signed certificates
                var isDevelopment = _configuration.GetValue<bool>("MTls:AllowSelfSignedInDevelopment", false);
                if (!isDevelopment)
                {
                    return new CertificateValidationResult
                    {
                        IsValid = false,
                        FailureReason = $"Certificate chain validation failed: {chainErrors}"
                    };
                }
            }

            // Verify certificate is issued by trusted CA
            var trustedIssuers = _configuration.GetSection("MTls:TrustedIssuers").Get<string[]>() ?? Array.Empty<string>();
            if (trustedIssuers.Length > 0)
            {
                var issuerMatches = trustedIssuers.Any(issuer =>
                    certificate.Issuer.Contains(issuer, StringComparison.OrdinalIgnoreCase));

                if (!issuerMatches)
                {
                    return new CertificateValidationResult
                    {
                        IsValid = false,
                        FailureReason = $"Certificate issuer not trusted: {certificate.Issuer}"
                    };
                }
            }

            // Validate certificate purpose (Extended Key Usage)
            var hasClientAuth = certificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .Any(ext => ext.EnhancedKeyUsages
                    .Cast<System.Security.Cryptography.Oid>()
                    .Any(oid => oid.Value == "1.3.6.1.5.5.7.3.2")); // Client Authentication OID

            if (!hasClientAuth)
            {
                _logger.LogWarning("Certificate does not have Client Authentication extended key usage");
            }

            return new CertificateValidationResult
            {
                IsValid = true,
                FailureReason = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during certificate validation");
            return new CertificateValidationResult
            {
                IsValid = false,
                FailureReason = $"Certificate validation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extracts identity information from certificate Common Name (CN)
    /// </summary>
    private CertificateIdentity? ExtractIdentityFromCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Parse subject DN
            var subjectParts = certificate.Subject.Split(',')
                .Select(part => part.Trim())
                .Select(part => part.Split('='))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

            if (!subjectParts.TryGetValue("CN", out var commonName))
            {
                return null;
            }

            // Check if this is a service certificate (convention: service certificates have CN like "service-name-svc")
            var isService = commonName.EndsWith("-svc", StringComparison.OrdinalIgnoreCase) ||
                           commonName.Contains("service", StringComparison.OrdinalIgnoreCase);

            string serviceId = string.Empty;
            string serviceName = string.Empty;

            if (isService)
            {
                // Extract service name from CN
                serviceName = commonName.Replace("-svc", "", StringComparison.OrdinalIgnoreCase);

                // Try to get service ID from certificate Subject Alternative Name (SAN)
                var sanExtension = certificate.Extensions
                    .OfType<X509Extension>()
                    .FirstOrDefault(ext => ext.Oid?.Value == "2.5.29.17"); // SAN OID

                if (sanExtension != null)
                {
                    // Parse SAN for service ID (simplified - production would use proper ASN.1 parsing)
                    serviceId = Guid.NewGuid().ToString(); // Placeholder
                }
                else
                {
                    serviceId = serviceName;
                }
            }

            return new CertificateIdentity
            {
                CommonName = commonName,
                IsService = isService,
                ServiceName = serviceName,
                ServiceId = serviceId,
                Organization = subjectParts.GetValueOrDefault("O", string.Empty),
                OrganizationalUnit = subjectParts.GetValueOrDefault("OU", string.Empty),
                Country = subjectParts.GetValueOrDefault("C", string.Empty)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting identity from certificate");
            return null;
        }
    }

    /// <summary>
    /// Determines if request should skip mTLS authentication
    /// </summary>
    private static bool ShouldSkipMTls(PathString path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/health/live",
            "/health/ready",
            "/swagger",
            "/metrics",
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refresh"
        };

        return skipPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Certificate validation result
    /// </summary>
    private class CertificateValidationResult
    {
        public bool IsValid { get; set; }
        public string? FailureReason { get; set; }
    }

    /// <summary>
    /// Certificate identity information
    /// </summary>
    private class CertificateIdentity
    {
        public string CommonName { get; set; } = string.Empty;
        public bool IsService { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string OrganizationalUnit { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}

/// <summary>
/// Extension methods for mTLS authentication middleware
/// </summary>
public static class MTlsAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseMTlsAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MTlsAuthenticationMiddleware>();
    }
}
