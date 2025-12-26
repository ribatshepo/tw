using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using USP.Core.Models.Entities;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Secrets;

namespace USP.UnitTests.Services.Secrets;

/// <summary>
/// Unit tests for LeaseManagementService
/// Tests lease creation, renewal, revocation, and automatic expiration handling
/// </summary>
public class LeaseManagementServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<LeaseManagementService>> _loggerMock;
    private readonly LeaseManagementService _service;

    public LeaseManagementServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"LeaseManagementTest_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup mocks
        _loggerMock = new Mock<ILogger<LeaseManagementService>>();

        _service = new LeaseManagementService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateLeaseAsync_ValidParameters_CreatesLeaseSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            PasswordHash = "dummy-hash"
        };

        var secret = new Secret
        {
            Id = secretId,
            Path = "secret/data/test",
            EncryptedData = "encrypted-value",
            CreatedBy = userId
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        await _context.SaveChangesAsync();

        // Act
        var lease = await _service.CreateLeaseAsync(
            secretId,
            userId,
            leaseDurationSeconds: 3600,
            autoRenewalEnabled: true,
            maxRenewals: 5);

        // Assert
        Assert.NotNull(lease);
        Assert.Equal(secretId, lease.SecretId);
        Assert.Equal(userId, lease.UserId);
        Assert.Equal(3600, lease.LeaseDurationSeconds);
        Assert.True(lease.AutoRenewalEnabled);
        Assert.Equal(5, lease.MaxRenewals);
        Assert.Equal("active", lease.Status);
        Assert.Equal(0, lease.RenewalCount);
        Assert.True(lease.CanRenew);
        Assert.False(lease.IsExpired);

        // Verify lease was saved to database
        var savedLease = await _context.Leases.FindAsync(lease.LeaseId);
        Assert.NotNull(savedLease);
    }

    [Fact]
    public async Task CreateLeaseAsync_SecretNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonExistentSecretId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            PasswordHash = "dummy-hash"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateLeaseAsync(nonExistentSecretId, userId, 3600));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task CreateLeaseAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var secret = new Secret
        {
            Id = secretId,
            Path = "secret/test",
            EncryptedData = "value1"
        };

        _context.Secrets.Add(secret);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateLeaseAsync(secretId, nonExistentUserId, 3600));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task CreateLeaseAsync_DurationTooShort_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateLeaseAsync(secretId, userId, leaseDurationSeconds: 30));

        Assert.Contains("at least 60 seconds", exception.Message);
    }

    [Fact]
    public async Task CreateLeaseAsync_DurationTooLong_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateLeaseAsync(secretId, userId, leaseDurationSeconds: 90000));

        Assert.Contains("cannot exceed 24 hours", exception.Message);
    }

    [Fact]
    public async Task RenewLeaseAsync_ValidLease_RenewsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            IssuedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            LeaseDurationSeconds = 3600,
            Status = "active",
            RenewalCount = 0,
            MaxRenewals = 10
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        var originalExpiresAt = lease.ExpiresAt;

        // Act
        var renewed = await _service.RenewLeaseAsync(lease.Id, userId);

        // Assert
        Assert.NotNull(renewed);
        Assert.Equal(1, renewed.RenewalCount);
        Assert.True(renewed.ExpiresAt > originalExpiresAt);

        // Verify renewal history was created
        var history = await _context.LeaseRenewalHistories
            .Where(h => h.LeaseId == lease.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(history);
        Assert.True(history.Success);
        Assert.Equal(1, history.RenewalCount);
        Assert.Equal(userId, history.RenewedBy);
        Assert.False(history.IsAutoRenewal);
    }

    [Fact]
    public async Task RenewLeaseAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var owner = new ApplicationUser { Id = ownerId, Email = "owner@example.com", UserName = "owner", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = ownerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        _context.Users.Add(owner);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RenewLeaseAsync(lease.Id, otherUserId));

        Assert.Contains("lease owner", exception.Message);
    }

    [Fact]
    public async Task RenewLeaseAsync_MaxRenewalsReached_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            LeaseDurationSeconds = 3600,
            Status = "active",
            RenewalCount = 3,
            MaxRenewals = 3
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RenewLeaseAsync(lease.Id, userId));

        Assert.Contains("Maximum renewals", exception.Message);
    }

    [Fact]
    public async Task RenewLeaseAsync_RevokedLease_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "revoked",
            RevokedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RenewLeaseAsync(lease.Id, userId));

        Assert.Contains("Cannot renew lease", exception.Message);
    }

    [Fact]
    public async Task RevokeLeaseAsync_ValidLease_RevokesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act
        await _service.RevokeLeaseAsync(lease.Id, userId, "No longer needed");

        // Assert
        var revokedLease = await _context.Leases.FindAsync(lease.Id);
        Assert.NotNull(revokedLease);
        Assert.Equal("revoked", revokedLease.Status);
        Assert.NotNull(revokedLease.RevokedAt);
        Assert.Equal(userId, revokedLease.RevokedBy);
        Assert.Equal("No longer needed", revokedLease.RevocationReason);
    }

    [Fact]
    public async Task RevokeLeaseAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var owner = new ApplicationUser { Id = ownerId, Email = "owner@example.com", UserName = "owner", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = ownerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        _context.Users.Add(owner);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RevokeLeaseAsync(lease.Id, otherUserId));

        Assert.Contains("lease owner", exception.Message);
    }

    [Fact]
    public async Task GetLeaseAsync_ValidLease_ReturnsLeaseDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/data/prod", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            IssuedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            LeaseDurationSeconds = 3600,
            Status = "active",
            RenewalCount = 2,
            MaxRenewals = 10
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetLeaseAsync(lease.Id, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lease.Id, result.LeaseId);
        Assert.Equal(secretId, result.SecretId);
        Assert.Equal("secret/data/prod", result.SecretPath);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("user@example.com", result.UserEmail);
        Assert.Equal(2, result.RenewalCount);
        Assert.True(result.CanRenew);
        Assert.False(result.IsExpired);
    }

    [Fact]
    public async Task GetUserLeasesAsync_MultipleLeases_ReturnsAllUserLeases()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var secret1Id = Guid.NewGuid();
        var secret2Id = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret1 = new Secret { Id = secret1Id, Path = "secret/1", EncryptedData = "value1" };
        var secret2 = new Secret { Id = secret2Id, Path = "secret/2", EncryptedData = "value2" };

        var lease1 = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secret1Id,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        var lease2 = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secret2Id,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        var otherUserLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secret1Id,
            UserId = otherUserId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        _context.Users.Add(user);
        _context.Secrets.AddRange(secret1, secret2);
        _context.Leases.AddRange(lease1, lease2, otherUserLease);
        await _context.SaveChangesAsync();

        // Act
        var leases = await _service.GetUserLeasesAsync(userId);

        // Assert
        Assert.Equal(2, leases.Count);
        Assert.All(leases, l => Assert.Equal(userId, l.UserId));
    }

    [Fact]
    public async Task GetUserLeasesAsync_IncludeExpiredFalse_ExcludesExpiredLeases()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secret1Id = Guid.NewGuid();
        var secret2Id = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret1 = new Secret { Id = secret1Id, Path = "secret/1", EncryptedData = "value1" };
        var secret2 = new Secret { Id = secret2Id, Path = "secret/2", EncryptedData = "value2" };

        var activeLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secret1Id,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        var expiredLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secret2Id,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            LeaseDurationSeconds = 3600,
            Status = "expired"
        };

        _context.Users.Add(user);
        _context.Secrets.AddRange(secret1, secret2);
        _context.Leases.AddRange(activeLease, expiredLease);
        await _context.SaveChangesAsync();

        // Act
        var leases = await _service.GetUserLeasesAsync(userId, includeExpired: false);

        // Assert
        Assert.Single(leases);
        Assert.Equal(activeLease.Id, leases[0].LeaseId);
        Assert.Equal("active", leases[0].Status);
    }

    [Fact]
    public async Task HandleExpiringLeasesAsync_MarksExpiredLeasesAsExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var expiredLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            LeaseDurationSeconds = 3600,
            Status = "active" // Still marked as active but should be expired
        };

        var activeLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.AddRange(expiredLease, activeLease);
        await _context.SaveChangesAsync();

        // Act
        await _service.HandleExpiringLeasesAsync();

        // Assert
        var updatedExpiredLease = await _context.Leases.FindAsync(expiredLease.Id);
        var updatedActiveLease = await _context.Leases.FindAsync(activeLease.Id);

        Assert.Equal("expired", updatedExpiredLease!.Status);
        Assert.Equal("active", updatedActiveLease!.Status);
    }

    [Fact]
    public async Task ProcessAutoRenewalsAsync_RenewsLeasesExpiringWithin10Minutes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var leaseToRenew = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), // Expires in 5 minutes
            LeaseDurationSeconds = 3600,
            Status = "active",
            AutoRenewalEnabled = true,
            RenewalCount = 0,
            MaxRenewals = 10
        };

        var leaseNotYetExpiring = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Expires in 15 minutes
            LeaseDurationSeconds = 3600,
            Status = "active",
            AutoRenewalEnabled = true
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.AddRange(leaseToRenew, leaseNotYetExpiring);
        await _context.SaveChangesAsync();

        var originalExpiresAt = leaseToRenew.ExpiresAt;

        // Act
        await _service.ProcessAutoRenewalsAsync();

        // Assert
        var renewed = await _context.Leases.FindAsync(leaseToRenew.Id);
        var notRenewed = await _context.Leases.FindAsync(leaseNotYetExpiring.Id);

        Assert.NotNull(renewed);
        Assert.Equal(1, renewed.RenewalCount);
        Assert.True(renewed.ExpiresAt > originalExpiresAt);
        Assert.NotNull(renewed.LastRenewedAt);

        Assert.NotNull(notRenewed);
        Assert.Equal(0, notRenewed.RenewalCount);

        // Verify renewal history was created
        var history = await _context.LeaseRenewalHistories
            .Where(h => h.LeaseId == leaseToRenew.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(history);
        Assert.True(history.Success);
        Assert.True(history.IsAutoRenewal);
        Assert.Null(history.RenewedBy);
    }

    [Fact]
    public async Task ProcessAutoRenewalsAsync_MaxRenewalsReached_DisablesAutoRenewal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            LeaseDurationSeconds = 3600,
            Status = "active",
            AutoRenewalEnabled = true,
            RenewalCount = 5,
            MaxRenewals = 5
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Act
        await _service.ProcessAutoRenewalsAsync();

        // Assert
        var updatedLease = await _context.Leases.FindAsync(lease.Id);
        Assert.NotNull(updatedLease);
        Assert.False(updatedLease.AutoRenewalEnabled); // Should be disabled
        Assert.Equal(5, updatedLease.RenewalCount); // Should not increment

        // Verify failed renewal history was created
        var history = await _context.LeaseRenewalHistories
            .Where(h => h.LeaseId == lease.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(history);
        Assert.False(history.Success);
        Assert.Contains("Maximum renewals", history.ErrorMessage);
    }

    [Fact]
    public async Task RevokeAllSecretLeasesAsync_RevokesAllActiveLeasesForSecret()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease1 = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        var lease2 = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            LeaseDurationSeconds = 3600,
            Status = "active"
        };

        var alreadyRevokedLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "revoked"
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.AddRange(lease1, lease2, alreadyRevokedLease);
        await _context.SaveChangesAsync();

        // Act
        await _service.RevokeAllSecretLeasesAsync(secretId, userId, "Secret rotated");

        // Assert
        var allLeases = await _context.Leases.Where(l => l.SecretId == secretId).ToListAsync();

        var revokedCount = allLeases.Count(l => l.Status == "revoked");
        Assert.Equal(3, revokedCount); // All 3 should be revoked now

        var lease1Updated = await _context.Leases.FindAsync(lease1.Id);
        Assert.Equal("revoked", lease1Updated!.Status);
        Assert.Equal("Secret rotated", lease1Updated.RevocationReason);
        Assert.Equal(userId, lease1Updated.RevokedBy);
    }

    [Fact]
    public async Task GetLeaseStatisticsAsync_CalculatesCorrectStatistics()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var activeLease1 = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            LeaseDurationSeconds = 3600,
            Status = "active",
            RenewalCount = 2,
            AutoRenewalEnabled = true
        };

        var activeLease2 = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            LeaseDurationSeconds = 7200,
            Status = "active",
            RenewalCount = 1,
            AutoRenewalEnabled = false
        };

        var expiredLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            LeaseDurationSeconds = 3600,
            Status = "expired",
            RenewalCount = 5
        };

        var revokedLease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "revoked",
            RenewalCount = 0
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.AddRange(activeLease1, activeLease2, expiredLease, revokedLease);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.GetLeaseStatisticsAsync(userId);

        // Assert
        Assert.Equal(4, stats.TotalLeases);
        Assert.Equal(2, stats.ActiveLeases);
        Assert.Equal(1, stats.ExpiredLeases);
        Assert.Equal(1, stats.RevokedLeases);
        Assert.Equal(1, stats.AutoRenewalEnabledCount);
        Assert.Equal(8, stats.TotalRenewals); // 2 + 1 + 5 + 0
        Assert.Equal(4500.0, stats.AverageLeaseDurationSeconds); // (3600 + 7200 + 3600 + 3600) / 4 = 18000 / 4
        Assert.Equal(2.0, stats.AverageRenewalCount); // (2 + 1 + 5 + 0) / 4
        Assert.Equal(1, stats.LeasesExpiringIn1Hour); // activeLease1 (1 hour)
        Assert.Equal(2, stats.LeasesExpiringIn24Hours); // activeLease1 (1 hour) + activeLease2 (2 hours)
    }

    [Fact]
    public async Task GetLeaseRenewalHistoryAsync_ReturnsCompleteHistory()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secretId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        var secret = new Secret { Id = secretId, Path = "secret/test", EncryptedData = "value1" };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            SecretId = secretId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LeaseDurationSeconds = 3600,
            Status = "active",
            RenewalCount = 2
        };

        var renewal1 = new LeaseRenewalHistory
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            RenewedAt = DateTime.UtcNow.AddMinutes(-60),
            PreviousExpiresAt = DateTime.UtcNow.AddMinutes(-30),
            NewExpiresAt = DateTime.UtcNow.AddMinutes(30),
            RenewalCount = 1,
            Success = true,
            RenewedBy = userId,
            IsAutoRenewal = false
        };

        var renewal2 = new LeaseRenewalHistory
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            RenewedAt = DateTime.UtcNow.AddMinutes(-30),
            PreviousExpiresAt = DateTime.UtcNow.AddMinutes(30),
            NewExpiresAt = DateTime.UtcNow.AddHours(1),
            RenewalCount = 2,
            Success = true,
            IsAutoRenewal = true
        };

        _context.Users.Add(user);
        _context.Secrets.Add(secret);
        _context.Leases.Add(lease);
        _context.LeaseRenewalHistories.AddRange(renewal1, renewal2);
        await _context.SaveChangesAsync();

        // Act
        var history = await _service.GetLeaseRenewalHistoryAsync(lease.Id, userId);

        // Assert
        Assert.Equal(2, history.Count);

        // Verify ordering (most recent first)
        Assert.True(history[0].RenewedAt > history[1].RenewedAt);

        // Verify renewal details
        var manualRenewal = history.First(h => !h.IsAutoRenewal);
        Assert.Equal(userId, manualRenewal.RenewedBy);
        Assert.True(manualRenewal.Success);

        var autoRenewal = history.First(h => h.IsAutoRenewal);
        Assert.Null(autoRenewal.RenewedBy);
        Assert.True(autoRenewal.Success);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
