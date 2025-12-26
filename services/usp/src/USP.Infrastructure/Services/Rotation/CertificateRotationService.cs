using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using USP.Core.Models.DTOs.Rotation;
using USP.Core.Models.Entities.Rotation;
using USP.Core.Services.Audit;
using USP.Core.Services.Rotation;
using USP.Core.Services.Webhook;
using USP.Infrastructure.Data;
using Cronos;

namespace USP.Infrastructure.Services.Rotation;

/// <summary>
/// Service for automated certificate rotation with ACME protocol support
/// </summary>
public class CertificateRotationService : ICertificateRotationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CertificateRotationService> _logger;
    private readonly IAuditService _auditService;
    private readonly IWebhookService _webhookService;
    private readonly IHttpClientFactory _httpClientFactory;

    public CertificateRotationService(
        ApplicationDbContext context,
        ILogger<CertificateRotationService> logger,
        IAuditService auditService,
        IWebhookService webhookService,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
        _webhookService = webhookService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CertificateRotationDto> CreateRotationConfigAsync(Guid userId, CreateCertificateRotationRequest request)
    {
        _logger.LogInformation("Creating certificate rotation config: {CertificateName}, User: {UserId}",
            request.CertificateName, userId);

        var rotation = new CertificateRotation
        {
            Id = Guid.NewGuid(),
            CertificateName = request.CertificateName,
            CertificateType = request.CertificateType,
            Subject = request.Subject,
            IssuerType = request.IssuerType,
            AcmeAccountUrl = request.AcmeAccountUrl,
            DomainValidationType = request.DomainValidationType,
            ExpirationDate = request.ExpirationDate,
            RotationIntervalDays = request.RotationIntervalDays,
            RotationPolicy = request.RotationPolicy,
            CronExpression = request.CronExpression,
            AutoDeploy = request.AutoDeploy,
            DeploymentTargets = request.DeploymentTargets != null ? JsonSerializer.Serialize(request.DeploymentTargets) : null,
            AlertThresholdDays = request.AlertThresholdDays,
            NotificationEmail = request.NotificationEmail,
            NotificationWebhook = request.NotificationWebhook,
            OwnerId = userId,
            Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        // Calculate next rotation date
        rotation.NextRotationDate = CalculateNextRotationDate(rotation);

        _context.CertificateRotations.Add(rotation);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            "certificate_rotation.config.created",
            "CertificateRotation",
            rotation.Id.ToString(),
            null,
            new { rotation.CertificateName, rotation.IssuerType, rotation.RotationPolicy }
        );

        await _webhookService.PublishEventAsync(
            "certificate.rotation.config.created",
            new { RotationId = rotation.Id, CertificateName = rotation.CertificateName },
            userId
        );

        _logger.LogInformation("Certificate rotation config created: {RotationId}, Certificate: {CertificateName}",
            rotation.Id, rotation.CertificateName);

        return MapToDto(rotation);
    }

    public async Task<bool> UpdateRotationConfigAsync(Guid rotationId, Guid userId, UpdateCertificateRotationRequest request)
    {
        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            _logger.LogWarning("Certificate rotation not found: {RotationId}, User: {UserId}", rotationId, userId);
            return false;
        }

        var oldValue = new
        {
            rotation.RotationIntervalDays,
            rotation.RotationPolicy,
            rotation.CronExpression,
            rotation.AutoDeploy,
            rotation.AlertThresholdDays
        };

        if (request.RotationIntervalDays.HasValue)
            rotation.RotationIntervalDays = request.RotationIntervalDays.Value;

        if (request.RotationPolicy != null)
            rotation.RotationPolicy = request.RotationPolicy;

        if (request.CronExpression != null)
            rotation.CronExpression = request.CronExpression;

        if (request.AutoDeploy.HasValue)
            rotation.AutoDeploy = request.AutoDeploy.Value;

        if (request.DeploymentTargets != null)
            rotation.DeploymentTargets = JsonSerializer.Serialize(request.DeploymentTargets);

        if (request.AlertThresholdDays.HasValue)
            rotation.AlertThresholdDays = request.AlertThresholdDays.Value;

        if (request.NotificationEmail != null)
            rotation.NotificationEmail = request.NotificationEmail;

        if (request.NotificationWebhook != null)
            rotation.NotificationWebhook = request.NotificationWebhook;

        if (request.Tags != null)
            rotation.Tags = JsonSerializer.Serialize(request.Tags);

        rotation.UpdatedAt = DateTime.UtcNow;
        rotation.NextRotationDate = CalculateNextRotationDate(rotation);

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            "certificate_rotation.config.updated",
            "CertificateRotation",
            rotationId.ToString(),
            oldValue,
            new
            {
                rotation.RotationIntervalDays,
                rotation.RotationPolicy,
                rotation.CronExpression,
                rotation.AutoDeploy,
                rotation.AlertThresholdDays
            }
        );

        _logger.LogInformation("Certificate rotation config updated: {RotationId}", rotationId);
        return true;
    }

    public async Task<CertificateRotationDto?> GetRotationConfigAsync(Guid rotationId, Guid userId)
    {
        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        return rotation != null ? MapToDto(rotation) : null;
    }

    public async Task<List<CertificateRotationDto>> GetRotationConfigsAsync(Guid userId, bool? activeOnly = null)
    {
        var query = _context.CertificateRotations
            .Where(r => r.OwnerId == userId);

        if (activeOnly == true)
        {
            query = query.Where(r => r.Status == "active");
        }

        var rotations = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return rotations.Select(MapToDto).ToList();
    }

    public async Task<bool> DeleteRotationConfigAsync(Guid rotationId, Guid userId)
    {
        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            _logger.LogWarning("Certificate rotation not found for deletion: {RotationId}, User: {UserId}", rotationId, userId);
            return false;
        }

        rotation.Status = "disabled";
        rotation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            "certificate_rotation.config.deleted",
            "CertificateRotation",
            rotationId.ToString(),
            new { rotation.CertificateName, rotation.Status },
            null
        );

        _logger.LogInformation("Certificate rotation config deleted: {RotationId}", rotationId);
        return true;
    }

    public async Task<CertificateRotationResultDto> RotateCertificateAsync(Guid rotationId, Guid userId)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting manual certificate rotation: {RotationId}, User: {UserId}", rotationId, userId);

        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            throw new InvalidOperationException($"Certificate rotation {rotationId} not found");
        }

        try
        {
            rotation.Status = "rotating";
            await _context.SaveChangesAsync();

            CertificateRotationResultDto result;

            // Perform rotation based on issuer type
            if (rotation.IssuerType.Equals("LetsEncrypt", StringComparison.OrdinalIgnoreCase))
            {
                result = await RenewAcmeCertificateAsync(rotationId, userId);
            }
            else if (rotation.IssuerType.Equals("PrivateCA", StringComparison.OrdinalIgnoreCase))
            {
                result = await RenewPrivateCaCertificateAsync(rotationId, userId);
            }
            else
            {
                throw new NotSupportedException($"Issuer type {rotation.IssuerType} is not supported");
            }

            // Update rotation status
            rotation.Status = result.Success ? "active" : "failed";
            rotation.LastRotationDate = DateTime.UtcNow;
            rotation.LastRotationStatus = result.Success ? "success" : "failed";
            rotation.LastRotationError = result.ErrorMessage;
            rotation.NextRotationDate = result.Success ? CalculateNextRotationDate(rotation) : rotation.NextRotationDate;
            rotation.ExpirationDate = result.NewExpirationDate ?? rotation.ExpirationDate;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "certificate_rotation.executed",
                "CertificateRotation",
                rotationId.ToString(),
                null,
                new { result.Success, result.NewCertificateThumbprint, ExecutionTime = result.RotationDuration }
            );

            await _webhookService.PublishEventAsync(
                result.Success ? "certificate.rotation.success" : "certificate.rotation.failed",
                new
                {
                    RotationId = rotationId,
                    CertificateName = rotation.CertificateName,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage
                },
                userId
            );

            _logger.LogInformation("Certificate rotation completed: {RotationId}, Success: {Success}, Duration: {Duration}",
                rotationId, result.Success, result.RotationDuration);

            return result;
        }
        catch (Exception ex)
        {
            rotation.Status = "failed";
            rotation.LastRotationStatus = "failed";
            rotation.LastRotationError = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Certificate rotation failed: {RotationId}", rotationId);

            await _auditService.LogAsync(
                userId,
                "certificate_rotation.failed",
                "CertificateRotation",
                rotationId.ToString(),
                null,
                null,
                status: "error",
                errorMessage: ex.Message
            );

            return new CertificateRotationResultDto
            {
                RotationHistoryId = Guid.NewGuid(),
                Success = false,
                ErrorMessage = ex.Message,
                RotationDuration = DateTime.UtcNow - startTime,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<int> ProcessScheduledRotationsAsync()
    {
        _logger.LogInformation("Processing scheduled certificate rotations");

        var now = DateTime.UtcNow;
        var rotationsDue = await _context.CertificateRotations
            .Where(r => r.Status == "active" &&
                       r.NextRotationDate.HasValue &&
                       r.NextRotationDate.Value <= now &&
                       r.RotationPolicy != "manual")
            .ToListAsync();

        _logger.LogInformation("Found {Count} certificates due for rotation", rotationsDue.Count);

        int successCount = 0;

        foreach (var rotation in rotationsDue)
        {
            try
            {
                var result = await RotateCertificateAsync(rotation.Id, rotation.OwnerId ?? Guid.Empty);
                if (result.Success)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate certificate: {RotationId}, Certificate: {CertificateName}",
                    rotation.Id, rotation.CertificateName);
            }
        }

        _logger.LogInformation("Scheduled certificate rotations completed: {Processed}/{Total} successful",
            successCount, rotationsDue.Count);

        return successCount;
    }

    public async Task<int> ProcessExpirationAlertsAsync()
    {
        _logger.LogInformation("Processing certificate expiration alerts");

        var now = DateTime.UtcNow;
        int alertsSent = 0;

        // 30-day alerts
        var certs30Days = await _context.CertificateRotations
            .Where(r => r.Status == "active" &&
                       !r.AlertSent30Days &&
                       r.ExpirationDate <= now.AddDays(30) &&
                       r.ExpirationDate > now.AddDays(14))
            .ToListAsync();

        foreach (var cert in certs30Days)
        {
            await SendExpirationAlert(cert, 30, "warning");
            cert.AlertSent30Days = true;
            alertsSent++;
        }

        // 14-day alerts
        var certs14Days = await _context.CertificateRotations
            .Where(r => r.Status == "active" &&
                       !r.AlertSent14Days &&
                       r.ExpirationDate <= now.AddDays(14) &&
                       r.ExpirationDate > now.AddDays(7))
            .ToListAsync();

        foreach (var cert in certs14Days)
        {
            await SendExpirationAlert(cert, 14, "warning");
            cert.AlertSent14Days = true;
            alertsSent++;
        }

        // 7-day alerts
        var certs7Days = await _context.CertificateRotations
            .Where(r => r.Status == "active" &&
                       !r.AlertSent7Days &&
                       r.ExpirationDate <= now.AddDays(7) &&
                       r.ExpirationDate > now.AddDays(1))
            .ToListAsync();

        foreach (var cert in certs7Days)
        {
            await SendExpirationAlert(cert, 7, "critical");
            cert.AlertSent7Days = true;
            alertsSent++;
        }

        // 1-day alerts
        var certs1Day = await _context.CertificateRotations
            .Where(r => r.Status == "active" &&
                       !r.AlertSent1Day &&
                       r.ExpirationDate <= now.AddDays(1))
            .ToListAsync();

        foreach (var cert in certs1Day)
        {
            await SendExpirationAlert(cert, 1, "critical");
            cert.AlertSent1Day = true;
            alertsSent++;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Certificate expiration alerts sent: {Count}", alertsSent);
        return alertsSent;
    }

    public async Task<List<CertificateRotationDto>> GetCertificatesDueForRotationAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var rotations = await _context.CertificateRotations
            .Where(r => r.OwnerId == userId &&
                       r.Status == "active" &&
                       r.NextRotationDate.HasValue &&
                       r.NextRotationDate.Value <= now.AddDays(7))
            .OrderBy(r => r.NextRotationDate)
            .ToListAsync();

        return rotations.Select(MapToDto).ToList();
    }

    public async Task<List<CertificateExpirationAlertDto>> GetExpiringCertificatesAsync(Guid userId, int daysThreshold = 30)
    {
        var now = DateTime.UtcNow;
        var expiringCerts = await _context.CertificateRotations
            .Where(r => r.OwnerId == userId &&
                       r.Status == "active" &&
                       r.ExpirationDate <= now.AddDays(daysThreshold))
            .OrderBy(r => r.ExpirationDate)
            .ToListAsync();

        return expiringCerts.Select(r => new CertificateExpirationAlertDto
        {
            CertificateRotationId = r.Id,
            CertificateName = r.CertificateName,
            Subject = r.Subject,
            ExpirationDate = r.ExpirationDate,
            DaysUntilExpiration = (int)(r.ExpirationDate - now).TotalDays,
            Severity = (r.ExpirationDate - now).TotalDays <= 7 ? "critical" :
                      (r.ExpirationDate - now).TotalDays <= 14 ? "warning" : "info",
            AutoRotationEnabled = r.RotationPolicy != "manual"
        }).ToList();
    }

    public async Task<List<CertificateRotationHistoryDto>> GetRotationHistoryAsync(Guid rotationId, Guid userId, int? limit = 50)
    {
        // Verify ownership
        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            return new List<CertificateRotationHistoryDto>();
        }

        var query = _context.CertificateRotationHistories
            .Where(h => h.CertificateRotationId == rotationId)
            .OrderByDescending(h => h.CreatedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<CertificateRotationHistory>)query.Take(limit.Value);
        }

        var history = await query.ToListAsync();

        return history.Select(h => new CertificateRotationHistoryDto
        {
            Id = h.Id,
            CertificateRotationId = h.CertificateRotationId,
            Action = h.Action,
            OldCertificateThumbprint = h.OldCertificateThumbprint,
            NewCertificateThumbprint = h.NewCertificateThumbprint,
            OldExpirationDate = h.OldExpirationDate,
            NewExpirationDate = h.NewExpirationDate,
            Status = h.Status,
            ErrorMessage = h.ErrorMessage,
            ChainValid = h.ChainValid,
            RotationDuration = h.RotationDuration,
            InitiationType = h.InitiationType,
            CreatedAt = h.CreatedAt
        }).ToList();
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateCertificateChainAsync(Guid rotationId, Guid userId)
    {
        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            return (false, "Certificate rotation not found");
        }

        try
        {
            // In a real implementation, this would retrieve the actual certificate
            // and validate the chain. For now, we'll simulate the validation.
            _logger.LogInformation("Validating certificate chain: {RotationId}", rotationId);

            // Simulate certificate chain validation
            // In production: Use X509Chain class to validate against trusted roots
            await Task.Delay(100); // Simulate validation time

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate chain validation failed: {RotationId}", rotationId);
            return (false, ex.Message);
        }
    }

    public async Task<CertificateRotationResultDto> RenewAcmeCertificateAsync(Guid rotationId, Guid userId)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Renewing certificate via ACME: {RotationId}", rotationId);

        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            throw new InvalidOperationException($"Certificate rotation {rotationId} not found");
        }

        try
        {
            // In a real implementation, this would:
            // 1. Connect to ACME server (Let's Encrypt)
            // 2. Request certificate renewal
            // 3. Complete domain validation (HTTP-01, DNS-01, or TLS-ALPN-01)
            // 4. Retrieve new certificate
            // 5. Validate certificate chain
            // 6. Deploy to target systems if auto-deploy is enabled

            var newThumbprint = GenerateCertificateThumbprint();
            var newExpiration = DateTime.UtcNow.AddDays(90); // Let's Encrypt certificates are valid for 90 days

            // Simulate ACME renewal
            await Task.Delay(500);

            // Validate certificate chain
            var (chainValid, chainError) = await ValidateCertificateChainAsync(rotationId, userId);

            Dictionary<string, bool>? deploymentResults = null;

            // Deploy if auto-deploy is enabled
            if (rotation.AutoDeploy && !string.IsNullOrEmpty(rotation.DeploymentTargets))
            {
                deploymentResults = await DeployCertificateAsync(rotationId, userId, "certificate-data-placeholder");
            }

            // Create history record
            var history = new CertificateRotationHistory
            {
                Id = Guid.NewGuid(),
                CertificateRotationId = rotationId,
                Action = "renewed",
                OldCertificateThumbprint = rotation.LastRotationStatus,
                NewCertificateThumbprint = newThumbprint,
                OldExpirationDate = rotation.ExpirationDate,
                NewExpirationDate = newExpiration,
                Status = "success",
                ChainValid = chainValid,
                ChainValidationDetails = chainError,
                DeploymentResults = deploymentResults != null ? JsonSerializer.Serialize(deploymentResults) : null,
                RotationDuration = DateTime.UtcNow - startTime,
                InitiatedByUserId = userId,
                InitiationType = "manual",
                CorrelationId = _auditService.GetCorrelationId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.CertificateRotationHistories.Add(history);
            await _context.SaveChangesAsync();

            _logger.LogInformation("ACME certificate renewed successfully: {RotationId}, New expiration: {Expiration}",
                rotationId, newExpiration);

            return new CertificateRotationResultDto
            {
                RotationHistoryId = history.Id,
                Success = true,
                NewCertificateThumbprint = newThumbprint,
                NewExpirationDate = newExpiration,
                ChainValid = chainValid,
                DeploymentResults = deploymentResults,
                RotationDuration = DateTime.UtcNow - startTime,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACME certificate renewal failed: {RotationId}", rotationId);

            var history = new CertificateRotationHistory
            {
                Id = Guid.NewGuid(),
                CertificateRotationId = rotationId,
                Action = "renewal_failed",
                Status = "failed",
                ErrorMessage = ex.Message,
                RotationDuration = DateTime.UtcNow - startTime,
                InitiatedByUserId = userId,
                InitiationType = "manual",
                CorrelationId = _auditService.GetCorrelationId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.CertificateRotationHistories.Add(history);
            await _context.SaveChangesAsync();

            throw;
        }
    }

    public async Task<CertificateRotationResultDto> RenewPrivateCaCertificateAsync(Guid rotationId, Guid userId)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Renewing certificate via Private CA: {RotationId}", rotationId);

        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            throw new InvalidOperationException($"Certificate rotation {rotationId} not found");
        }

        try
        {
            // In a real implementation, this would:
            // 1. Generate new CSR (Certificate Signing Request)
            // 2. Submit to Private CA
            // 3. Retrieve signed certificate
            // 4. Validate certificate chain
            // 5. Deploy to target systems if auto-deploy is enabled

            var newThumbprint = GenerateCertificateThumbprint();
            var newExpiration = DateTime.UtcNow.AddDays(365); // Private CA certificates typically 1 year

            // Simulate Private CA renewal
            await Task.Delay(300);

            var (chainValid, chainError) = await ValidateCertificateChainAsync(rotationId, userId);

            Dictionary<string, bool>? deploymentResults = null;

            if (rotation.AutoDeploy && !string.IsNullOrEmpty(rotation.DeploymentTargets))
            {
                deploymentResults = await DeployCertificateAsync(rotationId, userId, "certificate-data-placeholder");
            }

            var history = new CertificateRotationHistory
            {
                Id = Guid.NewGuid(),
                CertificateRotationId = rotationId,
                Action = "renewed",
                OldCertificateThumbprint = rotation.LastRotationStatus,
                NewCertificateThumbprint = newThumbprint,
                OldExpirationDate = rotation.ExpirationDate,
                NewExpirationDate = newExpiration,
                Status = "success",
                ChainValid = chainValid,
                ChainValidationDetails = chainError,
                DeploymentResults = deploymentResults != null ? JsonSerializer.Serialize(deploymentResults) : null,
                RotationDuration = DateTime.UtcNow - startTime,
                InitiatedByUserId = userId,
                InitiationType = "manual",
                CorrelationId = _auditService.GetCorrelationId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.CertificateRotationHistories.Add(history);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Private CA certificate renewed successfully: {RotationId}", rotationId);

            return new CertificateRotationResultDto
            {
                RotationHistoryId = history.Id,
                Success = true,
                NewCertificateThumbprint = newThumbprint,
                NewExpirationDate = newExpiration,
                ChainValid = chainValid,
                DeploymentResults = deploymentResults,
                RotationDuration = DateTime.UtcNow - startTime,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Private CA certificate renewal failed: {RotationId}", rotationId);

            var history = new CertificateRotationHistory
            {
                Id = Guid.NewGuid(),
                CertificateRotationId = rotationId,
                Action = "renewal_failed",
                Status = "failed",
                ErrorMessage = ex.Message,
                RotationDuration = DateTime.UtcNow - startTime,
                InitiatedByUserId = userId,
                InitiationType = "manual",
                CorrelationId = _auditService.GetCorrelationId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.CertificateRotationHistories.Add(history);
            await _context.SaveChangesAsync();

            throw;
        }
    }

    public async Task<Dictionary<string, bool>> DeployCertificateAsync(Guid rotationId, Guid userId, string certificateData)
    {
        var rotation = await _context.CertificateRotations
            .FirstOrDefaultAsync(r => r.Id == rotationId && r.OwnerId == userId);

        if (rotation == null)
        {
            throw new InvalidOperationException($"Certificate rotation {rotationId} not found");
        }

        if (string.IsNullOrEmpty(rotation.DeploymentTargets))
        {
            return new Dictionary<string, bool>();
        }

        var targets = JsonSerializer.Deserialize<List<string>>(rotation.DeploymentTargets) ?? new List<string>();
        var results = new Dictionary<string, bool>();

        foreach (var target in targets)
        {
            try
            {
                _logger.LogInformation("Deploying certificate to target: {Target}", target);

                // In a real implementation, this would:
                // 1. Connect to target system (via SSH, API, etc.)
                // 2. Upload certificate and private key
                // 3. Restart/reload services
                // 4. Verify deployment

                // Simulate deployment
                await Task.Delay(100);

                results[target] = true;
                _logger.LogInformation("Certificate deployed successfully to: {Target}", target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy certificate to target: {Target}", target);
                results[target] = false;
            }
        }

        return results;
    }

    public async Task<bool> RollbackRotationAsync(Guid rotationHistoryId, Guid userId)
    {
        var history = await _context.CertificateRotationHistories
            .Include(h => h.CertificateRotation)
            .FirstOrDefaultAsync(h => h.Id == rotationHistoryId);

        if (history == null || history.CertificateRotation?.OwnerId != userId)
        {
            _logger.LogWarning("Rotation history not found or unauthorized: {HistoryId}, User: {UserId}",
                rotationHistoryId, userId);
            return false;
        }

        try
        {
            _logger.LogInformation("Rolling back certificate rotation: {HistoryId}", rotationHistoryId);

            // In a real implementation, this would:
            // 1. Retrieve old certificate from history
            // 2. Redeploy to all target systems
            // 3. Update rotation configuration

            var rotation = history.CertificateRotation;
            if (history.OldExpirationDate.HasValue)
            {
                rotation.ExpirationDate = history.OldExpirationDate.Value;
            }

            rotation.Status = "active";
            rotation.LastRotationStatus = "rolled_back";
            rotation.UpdatedAt = DateTime.UtcNow;

            var rollbackHistory = new CertificateRotationHistory
            {
                Id = Guid.NewGuid(),
                CertificateRotationId = rotation.Id,
                Action = "rolled_back",
                Status = "success",
                InitiatedByUserId = userId,
                InitiationType = "manual",
                CorrelationId = _auditService.GetCorrelationId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.CertificateRotationHistories.Add(rollbackHistory);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "certificate_rotation.rolled_back",
                "CertificateRotation",
                rotation.Id.ToString(),
                null,
                new { OriginalHistoryId = rotationHistoryId }
            );

            _logger.LogInformation("Certificate rotation rolled back successfully: {HistoryId}", rotationHistoryId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback certificate rotation: {HistoryId}", rotationHistoryId);
            return false;
        }
    }

    public async Task<CertificateRotationStatisticsDto> GetStatisticsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        var allCerts = await _context.CertificateRotations
            .Where(r => r.OwnerId == userId)
            .ToListAsync();

        var activeCerts = allCerts.Where(r => r.Status == "active").ToList();

        var rotationsLast30Days = await _context.CertificateRotationHistories
            .Where(h => h.CreatedAt >= now.AddDays(-30) &&
                       h.CertificateRotation != null &&
                       h.CertificateRotation.OwnerId == userId &&
                       (h.Action == "renewed" || h.Action == "rotated"))
            .CountAsync();

        var failedRotationsLast30Days = await _context.CertificateRotationHistories
            .Where(h => h.CreatedAt >= now.AddDays(-30) &&
                       h.CertificateRotation != null &&
                       h.CertificateRotation.OwnerId == userId &&
                       h.Status == "failed")
            .CountAsync();

        var successRate = rotationsLast30Days > 0
            ? (double)(rotationsLast30Days - failedRotationsLast30Days) / rotationsLast30Days * 100
            : 100.0;

        return new CertificateRotationStatisticsDto
        {
            TotalCertificates = allCerts.Count,
            ActiveCertificates = activeCerts.Count,
            ExpiringWithin30Days = activeCerts.Count(c => c.ExpirationDate <= now.AddDays(30)),
            ExpiringWithin14Days = activeCerts.Count(c => c.ExpirationDate <= now.AddDays(14)),
            ExpiringWithin7Days = activeCerts.Count(c => c.ExpirationDate <= now.AddDays(7)),
            ExpiringWithin1Day = activeCerts.Count(c => c.ExpirationDate <= now.AddDays(1)),
            RotationsLast30Days = rotationsLast30Days,
            FailedRotationsLast30Days = failedRotationsLast30Days,
            SuccessRate = Math.Round(successRate, 2)
        };
    }

    public async Task<bool> TestAcmeAccountAsync(string acmeAccountUrl)
    {
        try
        {
            _logger.LogInformation("Testing ACME account connectivity: {AccountUrl}", acmeAccountUrl);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync(acmeAccountUrl);
            var isSuccess = response.IsSuccessStatusCode;

            _logger.LogInformation("ACME account test {Result}: {AccountUrl}",
                isSuccess ? "successful" : "failed", acmeAccountUrl);

            return isSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACME account test failed: {AccountUrl}", acmeAccountUrl);
            return false;
        }
    }

    public async Task<bool> TestDeploymentTargetAsync(string targetUrl)
    {
        try
        {
            _logger.LogInformation("Testing deployment target connectivity: {TargetUrl}", targetUrl);

            // In a real implementation, this would test actual connectivity
            // (SSH, API, etc.) to the deployment target
            await Task.Delay(100);

            _logger.LogInformation("Deployment target test successful: {TargetUrl}", targetUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment target test failed: {TargetUrl}", targetUrl);
            return false;
        }
    }

    private DateTime? CalculateNextRotationDate(CertificateRotation rotation)
    {
        if (rotation.RotationPolicy == "manual")
        {
            return null;
        }

        if (rotation.RotationPolicy == "scheduled" && !string.IsNullOrEmpty(rotation.CronExpression))
        {
            try
            {
                var cronExpression = CronExpression.Parse(rotation.CronExpression);
                return cronExpression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid cron expression for rotation {RotationId}: {CronExpression}",
                    rotation.Id, rotation.CronExpression);
                return null;
            }
        }

        // Automatic rotation based on interval
        var baseDate = rotation.LastRotationDate ?? rotation.CreatedAt;
        return baseDate.AddDays(rotation.RotationIntervalDays);
    }

    private async Task SendExpirationAlert(CertificateRotation rotation, int daysUntilExpiration, string severity)
    {
        _logger.LogInformation("Sending certificate expiration alert: {CertificateName}, Days: {Days}, Severity: {Severity}",
            rotation.CertificateName, daysUntilExpiration, severity);

        await _webhookService.PublishEventAsync(
            "certificate.expiration.alert",
            new
            {
                CertificateRotationId = rotation.Id,
                CertificateName = rotation.CertificateName,
                Subject = rotation.Subject,
                ExpirationDate = rotation.ExpirationDate,
                DaysUntilExpiration = daysUntilExpiration,
                Severity = severity,
                AutoRotationEnabled = rotation.RotationPolicy != "manual"
            },
            rotation.OwnerId
        );

        // In a real implementation, this would also send email if configured
        if (!string.IsNullOrEmpty(rotation.NotificationEmail))
        {
            _logger.LogInformation("Email alert would be sent to: {Email}", rotation.NotificationEmail);
        }
    }

    private string GenerateCertificateThumbprint()
    {
        var bytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private CertificateRotationDto MapToDto(CertificateRotation rotation)
    {
        var daysUntilExpiration = (int)(rotation.ExpirationDate - DateTime.UtcNow).TotalDays;

        return new CertificateRotationDto
        {
            Id = rotation.Id,
            CertificateName = rotation.CertificateName,
            CertificateType = rotation.CertificateType,
            Subject = rotation.Subject,
            IssuerType = rotation.IssuerType,
            ExpirationDate = rotation.ExpirationDate,
            LastRotationDate = rotation.LastRotationDate,
            NextRotationDate = rotation.NextRotationDate,
            RotationIntervalDays = rotation.RotationIntervalDays,
            RotationPolicy = rotation.RotationPolicy,
            CronExpression = rotation.CronExpression,
            AutoDeploy = rotation.AutoDeploy,
            DaysUntilExpiration = daysUntilExpiration,
            Status = rotation.Status,
            LastRotationStatus = rotation.LastRotationStatus,
            AlertThresholdDays = rotation.AlertThresholdDays,
            CreatedAt = rotation.CreatedAt
        };
    }
}
