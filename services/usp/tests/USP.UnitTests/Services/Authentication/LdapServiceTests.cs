using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.DTOs.Ldap;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Authentication;
using USP.UnitTests.TestHelpers;
using Xunit;

namespace USP.UnitTests.Services.Authentication;

public class LdapServiceTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<LdapService>> _loggerMock;
    private readonly LdapService _sut;

    public LdapServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TestApplicationDbContext(options);

        // Setup mocks
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<LdapService>>();

        // Default mock behaviors
        _encryptionServiceMock
            .Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns<string>(s => $"encrypted_{s}");

        _encryptionServiceMock
            .Setup(x => x.Decrypt(It.IsAny<string>()))
            .Returns<string>(s => s.Replace("encrypted_", ""));

        _jwtServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .Returns("access_token_123");

        _jwtServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token_456");

        _sut = new LdapService(_context, _encryptionServiceMock.Object, _jwtServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ====================
    // Configuration Management Tests
    // ====================

    [Fact]
    public async Task ConfigureLdapAsync_WithValidRequest_CreatesConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "admin@example.com",
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            NormalizedEmail = "ADMIN@EXAMPLE.COM",
            Status = "active",
            PasswordHash = "hashed_password",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new ConfigureLdapRequest
        {
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            UseSsl = true,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "password123",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            EnableJitProvisioning = true,
            DefaultRoleId = null,
            SyncGroupsAsRoles = true,
            EnableGroupSync = true,
            GroupSyncIntervalMinutes = 60
        };

        // Act
        var result = await _sut.ConfigureLdapAsync(request, userId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test LDAP");
        result.ServerUrl.Should().Be("ldap.example.com");
        result.Port.Should().Be(389);
        result.UseSsl.Should().BeTrue();
        result.IsActive.Should().BeTrue();

        var savedConfig = await _context.Set<LdapConfiguration>().FirstOrDefaultAsync();
        savedConfig.Should().NotBeNull();
        savedConfig!.BindPassword.Should().Be("encrypted_password123");

        _encryptionServiceMock.Verify(x => x.Encrypt("password123"), Times.Once);
    }

    [Fact]
    public async Task ConfigureLdapAsync_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "admin@example.com",
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            NormalizedEmail = "ADMIN@EXAMPLE.COM",
            Status = "active",
            PasswordHash = "hashed_password",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _context.Users.Add(user);

        var existingConfig = new LdapConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            CreatedBy = userId
        };
        _context.Set<LdapConfiguration>().Add(existingConfig);
        await _context.SaveChangesAsync();

        var request = new ConfigureLdapRequest
        {
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "password123"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConfigureLdapAsync(request, userId));
    }

    [Fact]
    public async Task ConfigureLdapAsync_WithGroupRoleMapping_SerializesMapping()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var groupMapping = new Dictionary<string, string>
        {
            { "CN=Admins,OU=Groups,DC=example,DC=com", "admin" },
            { "CN=Users,OU=Groups,DC=example,DC=com", "user" }
        };

        var request = new ConfigureLdapRequest
        {
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "password123",
            GroupRoleMapping = groupMapping
        };

        // Act
        var result = await _sut.ConfigureLdapAsync(request, userId);

        // Assert
        result.GroupRoleMapping.Should().NotBeNull();
        result.GroupRoleMapping.Should().HaveCount(2);
        result.GroupRoleMapping!["CN=Admins,OU=Groups,DC=example,DC=com"].Should().Be("admin");
    }

    [Fact]
    public async Task GetConfigurationAsync_WithExistingId_ReturnsConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            CreatedBy = userId
        };
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetConfigurationAsync(configId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(configId);
        result.Name.Should().Be("Test LDAP");
        result.ServerUrl.Should().Be("ldap.example.com");
    }

    [Fact]
    public async Task GetConfigurationAsync_WithNonExistentId_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetConfigurationAsync(nonExistentId));
    }

    [Fact]
    public async Task ListConfigurationsAsync_WithActiveOnly_ReturnsOnlyActiveConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var activeConfig = new LdapConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "Active LDAP",
            ServerUrl = "ldap1.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            IsActive = true,
            CreatedBy = userId
        };

        var inactiveConfig = new LdapConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "Inactive LDAP",
            ServerUrl = "ldap2.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            IsActive = false,
            CreatedBy = userId
        };

        _context.Set<LdapConfiguration>().AddRange(activeConfig, inactiveConfig);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ListConfigurationsAsync(activeOnly: true);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(1);
        result.Configurations[0].Name.Should().Be("Active LDAP");
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task ListConfigurationsAsync_WithActiveOnlyFalse_ReturnsAllConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var activeConfig = new LdapConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "Active LDAP",
            ServerUrl = "ldap1.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            IsActive = true,
            CreatedBy = userId
        };

        var inactiveConfig = new LdapConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "Inactive LDAP",
            ServerUrl = "ldap2.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            IsActive = false,
            CreatedBy = userId
        };

        _context.Set<LdapConfiguration>().AddRange(activeConfig, inactiveConfig);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ListConfigurationsAsync(activeOnly: false);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithValidRequest_UpdatesConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Original Name",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            EnableJitProvisioning = false,
            CreatedBy = userId
        };
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateLdapConfigRequest
        {
            Name = "Updated Name",
            Port = 636,
            UseSsl = true,
            EnableJitProvisioning = true
        };

        // Act
        var result = await _sut.UpdateConfigurationAsync(configId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Port.Should().Be(636);
        result.UseSsl.Should().BeTrue();
        result.EnableJitProvisioning.Should().BeTrue();
        result.UpdatedAt.Should().NotBeNull();

        var updatedConfig = await _context.Set<LdapConfiguration>().FindAsync(configId);
        updatedConfig!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithNewPassword_EncryptsPassword()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_old_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            CreatedBy = userId
        };
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateLdapConfigRequest
        {
            BindPassword = "new_password"
        };

        // Act
        await _sut.UpdateConfigurationAsync(configId, updateRequest);

        // Assert
        var updatedConfig = await _context.Set<LdapConfiguration>().FindAsync(configId);
        updatedConfig!.BindPassword.Should().Be("encrypted_new_password");

        _encryptionServiceMock.Verify(x => x.Encrypt("new_password"), Times.Once);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithNonExistentId_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateLdapConfigRequest { Name = "Updated" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateConfigurationAsync(nonExistentId, updateRequest));
    }

    [Fact]
    public async Task DeleteConfigurationAsync_WithExistingId_DeletesConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            CreatedBy = userId
        };
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteConfigurationAsync(configId);

        // Assert
        var deletedConfig = await _context.Set<LdapConfiguration>().FindAsync(configId);
        deletedConfig.Should().BeNull();
    }

    [Fact]
    public async Task DeleteConfigurationAsync_WithNonExistentId_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.DeleteConfigurationAsync(nonExistentId));
    }

    // ====================
    // Group Synchronization Tests
    // ====================

    [Fact]
    public async Task UpdateUserRolesFromLdapAsync_WithValidGroups_UpdatesUserRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            NormalizedEmail = "USER@EXAMPLE.COM",
            Status = "active",
            PasswordHash = "hashed_password",
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var role1 = new Role
        {
            Id = roleId1,
            Name = "Admins",
            Description = "Admin role"
        };

        var role2 = new Role
        {
            Id = roleId2,
            Name = "Developers",
            Description = "Developer role"
        };

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            SyncGroupsAsRoles = true,
            CreatedBy = userId
        };

        _context.Users.Add(user);
        _context.Set<Role>().AddRange(role1, role2);
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var ldapGroups = new List<string>
        {
            "CN=Admins,OU=Groups,DC=example,DC=com",
            "CN=Developers,OU=Groups,DC=example,DC=com"
        };

        // Act
        await _sut.UpdateUserRolesFromLdapAsync(userId, configId, ldapGroups);

        // Assert
        var userRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .ToListAsync();

        userRoles.Should().HaveCount(2);
        userRoles.Should().Contain(ur => ur.Role.Name == "Admins");
        userRoles.Should().Contain(ur => ur.Role.Name == "Developers");
    }

    [Fact]
    public async Task UpdateUserRolesFromLdapAsync_WithGroupRoleMapping_MapsGroupsToRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            NormalizedEmail = "USER@EXAMPLE.COM",
            Status = "active",
            PasswordHash = "hashed_password",
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var adminRole = new Role
        {
            Id = adminRoleId,
            Name = "admin",
            Description = "Administrator role"
        };

        var groupMapping = new Dictionary<string, string>
        {
            { "CN=Domain Admins,OU=Groups,DC=example,DC=com", "admin" }
        };

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            SyncGroupsAsRoles = true,
            GroupRoleMapping = JsonSerializer.Serialize(groupMapping),
            CreatedBy = userId
        };

        _context.Users.Add(user);
        _context.Set<Role>().Add(adminRole);
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var ldapGroups = new List<string>
        {
            "CN=Domain Admins,OU=Groups,DC=example,DC=com"
        };

        // Act
        await _sut.UpdateUserRolesFromLdapAsync(userId, configId, ldapGroups);

        // Assert
        var userRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .ToListAsync();

        userRoles.Should().HaveCount(1);
        userRoles[0].Role.Name.Should().Be("admin");
    }

    [Fact]
    public async Task UpdateUserRolesFromLdapAsync_RemovesExistingRoles_BeforeAddingNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var oldRoleId = Guid.NewGuid();
        var newRoleId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            NormalizedEmail = "USER@EXAMPLE.COM",
            Status = "active",
            PasswordHash = "hashed_password",
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var oldRole = new Role
        {
            Id = oldRoleId,
            Name = "OldRole",
            Description = "Old role"
        };

        var newRole = new Role
        {
            Id = newRoleId,
            Name = "NewRole",
            Description = "New role"
        };

        var existingUserRole = new UserRole
        {
            UserId = userId,
            RoleId = oldRoleId,
            AssignedAt = DateTime.UtcNow
        };

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            SyncGroupsAsRoles = true,
            CreatedBy = userId
        };

        _context.Users.Add(user);
        _context.Set<Role>().AddRange(oldRole, newRole);
        _context.Set<UserRole>().Add(existingUserRole);
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var ldapGroups = new List<string>
        {
            "CN=NewRole,OU=Groups,DC=example,DC=com"
        };

        // Act
        await _sut.UpdateUserRolesFromLdapAsync(userId, configId, ldapGroups);

        // Assert
        var userRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .ToListAsync();

        userRoles.Should().HaveCount(1);
        userRoles[0].Role.Name.Should().Be("NewRole");
        userRoles.Should().NotContain(ur => ur.Role.Name == "OldRole");
    }

    [Fact]
    public async Task UpdateUserRolesFromLdapAsync_WhenSyncDisabled_DoesNothing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            NormalizedEmail = "USER@EXAMPLE.COM",
            Status = "active",
            PasswordHash = "hashed_password",
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Test LDAP",
            ServerUrl = "ldap.example.com",
            Port = 389,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            SyncGroupsAsRoles = false, // Sync disabled
            CreatedBy = userId
        };

        _context.Users.Add(user);
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var ldapGroups = new List<string>
        {
            "CN=Admins,OU=Groups,DC=example,DC=com"
        };

        // Act
        await _sut.UpdateUserRolesFromLdapAsync(userId, configId, ldapGroups);

        // Assert
        var userRoles = await _context.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .ToListAsync();

        userRoles.Should().BeEmpty();
    }

    // ====================
    // Configuration Response Mapping Tests
    // ====================

    [Fact]
    public async Task ConfigureLdapAsync_MapsAllPropertiesToResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new ConfigureLdapRequest
        {
            Name = "Complete LDAP",
            ServerUrl = "ldap.example.com",
            Port = 636,
            UseSsl = true,
            UseTls = false,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "password123",
            UserSearchFilter = "(uid={0})",
            UserSearchBase = "OU=Users,DC=example,DC=com",
            GroupSearchFilter = "(objectClass=groupOfNames)",
            GroupSearchBase = "OU=Groups,DC=example,DC=com",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "uid",
            GroupMembershipAttribute = "memberOf",
            EnableJitProvisioning = true,
            DefaultRoleId = Guid.NewGuid(),
            SyncGroupsAsRoles = true,
            UpdateUserOnLogin = true,
            EnableGroupSync = true,
            GroupSyncIntervalMinutes = 120,
            NestedGroupsEnabled = true
        };

        // Act
        var result = await _sut.ConfigureLdapAsync(request, userId);

        // Assert
        result.Name.Should().Be("Complete LDAP");
        result.ServerUrl.Should().Be("ldap.example.com");
        result.Port.Should().Be(636);
        result.UseSsl.Should().BeTrue();
        result.UseTls.Should().BeFalse();
        result.BaseDn.Should().Be("DC=example,DC=com");
        result.BindDn.Should().Be("CN=Service,DC=example,DC=com");
        result.UserSearchFilter.Should().Be("(uid={0})");
        result.UserSearchBase.Should().Be("OU=Users,DC=example,DC=com");
        result.GroupSearchFilter.Should().Be("(objectClass=groupOfNames)");
        result.GroupSearchBase.Should().Be("OU=Groups,DC=example,DC=com");
        result.EmailAttribute.Should().Be("mail");
        result.FirstNameAttribute.Should().Be("givenName");
        result.LastNameAttribute.Should().Be("sn");
        result.UsernameAttribute.Should().Be("uid");
        result.GroupMembershipAttribute.Should().Be("memberOf");
        result.EnableJitProvisioning.Should().BeTrue();
        result.DefaultRoleId.Should().Be(request.DefaultRoleId);
        result.SyncGroupsAsRoles.Should().BeTrue();
        result.UpdateUserOnLogin.Should().BeTrue();
        result.EnableGroupSync.Should().BeTrue();
        result.GroupSyncIntervalMinutes.Should().Be(120);
        result.NestedGroupsEnabled.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ====================
    // Edge Cases and Validation Tests
    // ====================

    [Fact]
    public async Task UpdateConfigurationAsync_WithNullValues_DoesNotUpdateNullFields()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@example.com", UserName = "admin", NormalizedUserName = "ADMIN", NormalizedEmail = "ADMIN@EXAMPLE.COM", Status = "active", PasswordHash = "hashed_password", SecurityStamp = Guid.NewGuid().ToString() };
        _context.Users.Add(user);

        var config = new LdapConfiguration
        {
            Id = configId,
            Name = "Original Name",
            ServerUrl = "ldap.example.com",
            Port = 389,
            UseSsl = false,
            BaseDn = "DC=example,DC=com",
            BindDn = "CN=Service,DC=example,DC=com",
            BindPassword = "encrypted_password",
            UserSearchFilter = "(sAMAccountName={0})",
            EmailAttribute = "mail",
            FirstNameAttribute = "givenName",
            LastNameAttribute = "sn",
            UsernameAttribute = "sAMAccountName",
            GroupMembershipAttribute = "memberOf",
            CreatedBy = userId
        };
        _context.Set<LdapConfiguration>().Add(config);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateLdapConfigRequest
        {
            Name = null, // Should not update
            Port = 636,  // Should update
            UseSsl = null // Should not update
        };

        // Act
        var result = await _sut.UpdateConfigurationAsync(configId, updateRequest);

        // Assert
        result.Name.Should().Be("Original Name"); // Not updated
        result.Port.Should().Be(636); // Updated
        result.UseSsl.Should().BeFalse(); // Not updated
    }
}
