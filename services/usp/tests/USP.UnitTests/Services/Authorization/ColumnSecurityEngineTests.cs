using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Authorization;
using Xunit;

namespace USP.UnitTests.Services.Authorization;

public class ColumnSecurityEngineTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ColumnSecurityEngine>> _loggerMock;
    private readonly ColumnSecurityEngine _engine;
    private readonly Guid _testUserId;
    private readonly Guid _testRoleId;

    public ColumnSecurityEngineTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<ColumnSecurityEngine>>();
        _engine = new ColumnSecurityEngine(_context, _loggerMock.Object);

        _testUserId = Guid.NewGuid();
        _testRoleId = Guid.NewGuid();
        SeedTestData();
    }

    private void SeedTestData()
    {
        var role = new Role
        {
            Id = _testRoleId,
            Name = "DataAnalyst",
            Description = "Data Analyst",
            IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow
        };

        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = "test.user",
            Email = "test@example.com",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = DateTime.UtcNow
        };

        _context.Roles.Add(role);
        _context.Users.Add(user);
        _context.UserRoles.Add(userRole);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CheckColumnAccessAsync_ShouldAllowAll_WhenNoRulesDefined()
    {
        // Arrange
        var request = new ColumnAccessRequest
        {
            UserId = _testUserId,
            TableName = "users",
            RequestedColumns = new List<string> { "id", "name", "email" },
            Operation = "read"
        };

        // Act
        var result = await _engine.CheckColumnAccessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AllowedColumns.Should().BeEquivalentTo(new[] { "id", "name", "email" });
        result.DeniedColumns.Should().BeEmpty();
        result.ColumnRestrictions.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckColumnAccessAsync_ShouldDenyColumn_WhenRuleDenies()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "ssn",
            Operation = "read",
            RestrictionType = "deny",
            DeniedRoles = new List<string> { "DataAnalyst" }
        });

        var request = new ColumnAccessRequest
        {
            UserId = _testUserId,
            TableName = "users",
            RequestedColumns = new List<string> { "id", "name", "ssn" },
            Operation = "read"
        };

        // Act
        var result = await _engine.CheckColumnAccessAsync(request);

        // Assert
        result.AllowedColumns.Should().BeEquivalentTo(new[] { "id", "name" });
        result.DeniedColumns.Should().Contain("ssn");
    }

    [Fact]
    public async Task CheckColumnAccessAsync_ShouldMaskColumn_WhenRuleMasks()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "email",
            Operation = "read",
            RestrictionType = "mask",
            MaskingPattern = "***",
            AllowedRoles = new List<string> { "DataAnalyst" }
        });

        var request = new ColumnAccessRequest
        {
            UserId = _testUserId,
            TableName = "users",
            RequestedColumns = new List<string> { "id", "email" },
            Operation = "read"
        };

        // Act
        var result = await _engine.CheckColumnAccessAsync(request);

        // Assert
        result.AllowedColumns.Should().Contain("email");
        result.ColumnRestrictions.Should().ContainKey("email");
        result.ColumnRestrictions["email"].Should().Be("mask");
    }

    [Fact]
    public async Task ApplyMaskingAsync_ShouldMaskEmailCorrectly()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "email",
            Operation = "read",
            RestrictionType = "mask",
            AllowedRoles = new List<string> { "DataAnalyst" }
        });

        var data = new Dictionary<string, object>
        {
            { "id", 123 },
            { "name", "John Doe" },
            { "email", "john.doe@example.com" }
        };

        // Act
        var result = await _engine.ApplyMaskingAsync(_testUserId, "users", data);

        // Assert
        result.Should().ContainKey("id");
        result.Should().ContainKey("name");
        result.Should().ContainKey("email");
        result["id"].Should().Be(123);
        result["name"].Should().Be("John Doe");
        result["email"].ToString().Should().Contain("***");
        result["email"].ToString().Should().Contain("@example.com");
    }

    [Fact]
    public async Task ApplyMaskingAsync_ShouldRedactColumn()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "ssn",
            Operation = "read",
            RestrictionType = "redact",
            AllowedRoles = new List<string> { "DataAnalyst" }
        });

        var data = new Dictionary<string, object>
        {
            { "id", 123 },
            { "ssn", "123-45-6789" }
        };

        // Act
        var result = await _engine.ApplyMaskingAsync(_testUserId, "users", data);

        // Assert
        result.Should().ContainKey("ssn");
        result["ssn"].Should().Be("[REDACTED]");
    }

    [Fact]
    public async Task ApplyMaskingAsync_ShouldTokenizeColumn()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "credit_card",
            Operation = "read",
            RestrictionType = "tokenize",
            AllowedRoles = new List<string> { "DataAnalyst" }
        });

        var data = new Dictionary<string, object>
        {
            { "id", 123 },
            { "credit_card", "4532-1234-5678-9010" }
        };

        // Act
        var result = await _engine.ApplyMaskingAsync(_testUserId, "users", data);

        // Assert
        result.Should().ContainKey("credit_card");
        result["credit_card"].ToString().Should().StartWith("TOK_");
    }

    [Fact]
    public async Task ApplyMaskingAsync_ShouldRemoveDeniedColumns()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "salary",
            Operation = "read",
            RestrictionType = "deny",
            DeniedRoles = new List<string> { "DataAnalyst" }
        });

        var data = new Dictionary<string, object>
        {
            { "id", 123 },
            { "name", "John Doe" },
            { "salary", 100000 }
        };

        // Act
        var result = await _engine.ApplyMaskingAsync(_testUserId, "users", data);

        // Assert
        result.Should().ContainKey("id");
        result.Should().ContainKey("name");
        result.Should().NotContainKey("salary");
    }

    [Fact]
    public async Task GetAllowedColumnsAsync_ShouldReturnAllowedColumns()
    {
        // Arrange
        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "name",
            Operation = "read",
            RestrictionType = "allow",
            AllowedRoles = new List<string> { "DataAnalyst" }
        });

        await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "ssn",
            Operation = "read",
            RestrictionType = "deny",
            DeniedRoles = new List<string> { "DataAnalyst" }
        });

        // Act
        var allowedColumns = await _engine.GetAllowedColumnsAsync(_testUserId, "users", "read");

        // Assert
        allowedColumns.Should().Contain("name");
        allowedColumns.Should().NotContain("ssn");
    }

    [Fact]
    public async Task CreateColumnRuleAsync_ShouldCreateRule()
    {
        // Arrange
        var request = new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "email",
            Operation = "read",
            RestrictionType = "mask",
            MaskingPattern = "***",
            AllowedRoles = new List<string> { "DataAnalyst" },
            Priority = 100
        };

        // Act
        var rule = await _engine.CreateColumnRuleAsync(request);

        // Assert
        rule.Should().NotBeNull();
        rule.Id.Should().NotBeEmpty();
        rule.TableName.Should().Be("users");
        rule.ColumnName.Should().Be("email");
        rule.RestrictionType.Should().Be("mask");
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteColumnRuleAsync_ShouldDeleteRule()
    {
        // Arrange
        var rule = await _engine.CreateColumnRuleAsync(new CreateColumnRuleRequest
        {
            TableName = "users",
            ColumnName = "test",
            Operation = "read",
            RestrictionType = "deny"
        });

        // Act
        var deleted = await _engine.DeleteColumnRuleAsync(rule.Id);

        // Assert
        deleted.Should().BeTrue();

        var rules = await _engine.GetColumnRulesAsync("users");
        rules.Should().NotContain(r => r.Id == rule.Id);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
