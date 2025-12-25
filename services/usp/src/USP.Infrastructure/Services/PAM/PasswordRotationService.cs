using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.Cryptography;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.PAM.Connectors;

namespace USP.Infrastructure.Services.PAM;

public class PasswordRotationService : IPasswordRotationService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ISafeManagementService _safeService;
    private readonly IAuditService _auditService;
    private readonly ILogger<PasswordRotationService> _logger;
    private readonly Dictionary<string, ITargetSystemConnector> _connectors;

    public PasswordRotationService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ISafeManagementService safeService,
        IAuditService auditService,
        ILogger<PasswordRotationService> logger,
        ILogger<PostgreSqlConnector> postgresLogger,
        ILogger<MySqlPasswordConnector> mysqlLogger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _safeService = safeService;
        _auditService = auditService;
        _logger = logger;

        // Initialize connectors
        _connectors = new Dictionary<string, ITargetSystemConnector>(StringComparer.OrdinalIgnoreCase)
        {
            { "PostgreSQL", new PostgreSqlConnector(postgresLogger) },
            { "MySQL", new MySqlPasswordConnector(mysqlLogger) }
        };
    }

    public async Task<PasswordRotationResultDto> RotatePasswordAsync(Guid accountId, Guid userId)
    {
        var result = new PasswordRotationResultDto
        {
            Success = false,
            Message = "Password rotation failed"
        };

        try
        {
            // Get account with safe
            var account = await _context.PrivilegedAccounts
                .Include(a => a.Safe)
                .FirstOrDefaultAsync(a => a.Id == accountId);

            if (account == null)
            {
                result.ErrorMessage = "Account not found";
                return result;
            }

            // Check if user has manage access to the safe
            var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "manage");
            if (!hasAccess)
            {
                result.ErrorMessage = "Insufficient permissions to rotate password";
                return result;
            }

            // Check if connector exists for platform
            if (!_connectors.TryGetValue(account.Platform, out var connector))
            {
                result.ErrorMessage = $"No connector available for platform: {account.Platform}";
                return result;
            }

            // Decrypt current password
            var currentPassword = _encryptionService.Decrypt(account.EncryptedPassword);

            // Generate new password
            var newPassword = connector.GeneratePassword();

            // Rotate password on target system
            var rotationResult = await connector.RotatePasswordAsync(
                account.HostAddress ?? string.Empty,
                account.Port,
                account.Username,
                currentPassword,
                newPassword,
                account.DatabaseName,
                account.ConnectionDetails);

            if (!rotationResult.Success)
            {
                result.ErrorMessage = rotationResult.ErrorMessage;
                result.Message = rotationResult.Details ?? "Password rotation failed on target system";

                // Log failed rotation
                await LogRotationAsync(accountId, userId, false, rotationResult.ErrorMessage, false);

                return result;
            }

            // Verify new credentials
            var credentialsVerified = await connector.VerifyCredentialsAsync(
                account.HostAddress ?? string.Empty,
                account.Port,
                account.Username,
                newPassword,
                account.DatabaseName,
                account.ConnectionDetails);

            if (!credentialsVerified)
            {
                _logger.LogWarning(
                    "Password rotation completed but verification failed for account {AccountId}",
                    accountId);
            }

            // Update encrypted password in database
            account.EncryptedPassword = _encryptionService.Encrypt(newPassword);
            account.LastRotated = DateTime.UtcNow;
            account.NextRotation = CalculateNextRotation(account.RotationPolicy, account.RotationIntervalDays);

            await _context.SaveChangesAsync();

            // Log successful rotation
            await LogRotationAsync(accountId, userId, true, null, credentialsVerified);

            // Audit log
            await _auditService.LogAsync(
                userId,
                "password_rotated",
                "PrivilegedAccount",
                accountId.ToString(),
                null,
                new
                {
                    platform = account.Platform,
                    verified = credentialsVerified,
                    rotatedAt = rotationResult.RotatedAt
                });

            result.Success = true;
            result.Message = "Password rotated successfully";
            result.RotatedAt = rotationResult.RotatedAt;
            result.CredentialsVerified = credentialsVerified;

            _logger.LogInformation(
                "Password rotated successfully for account {AccountId} ({Platform})",
                accountId,
                account.Platform);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Message = "An error occurred during password rotation";

            _logger.LogError(ex,
                "Error rotating password for account {AccountId}",
                accountId);
        }

        return result;
    }

    public async Task<bool> VerifyCredentialsAsync(Guid accountId)
    {
        try
        {
            var account = await _context.PrivilegedAccounts.FindAsync(accountId);
            if (account == null)
                return false;

            if (!_connectors.TryGetValue(account.Platform, out var connector))
                return false;

            var password = _encryptionService.Decrypt(account.EncryptedPassword);

            return await connector.VerifyCredentialsAsync(
                account.HostAddress ?? string.Empty,
                account.Port,
                account.Username,
                password,
                account.DatabaseName,
                account.ConnectionDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error verifying credentials for account {AccountId}",
                accountId);

            return false;
        }
    }

    public async Task<List<PasswordRotationHistoryDto>> GetRotationHistoryAsync(Guid accountId, Guid userId, int? limit = 50)
    {
        // Get account to check access
        var account = await _context.PrivilegedAccounts.FindAsync(accountId);
        if (account == null)
            return new List<PasswordRotationHistoryDto>();

        // Check if user has read access to the safe
        var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "read");
        if (!hasAccess)
            return new List<PasswordRotationHistoryDto>();

        // For now, return empty list as we need to create PasswordRotationHistory entity
        // This would query the rotation history table
        return new List<PasswordRotationHistoryDto>();
    }

    public async Task<int> ProcessScheduledRotationsAsync()
    {
        var now = DateTime.UtcNow;

        // Get accounts due for scheduled rotation
        var accountsDue = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .Where(a => a.RotationPolicy == "scheduled" &&
                       a.NextRotation.HasValue &&
                       a.NextRotation.Value <= now)
            .ToListAsync();

        if (accountsDue.Count == 0)
            return 0;

        int successCount = 0;

        foreach (var account in accountsDue)
        {
            try
            {
                // Use system user (Guid.Empty) for automated rotations
                var result = await RotatePasswordAsync(account.Id, Guid.Empty);

                if (result.Success)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing scheduled rotation for account {AccountId}",
                    account.Id);
            }
        }

        _logger.LogInformation(
            "Processed {Total} scheduled rotations, {Success} successful",
            accountsDue.Count,
            successCount);

        return successCount;
    }

    public async Task<List<AccountDueForRotationDto>> GetAccountsDueForRotationAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Get all safes accessible by user
        var safes = await _safeService.GetSafesAsync(userId);
        var safeIds = safes.Select(s => s.Id).ToList();

        // Get accounts due for rotation
        var accounts = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .Where(a => safeIds.Contains(a.SafeId) &&
                       a.NextRotation.HasValue &&
                       a.NextRotation.Value <= now)
            .ToListAsync();

        return accounts.Select(a => new AccountDueForRotationDto
        {
            AccountId = a.Id,
            AccountName = a.AccountName,
            Platform = a.Platform,
            SafeName = a.Safe.Name,
            LastRotation = a.LastRotated,
            NextRotation = a.NextRotation,
            DaysOverdue = a.NextRotation.HasValue
                ? Math.Max(0, (int)(now - a.NextRotation.Value).TotalDays)
                : 0,
            RotationPolicy = a.RotationPolicy
        }).ToList();
    }

    public async Task<bool> UpdateRotationPolicyAsync(
        Guid accountId,
        Guid userId,
        string rotationPolicy,
        int rotationIntervalDays)
    {
        var account = await _context.PrivilegedAccounts.FindAsync(accountId);
        if (account == null)
            return false;

        // Check if user has manage access to the safe
        var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "manage");
        if (!hasAccess)
            return false;

        // Validate rotation policy
        var validPolicies = new[] { "manual", "on_checkout", "scheduled", "on_expiration" };
        if (!validPolicies.Contains(rotationPolicy))
            return false;

        account.RotationPolicy = rotationPolicy;
        account.RotationIntervalDays = rotationIntervalDays;

        if (rotationPolicy == "scheduled")
        {
            account.NextRotation = CalculateNextRotation(rotationPolicy, rotationIntervalDays);
        }
        else
        {
            account.NextRotation = null;
        }

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "rotation_policy_updated",
            "PrivilegedAccount",
            accountId.ToString(),
            null,
            new
            {
                rotationPolicy,
                rotationIntervalDays,
                nextRotation = account.NextRotation
            });

        _logger.LogInformation(
            "Rotation policy updated for account {AccountId}: {Policy}, {Interval} days",
            accountId,
            rotationPolicy,
            rotationIntervalDays);

        return true;
    }

    public async Task<RotationStatisticsDto> GetRotationStatisticsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Get all safes accessible by user
        var safes = await _safeService.GetSafesAsync(userId);
        var safeIds = safes.Select(s => s.Id).ToList();

        // Get all accounts
        var accounts = await _context.PrivilegedAccounts
            .Where(a => safeIds.Contains(a.SafeId))
            .ToListAsync();

        var totalAccounts = accounts.Count;
        var accountsDue = accounts.Count(a => a.NextRotation.HasValue && a.NextRotation.Value <= now);
        var accountsOverdue = accounts.Count(a => a.NextRotation.HasValue && a.NextRotation.Value < now.AddDays(-7));

        // For rotation counts, we would need to query the rotation history table
        // For now, return placeholder values
        var rotationsByPlatform = accounts
            .GroupBy(a => a.Platform)
            .Select(g => new RotationByPlatformDto
            {
                Platform = g.Key,
                Count = g.Count(),
                SuccessCount = 0,
                FailureCount = 0
            })
            .ToList();

        return new RotationStatisticsDto
        {
            TotalAccounts = totalAccounts,
            AccountsDueForRotation = accountsDue,
            AccountsOverdue = accountsOverdue,
            RotationsLast24Hours = 0,
            RotationsLast7Days = 0,
            RotationsLast30Days = 0,
            SuccessfulRotations = 0,
            FailedRotations = 0,
            SuccessRate = 0,
            RotationsByPlatform = rotationsByPlatform,
            RecentRotations = new List<RecentRotationDto>()
        };
    }

    private DateTime? CalculateNextRotation(string rotationPolicy, int intervalDays)
    {
        if (rotationPolicy != "scheduled")
            return null;

        return DateTime.UtcNow.AddDays(intervalDays);
    }

    private async Task LogRotationAsync(
        Guid accountId,
        Guid userId,
        bool success,
        string? errorMessage,
        bool credentialsVerified)
    {
        // This would create a PasswordRotationHistory record
        // For now, just log it
        _logger.LogInformation(
            "Password rotation logged: Account={AccountId}, User={UserId}, Success={Success}, Verified={Verified}",
            accountId,
            userId,
            success,
            credentialsVerified);
    }
}
