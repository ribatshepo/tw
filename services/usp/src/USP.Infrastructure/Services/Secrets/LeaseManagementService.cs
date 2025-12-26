using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using USP.Core.Models.DTOs.Secrets;
using USP.Core.Models.Entities;
using USP.Core.Services.Secrets;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// Service for managing time-bound leases for secret access
/// </summary>
public class LeaseManagementService : ILeaseManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LeaseManagementService> _logger;

    public LeaseManagementService(
        ApplicationDbContext context,
        ILogger<LeaseManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LeaseDto> CreateLeaseAsync(
        Guid secretId,
        Guid userId,
        int leaseDurationSeconds,
        bool autoRenewalEnabled = false,
        int? maxRenewals = null)
    {
        // Validate secret exists
        var secret = await _context.Secrets
            .FirstOrDefaultAsync(s => s.Id == secretId);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret {secretId} not found");
        }

        // Validate user exists
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        // Validate lease duration
        if (leaseDurationSeconds < 60)
        {
            throw new ArgumentException("Lease duration must be at least 60 seconds", nameof(leaseDurationSeconds));
        }

        if (leaseDurationSeconds > 86400)
        {
            throw new ArgumentException("Lease duration cannot exceed 24 hours (86400 seconds)", nameof(leaseDurationSeconds));
        }

        // Create lease
        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(leaseDurationSeconds),
            LeaseDurationSeconds = leaseDurationSeconds,
            AutoRenewalEnabled = autoRenewalEnabled,
            MaxRenewals = maxRenewals,
            Status = "active",
            RenewalCount = 0
        };

        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created lease {LeaseId} for secret {SecretId} by user {UserId}, expires at {ExpiresAt}",
            lease.Id, secretId, userId, lease.ExpiresAt);

        return await MapToDto(lease);
    }

    public async Task<LeaseDto> RenewLeaseAsync(Guid leaseId, Guid userId, int? incrementSeconds = null)
    {
        var lease = await _context.Leases
            .Include(l => l.Secret)
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null)
        {
            throw new InvalidOperationException($"Lease {leaseId} not found");
        }

        // Only the lease owner can renew
        if (lease.UserId != userId)
        {
            throw new UnauthorizedAccessException("Only the lease owner can renew the lease");
        }

        // Check if lease is still active
        if (lease.Status != "active")
        {
            throw new InvalidOperationException($"Cannot renew lease with status {lease.Status}");
        }

        // Check if max renewals reached
        if (lease.MaxRenewals.HasValue && lease.RenewalCount >= lease.MaxRenewals.Value)
        {
            throw new InvalidOperationException($"Maximum renewals ({lease.MaxRenewals.Value}) reached for this lease");
        }

        // Calculate new expiration
        var previousExpiresAt = lease.ExpiresAt;
        var increment = incrementSeconds ?? lease.LeaseDurationSeconds;
        var newExpiresAt = DateTime.UtcNow.AddSeconds(increment);

        // Create renewal history entry
        var renewalHistory = new LeaseRenewalHistory
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            RenewedAt = DateTime.UtcNow,
            PreviousExpiresAt = previousExpiresAt,
            NewExpiresAt = newExpiresAt,
            RenewalCount = lease.RenewalCount + 1,
            Success = true,
            RenewedBy = userId,
            IsAutoRenewal = false
        };

        // Update lease
        lease.ExpiresAt = newExpiresAt;
        lease.RenewalCount++;
        lease.LastRenewedAt = DateTime.UtcNow;

        _context.LeaseRenewalHistories.Add(renewalHistory);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Renewed lease {LeaseId}, new expiration: {ExpiresAt}, renewal count: {RenewalCount}",
            leaseId, newExpiresAt, lease.RenewalCount);

        return await MapToDto(lease);
    }

    public async Task RevokeLeaseAsync(Guid leaseId, Guid userId, string? reason = null)
    {
        var lease = await _context.Leases
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null)
        {
            throw new InvalidOperationException($"Lease {leaseId} not found");
        }

        // Only the lease owner can revoke
        if (lease.UserId != userId)
        {
            throw new UnauthorizedAccessException("Only the lease owner can revoke the lease");
        }

        // Check if already revoked or expired
        if (lease.Status != "active")
        {
            throw new InvalidOperationException($"Cannot revoke lease with status {lease.Status}");
        }

        lease.Status = "revoked";
        lease.RevokedAt = DateTime.UtcNow;
        lease.RevokedBy = userId;
        lease.RevocationReason = reason;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Revoked lease {LeaseId} by user {UserId}, reason: {Reason}",
            leaseId, userId, reason ?? "none");
    }

    public async Task<LeaseDto> GetLeaseAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases
            .Include(l => l.Secret)
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null)
        {
            throw new InvalidOperationException($"Lease {leaseId} not found");
        }

        // Only the lease owner can view
        if (lease.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this lease");
        }

        return await MapToDto(lease);
    }

    public async Task<List<LeaseDto>> GetUserLeasesAsync(Guid userId, bool includeExpired = false)
    {
        var query = _context.Leases
            .Include(l => l.Secret)
            .Include(l => l.User)
            .Where(l => l.UserId == userId);

        if (!includeExpired)
        {
            query = query.Where(l => l.Status == "active" && l.ExpiresAt > DateTime.UtcNow);
        }

        var leases = await query
            .OrderByDescending(l => l.IssuedAt)
            .ToListAsync();

        var result = new List<LeaseDto>();
        foreach (var lease in leases)
        {
            result.Add(await MapToDto(lease));
        }

        return result;
    }

    public async Task<List<LeaseDto>> GetSecretLeasesAsync(Guid secretId, Guid userId, bool includeExpired = false)
    {
        // Verify user has access to the secret
        var secret = await _context.Secrets
            .FirstOrDefaultAsync(s => s.Id == secretId);

        if (secret == null)
        {
            throw new InvalidOperationException($"Secret {secretId} not found");
        }

        var query = _context.Leases
            .Include(l => l.Secret)
            .Include(l => l.User)
            .Where(l => l.SecretId == secretId);

        if (!includeExpired)
        {
            query = query.Where(l => l.Status == "active" && l.ExpiresAt > DateTime.UtcNow);
        }

        var leases = await query
            .OrderByDescending(l => l.IssuedAt)
            .ToListAsync();

        var result = new List<LeaseDto>();
        foreach (var lease in leases)
        {
            result.Add(await MapToDto(lease));
        }

        return result;
    }

    public async Task<List<LeaseRenewalHistoryDto>> GetLeaseRenewalHistoryAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null)
        {
            throw new InvalidOperationException($"Lease {leaseId} not found");
        }

        // Only the lease owner can view history
        if (lease.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this lease history");
        }

        var history = await _context.LeaseRenewalHistories
            .Where(h => h.LeaseId == leaseId)
            .OrderByDescending(h => h.RenewedAt)
            .ToListAsync();

        return history.Select(h => new LeaseRenewalHistoryDto
        {
            Id = h.Id,
            LeaseId = h.LeaseId,
            RenewedAt = h.RenewedAt,
            PreviousExpiresAt = h.PreviousExpiresAt,
            NewExpiresAt = h.NewExpiresAt,
            RenewalCount = h.RenewalCount,
            Success = h.Success,
            ErrorMessage = h.ErrorMessage,
            RenewedBy = h.RenewedBy,
            IsAutoRenewal = h.IsAutoRenewal
        }).ToList();
    }

    public async Task HandleExpiringLeasesAsync()
    {
        var now = DateTime.UtcNow;

        // Find all leases that have expired but are still marked as active
        var expiredLeases = await _context.Leases
            .Where(l => l.Status == "active" && l.ExpiresAt <= now)
            .ToListAsync();

        foreach (var lease in expiredLeases)
        {
            lease.Status = "expired";
            _logger.LogInformation("Marked lease {LeaseId} as expired (expired at {ExpiresAt})", lease.Id, lease.ExpiresAt);
        }

        if (expiredLeases.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Processed {Count} expired leases", expiredLeases.Count);
        }
    }

    public async Task ProcessAutoRenewalsAsync()
    {
        var now = DateTime.UtcNow;
        var renewalThreshold = now.AddMinutes(10); // Renew leases expiring in the next 10 minutes

        // Find leases eligible for auto-renewal
        var leasesToRenew = await _context.Leases
            .Where(l => l.Status == "active" &&
                       l.AutoRenewalEnabled &&
                       l.ExpiresAt <= renewalThreshold &&
                       l.ExpiresAt > now)
            .ToListAsync();

        foreach (var lease in leasesToRenew)
        {
            try
            {
                // Check if max renewals reached
                if (lease.MaxRenewals.HasValue && lease.RenewalCount >= lease.MaxRenewals.Value)
                {
                    _logger.LogWarning(
                        "Lease {LeaseId} cannot be auto-renewed: max renewals ({MaxRenewals}) reached",
                        lease.Id, lease.MaxRenewals.Value);

                    // Create failed renewal history entry
                    var failedHistory = new LeaseRenewalHistory
                    {
                        Id = Guid.NewGuid(),
                        LeaseId = lease.Id,
                        RenewedAt = now,
                        PreviousExpiresAt = lease.ExpiresAt,
                        NewExpiresAt = lease.ExpiresAt,
                        RenewalCount = lease.RenewalCount,
                        Success = false,
                        ErrorMessage = $"Maximum renewals ({lease.MaxRenewals.Value}) reached",
                        IsAutoRenewal = true
                    };
                    _context.LeaseRenewalHistories.Add(failedHistory);

                    // Disable auto-renewal to prevent repeated attempts
                    lease.AutoRenewalEnabled = false;
                    continue;
                }

                // Perform renewal
                var previousExpiresAt = lease.ExpiresAt;
                var newExpiresAt = now.AddSeconds(lease.LeaseDurationSeconds);

                var renewalHistory = new LeaseRenewalHistory
                {
                    Id = Guid.NewGuid(),
                    LeaseId = lease.Id,
                    RenewedAt = now,
                    PreviousExpiresAt = previousExpiresAt,
                    NewExpiresAt = newExpiresAt,
                    RenewalCount = lease.RenewalCount + 1,
                    Success = true,
                    IsAutoRenewal = true
                };

                lease.ExpiresAt = newExpiresAt;
                lease.RenewalCount++;
                lease.LastRenewedAt = now;

                _context.LeaseRenewalHistories.Add(renewalHistory);

                _logger.LogInformation(
                    "Auto-renewed lease {LeaseId}, new expiration: {ExpiresAt}, renewal count: {RenewalCount}",
                    lease.Id, newExpiresAt, lease.RenewalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-renew lease {LeaseId}", lease.Id);

                // Create failed renewal history entry
                var failedHistory = new LeaseRenewalHistory
                {
                    Id = Guid.NewGuid(),
                    LeaseId = lease.Id,
                    RenewedAt = now,
                    PreviousExpiresAt = lease.ExpiresAt,
                    NewExpiresAt = lease.ExpiresAt,
                    RenewalCount = lease.RenewalCount,
                    Success = false,
                    ErrorMessage = ex.Message,
                    IsAutoRenewal = true
                };
                _context.LeaseRenewalHistories.Add(failedHistory);
            }
        }

        if (leasesToRenew.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Processed {Count} leases for auto-renewal", leasesToRenew.Count);
        }
    }

    public async Task RevokeAllSecretLeasesAsync(Guid secretId, Guid userId, string? reason = null)
    {
        var leases = await _context.Leases
            .Where(l => l.SecretId == secretId && l.Status == "active")
            .ToListAsync();

        foreach (var lease in leases)
        {
            lease.Status = "revoked";
            lease.RevokedAt = DateTime.UtcNow;
            lease.RevokedBy = userId;
            lease.RevocationReason = reason ?? "Secret deleted or rotated";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Revoked {Count} leases for secret {SecretId} by user {UserId}",
            leases.Count, secretId, userId);
    }

    public async Task<LeaseStatisticsDto> GetLeaseStatisticsAsync(Guid? userId = null)
    {
        var query = _context.Leases.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(l => l.UserId == userId.Value);
        }

        var now = DateTime.UtcNow;

        var statistics = new LeaseStatisticsDto
        {
            TotalLeases = await query.CountAsync(),
            ActiveLeases = await query.CountAsync(l => l.Status == "active" && l.ExpiresAt > now),
            ExpiredLeases = await query.CountAsync(l => l.Status == "expired"),
            RevokedLeases = await query.CountAsync(l => l.Status == "revoked"),
            AutoRenewalEnabledCount = await query.CountAsync(l => l.AutoRenewalEnabled && l.Status == "active"),
            TotalRenewals = await query.SumAsync(l => l.RenewalCount),
            LeasesExpiringIn24Hours = await query.CountAsync(l =>
                l.Status == "active" &&
                l.ExpiresAt > now &&
                l.ExpiresAt <= now.AddHours(24)),
            LeasesExpiringIn1Hour = await query.CountAsync(l =>
                l.Status == "active" &&
                l.ExpiresAt > now &&
                l.ExpiresAt <= now.AddHours(1))
        };

        var allLeases = await query.ToListAsync();
        if (allLeases.Any())
        {
            statistics.AverageLeaseDurationSeconds = allLeases.Average(l => l.LeaseDurationSeconds);
            statistics.AverageRenewalCount = allLeases.Average(l => l.RenewalCount);
        }

        var activeLeases = allLeases.Where(l => l.Status == "active" && l.ExpiresAt > now).ToList();
        if (activeLeases.Any())
        {
            statistics.OldestActiveLease = activeLeases.Min(l => l.IssuedAt);
        }

        if (allLeases.Any())
        {
            statistics.NewestLease = allLeases.Max(l => l.IssuedAt);
        }

        statistics.LeasesByStatus = allLeases
            .GroupBy(l => l.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return statistics;
    }

    private async Task<LeaseDto> MapToDto(Lease lease)
    {
        var now = DateTime.UtcNow;
        var remainingSeconds = (int)(lease.ExpiresAt - now).TotalSeconds;
        var isExpired = lease.ExpiresAt <= now || lease.Status != "active";

        var canRenew = lease.Status == "active" &&
                      !isExpired &&
                      (!lease.MaxRenewals.HasValue || lease.RenewalCount < lease.MaxRenewals.Value);

        Dictionary<string, string>? metadata = null;
        if (!string.IsNullOrEmpty(lease.Metadata))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(lease.Metadata);
            }
            catch
            {
                // Ignore invalid metadata
            }
        }

        return new LeaseDto
        {
            LeaseId = lease.Id,
            SecretId = lease.SecretId,
            SecretPath = lease.Secret?.Path ?? string.Empty,
            UserId = lease.UserId,
            UserEmail = lease.User?.Email ?? string.Empty,
            IssuedAt = lease.IssuedAt,
            ExpiresAt = lease.ExpiresAt,
            RenewalCount = lease.RenewalCount,
            AutoRenewalEnabled = lease.AutoRenewalEnabled,
            Status = lease.Status,
            MaxRenewals = lease.MaxRenewals,
            LastRenewedAt = lease.LastRenewedAt,
            RevokedAt = lease.RevokedAt,
            RevocationReason = lease.RevocationReason,
            LeaseDurationSeconds = lease.LeaseDurationSeconds,
            RemainingSeconds = Math.Max(0, remainingSeconds),
            IsExpired = isExpired,
            CanRenew = canRenew,
            Metadata = metadata
        };
    }
}
