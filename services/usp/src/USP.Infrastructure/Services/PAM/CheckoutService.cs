using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.Cryptography;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

public class CheckoutService : ICheckoutService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ISafeManagementService _safeService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ISafeManagementService safeService,
        IAuditService auditService,
        ILogger<CheckoutService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _safeService = safeService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<CheckoutResponseDto> RequestCheckoutAsync(Guid accountId, Guid userId, CheckoutRequestDto request)
    {
        // Validate reason
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Reason is required for checkout", nameof(request));

        // Get account with safe
        var account = await _context.PrivilegedAccounts
            .Include(a => a.Safe)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
            throw new InvalidOperationException("Account not found");

        // Check if user has checkout access
        var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "checkout");
        if (!hasAccess)
            throw new UnauthorizedAccessException("User does not have checkout permission for this safe");

        // Check if account is already checked out
        var existingCheckout = await _context.AccountCheckouts
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.Status == "active");

        if (existingCheckout != null)
        {
            throw new InvalidOperationException(
                $"Account is already checked out by user {existingCheckout.UserId} until {existingCheckout.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
        }

        // Check if approval is required
        if (account.Safe.RequireApproval || account.Safe.RequireDualControl)
        {
            // Create approval request
            var approval = new AccessApproval
            {
                Id = Guid.NewGuid(),
                RequesterId = userId,
                ResourceType = "PrivilegedAccount",
                ResourceId = accountId,
                Reason = request.Reason,
                Status = "pending",
                RequestedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                ApprovalPolicy = account.Safe.RequireDualControl ? "dual_control" : "single_approver",
                Approvers = new List<Guid>() // Will be populated by approval workflow
            };

            _context.AccessApprovals.Add(approval);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "checkout_approval_requested",
                "PrivilegedAccount",
                accountId.ToString(),
                null,
                new { approvalId = approval.Id, reason = request.Reason });

            throw new InvalidOperationException(
                $"Approval required for this account. Approval request ID: {approval.Id}. Status: pending");
        }

        // Determine checkout duration
        var durationMinutes = request.DurationMinutes ?? account.Safe.MaxCheckoutDurationMinutes;
        if (durationMinutes > account.Safe.MaxCheckoutDurationMinutes)
        {
            durationMinutes = account.Safe.MaxCheckoutDurationMinutes;
            _logger.LogWarning(
                "Checkout duration {RequestedMinutes} exceeds max {MaxMinutes}, using max",
                request.DurationMinutes,
                account.Safe.MaxCheckoutDurationMinutes);
        }

        // Create checkout record
        var checkout = new AccountCheckout
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            CheckedOutAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes),
            Reason = request.Reason,
            Status = "active",
            RotateOnCheckin = request.RotateOnCheckin,
            ApprovalId = null
        };

        _context.AccountCheckouts.Add(checkout);
        await _context.SaveChangesAsync();

        // Decrypt password
        var password = _encryptionService.Decrypt(account.EncryptedPassword);

        // Generate connection string if applicable
        string? connectionString = null;
        if (!string.IsNullOrWhiteSpace(account.HostAddress))
        {
            connectionString = GenerateConnectionString(account, password);
        }

        // Audit log
        await _auditService.LogAsync(
            userId,
            "account_checkout",
            "PrivilegedAccount",
            accountId.ToString(),
            null,
            new
            {
                checkoutId = checkout.Id,
                reason = request.Reason,
                durationMinutes,
                expiresAt = checkout.ExpiresAt,
                rotateOnCheckin = request.RotateOnCheckin
            });

        _logger.LogInformation(
            "Account {AccountId} checked out by user {UserId} until {ExpiresAt}",
            accountId,
            userId,
            checkout.ExpiresAt);

        return new CheckoutResponseDto
        {
            CheckoutId = checkout.Id,
            AccountId = accountId,
            AccountName = account.AccountName,
            Username = account.Username,
            Password = password,
            ConnectionString = connectionString,
            CheckedOutAt = checkout.CheckedOutAt,
            ExpiresAt = checkout.ExpiresAt,
            RotateOnCheckin = checkout.RotateOnCheckin,
            Message = $"Account checked out successfully. Access expires at {checkout.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC"
        };
    }

    public async Task<bool> CheckinAccountAsync(Guid checkoutId, Guid userId, CheckinRequestDto? request = null)
    {
        var checkout = await _context.AccountCheckouts
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == checkoutId);

        if (checkout == null)
            return false;

        // Only the user who checked out can checkin (or admin via ForceCheckin)
        if (checkout.UserId != userId)
            throw new UnauthorizedAccessException("Only the user who checked out the account can checkin");

        if (checkout.Status != "active")
            throw new InvalidOperationException($"Checkout is not active (status: {checkout.Status})");

        // Update checkout status
        checkout.CheckedInAt = DateTime.UtcNow;
        checkout.Status = "checkedin";

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "account_checkin",
            "AccountCheckout",
            checkoutId.ToString(),
            null,
            new
            {
                accountId = checkout.AccountId,
                notes = request?.Notes,
                rotateOnCheckin = checkout.RotateOnCheckin
            });

        _logger.LogInformation(
            "Account {AccountId} checked in by user {UserId}",
            checkout.AccountId,
            userId);

        // Handle password rotation if requested
        if (checkout.RotateOnCheckin)
        {
            _logger.LogInformation(
                "Password rotation requested on checkin for account {AccountId}",
                checkout.AccountId);
            // Password rotation will be handled by PasswordRotationService in Phase 3.4
        }

        return true;
    }

    public async Task<List<AccountCheckoutDto>> GetActiveCheckoutsAsync(Guid userId)
    {
        var checkouts = await _context.AccountCheckouts
            .Include(c => c.Account)
            .Include(c => c.User)
            .Where(c => c.UserId == userId && c.Status == "active")
            .OrderByDescending(c => c.CheckedOutAt)
            .ToListAsync();

        return checkouts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountCheckoutDto>> GetCheckoutHistoryAsync(Guid userId, int? limit = 50)
    {
        var checkouts = await _context.AccountCheckouts
            .Include(c => c.Account)
            .Include(c => c.User)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CheckedOutAt)
            .Take(limit ?? 50)
            .ToListAsync();

        return checkouts.Select(MapToDto).ToList();
    }

    public async Task<AccountCheckoutDto?> GetCheckoutByIdAsync(Guid checkoutId, Guid userId)
    {
        var checkout = await _context.AccountCheckouts
            .Include(c => c.Account)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == checkoutId);

        if (checkout == null)
            return null;

        // Only the user who checked out can view checkout details
        if (checkout.UserId != userId)
            return null;

        return MapToDto(checkout);
    }

    public async Task<bool> ExtendCheckoutAsync(Guid checkoutId, Guid userId, int additionalMinutes)
    {
        var checkout = await _context.AccountCheckouts
            .Include(c => c.Account)
            .ThenInclude(a => a.Safe)
            .FirstOrDefaultAsync(c => c.Id == checkoutId);

        if (checkout == null)
            return false;

        // Only the user who checked out can extend
        if (checkout.UserId != userId)
            throw new UnauthorizedAccessException("Only the user who checked out the account can extend");

        if (checkout.Status != "active")
            throw new InvalidOperationException($"Checkout is not active (status: {checkout.Status})");

        // Calculate new expiration
        var newExpiration = checkout.ExpiresAt.AddMinutes(additionalMinutes);
        var maxExpiration = checkout.CheckedOutAt.AddMinutes(checkout.Account.Safe.MaxCheckoutDurationMinutes);

        if (newExpiration > maxExpiration)
        {
            throw new InvalidOperationException(
                $"Cannot extend beyond maximum checkout duration of {checkout.Account.Safe.MaxCheckoutDurationMinutes} minutes");
        }

        var oldExpiration = checkout.ExpiresAt;
        checkout.ExpiresAt = newExpiration;

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "checkout_extended",
            "AccountCheckout",
            checkoutId.ToString(),
            new { expiresAt = oldExpiration },
            new
            {
                accountId = checkout.AccountId,
                expiresAt = newExpiration,
                additionalMinutes
            });

        _logger.LogInformation(
            "Checkout {CheckoutId} extended from {OldExpiration} to {NewExpiration}",
            checkoutId,
            oldExpiration,
            newExpiration);

        return true;
    }

    public async Task<bool> ForceCheckinAsync(Guid checkoutId, Guid adminUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required for force checkin", nameof(reason));

        var checkout = await _context.AccountCheckouts
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == checkoutId);

        if (checkout == null)
            return false;

        if (checkout.Status != "active")
            throw new InvalidOperationException($"Checkout is not active (status: {checkout.Status})");

        // Update checkout status
        checkout.CheckedInAt = DateTime.UtcNow;
        checkout.Status = "force_checkedin";

        await _context.SaveChangesAsync();

        // Audit log (critical security event)
        await _auditService.LogAsync(
            adminUserId,
            "account_force_checkin",
            "AccountCheckout",
            checkoutId.ToString(),
            null,
            new
            {
                accountId = checkout.AccountId,
                originalUserId = checkout.UserId,
                reason
            });

        _logger.LogWarning(
            "Account {AccountId} force checked in by admin {AdminUserId}. Original user: {UserId}. Reason: {Reason}",
            checkout.AccountId,
            adminUserId,
            checkout.UserId,
            reason);

        return true;
    }

    public async Task<bool> IsAccountCheckedOutAsync(Guid accountId)
    {
        return await _context.AccountCheckouts
            .AnyAsync(c => c.AccountId == accountId && c.Status == "active");
    }

    public async Task<AccountCheckoutDto?> GetActiveCheckoutForAccountAsync(Guid accountId)
    {
        var checkout = await _context.AccountCheckouts
            .Include(c => c.Account)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.Status == "active");

        return checkout != null ? MapToDto(checkout) : null;
    }

    public async Task<int> ProcessExpiredCheckoutsAsync()
    {
        var expiredCheckouts = await _context.AccountCheckouts
            .Include(c => c.Account)
            .Where(c => c.Status == "active" && c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (expiredCheckouts.Count == 0)
            return 0;

        foreach (var checkout in expiredCheckouts)
        {
            checkout.CheckedInAt = DateTime.UtcNow;
            checkout.Status = "expired";

            // Audit log
            await _auditService.LogAsync(
                checkout.UserId,
                "checkout_expired",
                "AccountCheckout",
                checkout.Id.ToString(),
                null,
                new
                {
                    accountId = checkout.AccountId,
                    expiresAt = checkout.ExpiresAt,
                    autoCheckedInAt = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Checkout {CheckoutId} for account {AccountId} auto-expired",
                checkout.Id,
                checkout.AccountId);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Processed {Count} expired checkouts",
            expiredCheckouts.Count);

        return expiredCheckouts.Count;
    }

    public async Task<CheckoutStatisticsDto> GetCheckoutStatisticsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var allCheckouts = await _context.AccountCheckouts
            .Include(c => c.Account)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var activeCheckouts = allCheckouts.Count(c => c.Status == "active");
        var checkoutsLast24Hours = allCheckouts.Count(c => c.CheckedOutAt >= last24Hours);
        var checkoutsLast7Days = allCheckouts.Count(c => c.CheckedOutAt >= last7Days);
        var checkoutsLast30Days = allCheckouts.Count(c => c.CheckedOutAt >= last30Days);

        var recentCheckouts = allCheckouts
            .OrderByDescending(c => c.CheckedOutAt)
            .Take(10)
            .Select(c => new AccountCheckoutSummaryDto
            {
                CheckoutId = c.Id,
                AccountName = c.Account.AccountName,
                Platform = c.Account.Platform,
                CheckedOutAt = c.CheckedOutAt,
                CheckedInAt = c.CheckedInAt,
                Status = c.Status,
                DurationMinutes = (int)(c.ExpiresAt - c.CheckedOutAt).TotalMinutes
            })
            .ToList();

        var checkoutsByPlatform = allCheckouts
            .GroupBy(c => c.Account.Platform)
            .Select(g => new CheckoutByPlatformDto
            {
                Platform = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new CheckoutStatisticsDto
        {
            ActiveCheckouts = activeCheckouts,
            TotalCheckouts = allCheckouts.Count,
            CheckoutsLast24Hours = checkoutsLast24Hours,
            CheckoutsLast7Days = checkoutsLast7Days,
            CheckoutsLast30Days = checkoutsLast30Days,
            RecentCheckouts = recentCheckouts,
            CheckoutsByPlatform = checkoutsByPlatform
        };
    }

    private static AccountCheckoutDto MapToDto(AccountCheckout checkout)
    {
        return new AccountCheckoutDto
        {
            Id = checkout.Id,
            AccountId = checkout.AccountId,
            AccountName = checkout.Account.AccountName,
            UserId = checkout.UserId,
            UserEmail = checkout.User.Email ?? string.Empty,
            CheckedOutAt = checkout.CheckedOutAt,
            CheckedInAt = checkout.CheckedInAt,
            ExpiresAt = checkout.ExpiresAt,
            Reason = checkout.Reason,
            Status = checkout.Status,
            RotateOnCheckin = checkout.RotateOnCheckin,
            ApprovalId = checkout.ApprovalId,
            DurationMinutes = (int)(checkout.ExpiresAt - checkout.CheckedOutAt).TotalMinutes
        };
    }

    private static string? GenerateConnectionString(PrivilegedAccount account, string password)
    {
        if (string.IsNullOrWhiteSpace(account.HostAddress))
            return null;

        return account.Platform.ToLower() switch
        {
            "postgresql" => $"Host={account.HostAddress};Port={account.Port ?? 5432};Database={account.DatabaseName};Username={account.Username};Password={password};",
            "mysql" => $"Server={account.HostAddress};Port={account.Port ?? 3306};Database={account.DatabaseName};Uid={account.Username};Pwd={password};",
            "sqlserver" => $"Server={account.HostAddress},{account.Port ?? 1433};Database={account.DatabaseName};User Id={account.Username};Password={password};",
            "mongodb" => $"mongodb://{account.Username}:{password}@{account.HostAddress}:{account.Port ?? 27017}/{account.DatabaseName}",
            "redis" => $"{account.HostAddress}:{account.Port ?? 6379},password={password}",
            _ => null
        };
    }
}
