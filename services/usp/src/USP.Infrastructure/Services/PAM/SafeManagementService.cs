using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

public class SafeManagementService : ISafeManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SafeManagementService> _logger;
    private readonly IEncryptionService _encryptionService;

    public SafeManagementService(
        ApplicationDbContext context,
        ILogger<SafeManagementService> logger,
        IEncryptionService encryptionService)
    {
        _context = context;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    // Safe Management

    public async Task<PrivilegedSafeDto> CreateSafeAsync(Guid ownerId, CreateSafeRequest request)
    {
        // Validate safe type
        var validTypes = new[] { "Database", "SSH", "Cloud", "Windows", "Linux", "Generic" };
        if (!validTypes.Contains(request.SafeType))
            throw new ArgumentException($"Invalid safe type. Must be one of: {string.Join(", ", validTypes)}", nameof(request.SafeType));

        var safe = new PrivilegedSafe
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = ownerId,
            SafeType = request.SafeType,
            AccessControl = JsonSerializer.Serialize(request.AccessControl),
            RequireApproval = request.RequireApproval,
            RequireDualControl = request.RequireDualControl,
            MaxCheckoutDurationMinutes = request.MaxCheckoutDurationMinutes,
            RotateOnCheckin = request.RotateOnCheckin,
            SessionRecordingEnabled = request.SessionRecordingEnabled,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.PrivilegedSafes.Add(safe);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Privileged safe {SafeId} created by user {UserId}", safe.Id, ownerId);

        return await MapSafeToDtoAsync(safe);
    }

    public async Task<PrivilegedSafeDto?> GetSafeByIdAsync(Guid id, Guid userId)
    {
        var safe = await _context.PrivilegedSafes
            .Include(s => s.Owner)
            .Include(s => s.Accounts)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (safe == null)
            return null;

        // Check access
        if (!await HasSafeAccessAsync(id, userId, "read"))
            return null;

        return await MapSafeToDtoAsync(safe);
    }

    public async Task<List<PrivilegedSafeDto>> GetSafesAsync(Guid userId, string? safeType = null)
    {
        var query = _context.PrivilegedSafes
            .Include(s => s.Owner)
            .Include(s => s.Accounts)
            .Where(s => s.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(safeType))
            query = query.Where(s => s.SafeType == safeType);

        var safes = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

        // Also get safes where user has access through ACL
        var allSafes = await _context.PrivilegedSafes
            .Include(s => s.Owner)
            .Include(s => s.Accounts)
            .ToListAsync();

        var accessibleSafes = allSafes.Where(s =>
        {
            if (s.OwnerId == userId)
                return true;

            var acl = JsonSerializer.Deserialize<List<SafeAccessControl>>(s.AccessControl);
            return acl?.Any(a => a.UserId == userId) ?? false;
        }).ToList();

        if (!string.IsNullOrWhiteSpace(safeType))
            accessibleSafes = accessibleSafes.Where(s => s.SafeType == safeType).ToList();

        var tasks = accessibleSafes.Select(MapSafeToDtoAsync);
        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<bool> UpdateSafeAsync(Guid id, Guid userId, UpdateSafeRequest request)
    {
        var safe = await _context.PrivilegedSafes.FirstOrDefaultAsync(s => s.Id == id);

        if (safe == null)
            return false;

        // Check manage permission
        if (!await HasSafeAccessAsync(id, userId, "manage"))
            return false;

        // Update fields
        if (!string.IsNullOrWhiteSpace(request.Name))
            safe.Name = request.Name;

        if (request.Description != null)
            safe.Description = request.Description;

        if (request.AccessControl != null)
            safe.AccessControl = JsonSerializer.Serialize(request.AccessControl);

        if (request.RequireApproval.HasValue)
            safe.RequireApproval = request.RequireApproval.Value;

        if (request.RequireDualControl.HasValue)
            safe.RequireDualControl = request.RequireDualControl.Value;

        if (request.MaxCheckoutDurationMinutes.HasValue)
            safe.MaxCheckoutDurationMinutes = request.MaxCheckoutDurationMinutes.Value;

        if (request.RotateOnCheckin.HasValue)
            safe.RotateOnCheckin = request.RotateOnCheckin.Value;

        if (request.SessionRecordingEnabled.HasValue)
            safe.SessionRecordingEnabled = request.SessionRecordingEnabled.Value;

        if (request.Metadata != null)
            safe.Metadata = JsonSerializer.Serialize(request.Metadata);

        safe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Privileged safe {SafeId} updated by user {UserId}", id, userId);

        return true;
    }

    public async Task<bool> DeleteSafeAsync(Guid id, Guid userId)
    {
        var safe = await _context.PrivilegedSafes
            .Include(s => s.Accounts)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (safe == null)
            return false;

        // Only owner can delete
        if (safe.OwnerId != userId)
            return false;

        _context.PrivilegedSafes.Remove(safe);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Privileged safe {SafeId} deleted by user {UserId}", id, userId);

        return true;
    }

    // Account Management

    public async Task<PrivilegedAccountDto> AddAccountAsync(Guid safeId, Guid userId, CreatePrivilegedAccountRequest request)
    {
        var safe = await _context.PrivilegedSafes.FirstOrDefaultAsync(s => s.Id == safeId);

        if (safe == null)
            throw new InvalidOperationException("Safe not found");

        // Check manage permission
        if (!await HasSafeAccessAsync(safeId, userId, "manage"))
            throw new UnauthorizedAccessException("Insufficient permissions to add accounts to this safe");

        // Encrypt password
        var encryptedPassword = _encryptionService.Encrypt(request.Password);

        // Calculate next rotation if scheduled
        DateTime? nextRotation = null;
        if (request.RotationPolicy == "scheduled")
        {
            nextRotation = DateTime.UtcNow.AddDays(request.RotationIntervalDays);
        }

        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = safeId,
            AccountName = request.AccountName,
            Username = request.Username,
            EncryptedPassword = encryptedPassword,
            Platform = request.Platform,
            HostAddress = request.HostAddress,
            Port = request.Port,
            DatabaseName = request.DatabaseName,
            ConnectionDetails = request.ConnectionDetails != null ? JsonSerializer.Serialize(request.ConnectionDetails) : null,
            RotationPolicy = request.RotationPolicy,
            RotationIntervalDays = request.RotationIntervalDays,
            NextRotation = nextRotation,
            Status = "active",
            PasswordComplexity = request.PasswordComplexity,
            RequireMfa = request.RequireMfa,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.PrivilegedAccounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Privileged account {AccountId} added to safe {SafeId} by user {UserId}",
            account.Id, safeId, userId);

        return await MapAccountToDtoAsync(account);
    }

    public async Task<PrivilegedAccountDto?> GetAccountByIdAsync(Guid accountId, Guid userId)
    {
        var account = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .Include(a => a.Checkouts.Where(c => c.Status == "active"))
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
            return null;

        // Check access
        if (!await HasAccountAccessAsync(accountId, userId, "read"))
            return null;

        return await MapAccountToDtoAsync(account);
    }

    public async Task<List<PrivilegedAccountDto>> GetAccountsAsync(Guid safeId, Guid userId)
    {
        // Check access
        if (!await HasSafeAccessAsync(safeId, userId, "read"))
            return new List<PrivilegedAccountDto>();

        var accounts = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .Include(a => a.Checkouts.Where(c => c.Status == "active"))
            .Where(a => a.SafeId == safeId)
            .OrderBy(a => a.AccountName)
            .ToListAsync();

        var tasks = accounts.Select(MapAccountToDtoAsync);
        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<bool> UpdateAccountAsync(Guid accountId, Guid userId, UpdatePrivilegedAccountRequest request)
    {
        var account = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
            return false;

        // Check manage permission
        if (!await HasSafeAccessAsync(account.SafeId, userId, "manage"))
            return false;

        // Update fields
        if (!string.IsNullOrWhiteSpace(request.AccountName))
            account.AccountName = request.AccountName;

        if (!string.IsNullOrWhiteSpace(request.Username))
            account.Username = request.Username;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            account.EncryptedPassword = _encryptionService.Encrypt(request.Password);
            account.LastRotated = DateTime.UtcNow;

            // Update next rotation if scheduled
            if (account.RotationPolicy == "scheduled")
                account.NextRotation = DateTime.UtcNow.AddDays(account.RotationIntervalDays);
        }

        if (request.HostAddress != null)
            account.HostAddress = request.HostAddress;

        if (request.Port.HasValue)
            account.Port = request.Port.Value;

        if (request.DatabaseName != null)
            account.DatabaseName = request.DatabaseName;

        if (request.ConnectionDetails != null)
            account.ConnectionDetails = JsonSerializer.Serialize(request.ConnectionDetails);

        if (!string.IsNullOrWhiteSpace(request.RotationPolicy))
        {
            account.RotationPolicy = request.RotationPolicy;

            // Update next rotation if switched to scheduled
            if (request.RotationPolicy == "scheduled" && account.NextRotation == null)
                account.NextRotation = DateTime.UtcNow.AddDays(account.RotationIntervalDays);
        }

        if (request.RotationIntervalDays.HasValue)
        {
            account.RotationIntervalDays = request.RotationIntervalDays.Value;

            // Recalculate next rotation if scheduled
            if (account.RotationPolicy == "scheduled")
                account.NextRotation = DateTime.UtcNow.AddDays(account.RotationIntervalDays);
        }

        if (request.PasswordComplexity.HasValue)
            account.PasswordComplexity = request.PasswordComplexity.Value;

        if (request.RequireMfa.HasValue)
            account.RequireMfa = request.RequireMfa.Value;

        if (!string.IsNullOrWhiteSpace(request.Status))
            account.Status = request.Status;

        if (request.Metadata != null)
            account.Metadata = JsonSerializer.Serialize(request.Metadata);

        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Privileged account {AccountId} updated by user {UserId}", accountId, userId);

        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid accountId, Guid userId)
    {
        var account = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
            return false;

        // Check manage permission
        if (!await HasSafeAccessAsync(account.SafeId, userId, "manage"))
            return false;

        _context.PrivilegedAccounts.Remove(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Privileged account {AccountId} deleted by user {UserId}", accountId, userId);

        return true;
    }

    public async Task<RevealPasswordResponse> RevealPasswordAsync(Guid accountId, Guid userId, string? reason = null)
    {
        var account = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
            throw new InvalidOperationException("Account not found");

        // Check checkout permission
        if (!await HasAccountAccessAsync(accountId, userId, "checkout"))
            throw new UnauthorizedAccessException("Insufficient permissions to reveal this password");

        // Decrypt password
        var password = _encryptionService.Decrypt(account.EncryptedPassword);

        // Generate connection string if applicable
        string? connectionString = null;
        if (!string.IsNullOrWhiteSpace(account.HostAddress))
        {
            connectionString = account.Platform.ToLower() switch
            {
                "postgresql" => $"Host={account.HostAddress};Port={account.Port ?? 5432};Database={account.DatabaseName};Username={account.Username};Password={password}",
                "mysql" => $"Server={account.HostAddress};Port={account.Port ?? 3306};Database={account.DatabaseName};Uid={account.Username};Pwd={password}",
                "sqlserver" => $"Server={account.HostAddress},{account.Port ?? 1433};Database={account.DatabaseName};User Id={account.Username};Password={password}",
                "mongodb" => $"mongodb://{account.Username}:{password}@{account.HostAddress}:{account.Port ?? 27017}/{account.DatabaseName}",
                "redis" => $"redis://{account.Username}:{password}@{account.HostAddress}:{account.Port ?? 6379}",
                _ => null
            };
        }

        _logger.LogWarning("Password revealed for account {AccountId} by user {UserId}. Reason: {Reason}",
            accountId, userId, reason ?? "Not specified");

        return new RevealPasswordResponse
        {
            AccountId = accountId,
            Username = account.Username,
            Password = password,
            HostAddress = account.HostAddress,
            Port = account.Port,
            DatabaseName = account.DatabaseName,
            ConnectionString = connectionString,
            RevealedAt = DateTime.UtcNow,
            ValidForMinutes = account.Safe.MaxCheckoutDurationMinutes
        };
    }

    public async Task<bool> HasSafeAccessAsync(Guid safeId, Guid userId, string permission = "read")
    {
        var safe = await _context.PrivilegedSafes.FindAsync(safeId);

        if (safe == null)
            return false;

        // Owner has all permissions
        if (safe.OwnerId == userId)
            return true;

        // Check ACL
        var acl = JsonSerializer.Deserialize<List<SafeAccessControl>>(safe.AccessControl);
        var userAccess = acl?.FirstOrDefault(a => a.UserId == userId);

        if (userAccess == null)
            return false;

        // Permission hierarchy: manage > checkout > read
        return permission.ToLower() switch
        {
            "read" => userAccess.Permission == "read" || userAccess.Permission == "checkout" || userAccess.Permission == "manage",
            "checkout" => userAccess.Permission == "checkout" || userAccess.Permission == "manage",
            "manage" => userAccess.Permission == "manage",
            _ => false
        };
    }

    public async Task<bool> HasAccountAccessAsync(Guid accountId, Guid userId, string permission = "read")
    {
        var account = await _context.PrivilegedAccounts.FindAsync(accountId);

        if (account == null)
            return false;

        return await HasSafeAccessAsync(account.SafeId, userId, permission);
    }

    public async Task<SafeStatisticsDto> GetStatisticsAsync(Guid userId)
    {
        var safes = await _context.PrivilegedSafes
            .Include(s => s.Accounts)
            .Where(s => s.OwnerId == userId)
            .ToListAsync();

        // Also get safes where user has access through ACL
        var allSafes = await _context.PrivilegedSafes
            .Include(s => s.Accounts)
            .ToListAsync();

        var accessibleSafes = allSafes.Where(s =>
        {
            if (s.OwnerId == userId)
                return true;

            var acl = JsonSerializer.Deserialize<List<SafeAccessControl>>(s.AccessControl);
            return acl?.Any(a => a.UserId == userId) ?? false;
        }).ToList();

        var allAccounts = accessibleSafes.SelectMany(s => s.Accounts).ToList();

        var activeCheckouts = await _context.AccountCheckouts
            .Where(c => c.Status == "active" && allAccounts.Select(a => a.Id).Contains(c.AccountId))
            .CountAsync();

        var pendingApprovals = await _context.AccessApprovals
            .Where(a => a.Status == "pending" &&
                       (a.RequesterId == userId || a.Approvers.Contains(userId)))
            .CountAsync();

        var nextWeek = DateTime.UtcNow.AddDays(7);
        var rotationsDue = allAccounts.Count(a =>
            a.RotationPolicy == "scheduled" &&
            a.NextRotation.HasValue &&
            a.NextRotation.Value <= nextWeek);

        var accountsByPlatform = allAccounts
            .GroupBy(a => a.Platform)
            .ToDictionary(g => g.Key, g => g.Count());

        var safesByType = accessibleSafes
            .GroupBy(s => s.SafeType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new SafeStatisticsDto
        {
            TotalSafes = accessibleSafes.Count,
            TotalAccounts = allAccounts.Count,
            ActiveCheckouts = activeCheckouts,
            PendingApprovals = pendingApprovals,
            RotationsDueThisWeek = rotationsDue,
            AccountsByPlatform = accountsByPlatform,
            SafesByType = safesByType
        };
    }

    public async Task<List<PrivilegedAccountDto>> SearchAccountsAsync(Guid userId, string searchTerm, string? platform = null)
    {
        // Get all accessible safes
        var allSafes = await _context.PrivilegedSafes.ToListAsync();

        var accessibleSafeIds = allSafes.Where(s =>
        {
            if (s.OwnerId == userId)
                return true;

            var acl = JsonSerializer.Deserialize<List<SafeAccessControl>>(s.AccessControl);
            return acl?.Any(a => a.UserId == userId) ?? false;
        }).Select(s => s.Id).ToList();

        var query = _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .Include(a => a.Checkouts.Where(c => c.Status == "active"))
            .Where(a => accessibleSafeIds.Contains(a.SafeId));

        if (!string.IsNullOrWhiteSpace(platform))
            query = query.Where(a => a.Platform.ToLower() == platform.ToLower());

        var searchLower = searchTerm.ToLower();
        query = query.Where(a =>
            a.AccountName.ToLower().Contains(searchLower) ||
            a.Username.ToLower().Contains(searchLower) ||
            (a.HostAddress != null && a.HostAddress.ToLower().Contains(searchLower)));

        var accounts = await query.Take(100).ToListAsync();

        var tasks = accounts.Select(MapAccountToDtoAsync);
        return (await Task.WhenAll(tasks)).ToList();
    }

    // Private helper methods

    private async Task<PrivilegedSafeDto> MapSafeToDtoAsync(PrivilegedSafe safe)
    {
        var owner = await _context.Users.FindAsync(safe.OwnerId);
        var acl = JsonSerializer.Deserialize<List<SafeAccessControl>>(safe.AccessControl) ?? new List<SafeAccessControl>();
        var metadata = !string.IsNullOrWhiteSpace(safe.Metadata)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(safe.Metadata)
            : null;

        return new PrivilegedSafeDto
        {
            Id = safe.Id,
            Name = safe.Name,
            Description = safe.Description,
            OwnerId = safe.OwnerId,
            OwnerName = owner?.UserName ?? "Unknown",
            SafeType = safe.SafeType,
            AccessControl = acl,
            RequireApproval = safe.RequireApproval,
            RequireDualControl = safe.RequireDualControl,
            MaxCheckoutDurationMinutes = safe.MaxCheckoutDurationMinutes,
            RotateOnCheckin = safe.RotateOnCheckin,
            SessionRecordingEnabled = safe.SessionRecordingEnabled,
            Metadata = metadata,
            AccountCount = safe.Accounts?.Count ?? 0,
            CreatedAt = safe.CreatedAt,
            UpdatedAt = safe.UpdatedAt
        };
    }

    private async Task<PrivilegedAccountDto> MapAccountToDtoAsync(PrivilegedAccount account)
    {
        var safe = await _context.PrivilegedSafes.FindAsync(account.SafeId);
        var connectionDetails = !string.IsNullOrWhiteSpace(account.ConnectionDetails)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(account.ConnectionDetails)
            : null;
        var metadata = !string.IsNullOrWhiteSpace(account.Metadata)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(account.Metadata)
            : null;

        var activeCheckout = account.Checkouts.FirstOrDefault(c => c.Status == "active");

        return new PrivilegedAccountDto
        {
            Id = account.Id,
            SafeId = account.SafeId,
            SafeName = safe?.Name ?? "Unknown",
            AccountName = account.AccountName,
            Username = account.Username,
            Platform = account.Platform,
            HostAddress = account.HostAddress,
            Port = account.Port,
            DatabaseName = account.DatabaseName,
            ConnectionDetails = connectionDetails,
            RotationPolicy = account.RotationPolicy,
            RotationIntervalDays = account.RotationIntervalDays,
            LastRotated = account.LastRotated,
            NextRotation = account.NextRotation,
            Status = account.Status,
            PasswordComplexity = account.PasswordComplexity ?? 16,
            RequireMfa = account.RequireMfa,
            Metadata = metadata,
            IsCheckedOut = activeCheckout != null,
            CurrentCheckoutId = activeCheckout?.Id,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }
}
