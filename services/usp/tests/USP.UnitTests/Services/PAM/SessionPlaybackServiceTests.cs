using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.PAM;

namespace USP.UnitTests.Services.PAM;

/// <summary>
/// Unit tests for SessionPlaybackService
/// Tests timeline generation, metadata retrieval, and access control
/// </summary>
public class SessionPlaybackServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ISafeManagementService> _safeServiceMock;
    private readonly Mock<ILogger<SessionPlaybackService>> _loggerMock;
    private readonly SessionPlaybackService _service;

    public SessionPlaybackServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"SessionPlaybackTest_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup mocks
        _safeServiceMock = new Mock<ISafeManagementService>();
        _loggerMock = new Mock<ILogger<SessionPlaybackService>>();

        _service = new SessionPlaybackService(
            _context,
            _safeServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetPlaybackTimelineAsync_ValidSession_ReturnsTimelineWithAllCommands()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin",
            Platform = "PostgreSQL"
        };

        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow,
            Protocol = "Database",
            Platform = "PostgreSQL",
            Status = "completed",
            CommandCount = 3,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = session.StartTime.AddSeconds(10),
                CommandType = "SQL",
                Command = "SELECT * FROM users",
                Response = "100 rows returned",
                ResponseSize = 1000,
                Success = true,
                ExecutionTimeMs = 50,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = session.StartTime.AddSeconds(30),
                CommandType = "SQL",
                Command = "UPDATE users SET active = true",
                Response = "10 rows affected",
                ResponseSize = 50,
                Success = true,
                ExecutionTimeMs = 100,
                SequenceNumber = 2
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = session.StartTime.AddSeconds(60),
                CommandType = "SQL",
                Command = "COMMIT",
                Response = "Transaction committed",
                ResponseSize = 30,
                Success = true,
                ExecutionTimeMs = 20,
                SequenceNumber = 3
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act
        var timeline = await _service.GetPlaybackTimelineAsync(sessionId, userId);

        // Assert
        Assert.NotNull(timeline);
        Assert.Equal(sessionId, timeline.SessionId);
        Assert.Equal(3, timeline.TotalCommands);
        Assert.Equal(3, timeline.Entries.Count);

        // Verify timeline entries are in correct sequence
        Assert.Equal(1, timeline.Entries[0].SequenceNumber);
        Assert.Equal(2, timeline.Entries[1].SequenceNumber);
        Assert.Equal(3, timeline.Entries[2].SequenceNumber);

        // Verify inter-command delay is calculated
        Assert.Equal(TimeSpan.Zero, timeline.Entries[0].Metadata.TimeSincePreviousCommand);
        Assert.Equal(TimeSpan.FromSeconds(20), timeline.Entries[1].Metadata.TimeSincePreviousCommand);
        Assert.Equal(TimeSpan.FromSeconds(30), timeline.Entries[2].Metadata.TimeSincePreviousCommand);

        // Verify capabilities
        Assert.True(timeline.Capabilities.SupportsTimeline);
        Assert.True(timeline.Capabilities.SupportsFrameNavigation);
        Assert.True(timeline.Capabilities.SupportsSearch);
        Assert.True(timeline.Capabilities.SupportsExport);
        Assert.Equal("command-log", timeline.Capabilities.RecordingType);
    }

    [Fact]
    public async Task GetPlaybackTimelineAsync_SessionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentSessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetPlaybackTimelineAsync(nonExistentSessionId, userId));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task GetPlaybackTimelineAsync_UserDoesNotOwnSessionAndNoSafeAccess_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var owner = new ApplicationUser
        {
            Id = ownerId,
            Email = "owner@example.com",
            UserName = "owner"
        };

        var unauthorizedUser = new ApplicationUser
        {
            Id = unauthorizedUserId,
            Email = "unauthorized@example.com",
            UserName = "unauthorized"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = ownerId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = ownerId, // Session owned by different user
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            Protocol = "Database",
            Platform = "PostgreSQL",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        _context.Users.AddRange(owner, unauthorizedUser);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        await _context.SaveChangesAsync();

        // Mock safe service to return empty list (no access)
        _safeServiceMock
            .Setup(s => s.GetSafesAsync(unauthorizedUserId, It.IsAny<string?>()))
            .ReturnsAsync(new List<PrivilegedSafeDto>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetPlaybackTimelineAsync(sessionId, unauthorizedUserId));

        Assert.Contains("permission", exception.Message);
    }

    [Fact]
    public async Task GetPlaybackMetadataAsync_ValidSession_ReturnsMetadataWithStatistics()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "dbadmin"
        };

        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            Protocol = "Database",
            Platform = "MySQL",
            Status = "completed",
            CommandCount = 5,
            QueryCount = 3,
            SuspiciousActivityDetected = true,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = session.StartTime.AddMinutes(10),
                CommandType = "SQL",
                Command = "SELECT * FROM sensitive_data",
                Success = true,
                ExecutionTimeMs = 100,
                IsSuspicious = true,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = session.StartTime.AddMinutes(20),
                CommandType = "SQL",
                Command = "UPDATE config SET value = 'new'",
                Success = false,
                ErrorMessage = "Permission denied",
                ExecutionTimeMs = 50,
                SequenceNumber = 2
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = session.StartTime.AddMinutes(30),
                CommandType = "Shell",
                Command = "cat /etc/passwd",
                Success = true,
                ExecutionTimeMs = 200,
                SequenceNumber = 3
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act
        var metadata = await _service.GetPlaybackMetadataAsync(sessionId, userId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(sessionId, metadata.SessionId);
        Assert.Equal("dbadmin", metadata.AccountName);
        Assert.Equal("test@example.com", metadata.UserEmail);
        Assert.Equal("MySQL", metadata.Platform);
        Assert.True(metadata.HasSuspiciousActivity);
        Assert.NotNull(metadata.Duration);
        Assert.Equal(5, metadata.TotalCommands);
        Assert.Equal(3, metadata.TotalQueries);

        // Verify statistics
        Assert.NotNull(metadata.Statistics);
        Assert.Equal(2, metadata.Statistics.SuccessfulCommands);
        Assert.Equal(1, metadata.Statistics.FailedCommands);
        Assert.Equal(1, metadata.Statistics.SuspiciousCommands);
        Assert.Equal(2, metadata.Statistics.CommandTypeDistribution.Count);
        Assert.Equal(2, metadata.Statistics.CommandTypeDistribution["SQL"]);
        Assert.Equal(1, metadata.Statistics.CommandTypeDistribution["Shell"]);
    }

    [Fact]
    public async Task GetPlaybackSummariesAsync_UserHasAccessToMultipleSessions_ReturnsFilteredSummaries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var safeId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Shared Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "shared-admin"
        };

        // User's own session
        var userSession = new PrivilegedSession
        {
            Id = Guid.NewGuid(),
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1),
            Protocol = "SSH",
            Platform = "Linux",
            Status = "completed",
            CommandCount = 10,
            RecordingFormat = "command-log"
        };

        // Another user's session (user has safe access)
        var otherUserSession = new PrivilegedSession
        {
            Id = Guid.NewGuid(),
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = otherUserId,
            StartTime = DateTime.UtcNow.AddDays(-2),
            EndTime = DateTime.UtcNow.AddDays(-2).AddHours(2),
            Protocol = "Database",
            Platform = "PostgreSQL",
            Status = "completed",
            CommandCount = 25,
            SuspiciousActivityDetected = true,
            RecordingFormat = "command-log"
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.AddRange(userSession, otherUserSession);
        await _context.SaveChangesAsync();

        // Mock safe service to return accessible safe
        _safeServiceMock
            .Setup(s => s.GetSafesAsync(userId, It.IsAny<string?>()))
            .ReturnsAsync(new List<PrivilegedSafeDto>
            {
                new PrivilegedSafeDto
                {
                    Id = safeId,
                    Name = "Shared Safe"
                }
            });

        // Act
        var summaries = await _service.GetPlaybackSummariesAsync(userId);

        // Assert
        Assert.NotNull(summaries);
        Assert.Equal(2, summaries.Count); // Both sessions visible

        var userSummary = summaries.FirstOrDefault(s => s.SessionId == userSession.Id);
        Assert.NotNull(userSummary);
        Assert.Equal(10, userSummary.CommandCount);
        Assert.False(userSummary.HasSuspiciousActivity);

        var otherSummary = summaries.FirstOrDefault(s => s.SessionId == otherUserSession.Id);
        Assert.NotNull(otherSummary);
        Assert.Equal(25, otherSummary.CommandCount);
        Assert.True(otherSummary.HasSuspiciousActivity);
    }

    [Fact]
    public async Task GetPlaybackSummariesAsync_WithAccountIdFilter_ReturnsOnlyMatchingSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var safeId = Guid.NewGuid();
        var account1Id = Guid.NewGuid();
        var account2Id = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account1 = new PrivilegedAccount
        {
            Id = account1Id,
            SafeId = safeId,
            AccountName = "account1"
        };

        var account2 = new PrivilegedAccount
        {
            Id = account2Id,
            SafeId = safeId,
            AccountName = "account2"
        };

        var session1 = new PrivilegedSession
        {
            Id = Guid.NewGuid(),
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = account1Id,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddDays(-1),
            Protocol = "SSH",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        var session2 = new PrivilegedSession
        {
            Id = Guid.NewGuid(),
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = account2Id,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddDays(-1),
            Protocol = "Database",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.AddRange(account1, account2);
        _context.PrivilegedSessions.AddRange(session1, session2);
        await _context.SaveChangesAsync();

        _safeServiceMock
            .Setup(s => s.GetSafesAsync(userId, It.IsAny<string?>()))
            .ReturnsAsync(new List<PrivilegedSafeDto>
            {
                new PrivilegedSafeDto { Id = safeId }
            });

        // Act
        var summaries = await _service.GetPlaybackSummariesAsync(userId, accountId: account1Id);

        // Assert
        Assert.Single(summaries);
        Assert.Equal(session1.Id, summaries[0].SessionId);
        Assert.Equal(account1Id, summaries[0].AccountName == "account1" ? account1Id : account2Id);
    }

    [Fact]
    public async Task GetPlaybackFrameAsync_ValidTimestamp_ReturnsFrameWithCommandsUpToTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin",
            Platform = "PostgreSQL"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-10);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            EndTime = sessionStart.AddMinutes(10),
            Protocol = "Database",
            Platform = "PostgreSQL",
            Status = "completed",
            CommandCount = 4,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                CommandType = "SQL",
                Command = "SELECT * FROM users",
                Success = true,
                ExecutionTimeMs = 50,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(30),
                CommandType = "SQL",
                Command = "UPDATE users SET active = true WHERE id = 1",
                Success = true,
                ExecutionTimeMs = 100,
                SequenceNumber = 2
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(60),
                CommandType = "SQL",
                Command = "DELETE FROM logs WHERE created_at < NOW() - INTERVAL '30 days'",
                Success = true,
                ExecutionTimeMs = 200,
                SequenceNumber = 3
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(90),
                CommandType = "SQL",
                Command = "COMMIT",
                Success = true,
                ExecutionTimeMs = 20,
                SequenceNumber = 4
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act - Get frame at 45 seconds (should include first 2 commands only)
        var frame = await _service.GetPlaybackFrameAsync(sessionId, userId, TimeSpan.FromSeconds(45));

        // Assert
        Assert.NotNull(frame);
        Assert.Equal(sessionId, frame.SessionId);
        Assert.Equal(TimeSpan.FromSeconds(45), frame.RequestedTimestamp);
        Assert.Equal(2, frame.CommandsUpToFrame.Count); // Only first 2 commands

        // Verify commands are included
        Assert.Equal("SELECT * FROM users", frame.CommandsUpToFrame[0].Command);
        Assert.Equal("UPDATE users SET active = true WHERE id = 1", frame.CommandsUpToFrame[1].Command);

        // Verify current command context
        Assert.NotNull(frame.Context.CurrentCommand);
        Assert.Equal(2, frame.Context.CurrentCommand.SequenceNumber);
        Assert.Equal("UPDATE users SET active = true WHERE id = 1", frame.Context.CurrentCommand.Command);

        // Verify next command context
        Assert.NotNull(frame.Context.NextCommand);
        Assert.Equal(3, frame.Context.NextCommand.SequenceNumber);
        Assert.Equal("DELETE FROM logs WHERE created_at < NOW() - INTERVAL '30 days'", frame.Context.NextCommand.Command);
    }

    [Fact]
    public async Task GetPlaybackFrameAsync_TimestampAtSessionStart_ReturnsEmptyFrame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-5);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            Protocol = "SSH",
            Status = "active",
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(30),
                Command = "ls -la",
                Success = true,
                SequenceNumber = 1
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act - Get frame at timestamp 0 (session start)
        var frame = await _service.GetPlaybackFrameAsync(sessionId, userId, TimeSpan.Zero);

        // Assert
        Assert.NotNull(frame);
        Assert.Empty(frame.CommandsUpToFrame); // No commands executed yet
        Assert.Null(frame.Context.CurrentCommand); // No current command
        Assert.Null(frame.Context.PreviousCommand); // No previous command
        Assert.NotNull(frame.Context.NextCommand); // First command is next
        Assert.Equal(1, frame.Context.NextCommand.SequenceNumber);
    }

    [Fact]
    public async Task SearchPlaybackAsync_LiteralSearch_FindsMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-5);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            Protocol = "Database",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "SELECT * FROM users WHERE email LIKE '%@example.com'",
                Response = "Found 100 users",
                Success = true,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(20),
                Command = "UPDATE users SET verified = true",
                Response = "Updated 50 users",
                Success = true,
                SequenceNumber = 2
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(30),
                Command = "DELETE FROM sessions WHERE expired = true",
                Response = "Deleted 25 sessions",
                Success = true,
                SequenceNumber = 3
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act - Search for "users" (case-insensitive)
        var result = await _service.SearchPlaybackAsync(sessionId, userId, "users", new PlaybackSearchOptions
        {
            CaseSensitive = false,
            UseRegex = false,
            SearchCommands = true,
            SearchResponses = true,
            ContextCharacters = 20
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("users", result.SearchTerm);
        Assert.True(result.TotalMatches >= 3); // Should find "users" in multiple commands and responses
        Assert.NotEmpty(result.Matches);

        // Verify at least one match has context
        var firstMatch = result.Matches.First();
        Assert.NotNull(firstMatch.Context);
        Assert.Contains("users", firstMatch.Context, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchPlaybackAsync_RegexSearch_FindsPatternMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-5);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            Protocol = "SSH",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "rm -rf /tmp/file1.txt",
                Success = true,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(20),
                Command = "rm -rf /tmp/file2.log",
                Success = true,
                SequenceNumber = 2
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(30),
                Command = "ls /home/user",
                Success = true,
                SequenceNumber = 3
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act - Search for files with regex pattern (file*.txt or file*.log)
        var result = await _service.SearchPlaybackAsync(sessionId, userId, @"file\d+\.(txt|log)", new PlaybackSearchOptions
        {
            CaseSensitive = false,
            UseRegex = true,
            SearchCommands = true,
            SearchResponses = false,
            ContextCharacters = 30
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalMatches); // Should match file1.txt and file2.log
        Assert.Equal(2, result.Matches.Count);
        Assert.All(result.Matches, match =>
            Assert.Contains("file", match.Context, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchPlaybackAsync_CaseSensitiveSearch_RespectsCase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-5);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            Protocol = "Database",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "SELECT * FROM Users",
                Success = true,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(20),
                Command = "SELECT * FROM users",
                Success = true,
                SequenceNumber = 2
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act - Case-sensitive search for "Users" (capital U)
        var result = await _service.SearchPlaybackAsync(sessionId, userId, "Users", new PlaybackSearchOptions
        {
            CaseSensitive = true,
            UseRegex = false,
            SearchCommands = true
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalMatches); // Should only match "Users", not "users"
        Assert.Single(result.Matches);
        Assert.Contains("Users", result.Matches[0].Context);
    }

    [Fact]
    public async Task SearchPlaybackAsync_SearchOnlyInResponses_IgnoresCommands()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-5);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            Protocol = "Database",
            Status = "completed",
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "SELECT COUNT(*) FROM errors",
                Response = "Count: 42",
                Success = true,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(20),
                Command = "TRUNCATE TABLE logs",
                Response = "No errors occurred",
                Success = true,
                SequenceNumber = 2
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act - Search only in responses
        var result = await _service.SearchPlaybackAsync(sessionId, userId, "errors", new PlaybackSearchOptions
        {
            CaseSensitive = false,
            SearchCommands = false,
            SearchResponses = true
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalMatches); // Should only find "errors" in response, not command
        Assert.Single(result.Matches);
        Assert.Equal(2, result.Matches[0].SequenceNumber); // Match is in second command's response
    }

    [Fact]
    public async Task ExportSessionAsync_JsonFormat_ReturnsValidJsonExport()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "dbadmin",
            Platform = "PostgreSQL"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-10);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            EndTime = sessionStart.AddMinutes(5),
            Protocol = "Database",
            Platform = "PostgreSQL",
            Status = "completed",
            CommandCount = 2,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                CommandType = "SQL",
                Command = "SELECT * FROM users",
                Response = "10 rows returned",
                Success = true,
                ExecutionTimeMs = 50,
                SequenceNumber = 1
            },
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(30),
                CommandType = "SQL",
                Command = "COMMIT",
                Response = "Transaction committed",
                Success = true,
                ExecutionTimeMs = 10,
                SequenceNumber = 2
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act
        var export = await _service.ExportSessionAsync(sessionId, userId, PlaybackExportFormat.Json);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("application/json", export.MimeType);
        Assert.Contains(".json", export.FileName);
        Assert.NotNull(export.Data);
        Assert.True(export.Data.Length > 0);

        // Verify JSON is parseable
        var jsonString = System.Text.Encoding.UTF8.GetString(export.Data);
        Assert.NotNull(System.Text.Json.JsonDocument.Parse(jsonString));
    }

    [Fact]
    public async Task ExportSessionAsync_CsvFormat_ReturnsValidCsvExport()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "admin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-10);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            EndTime = sessionStart.AddMinutes(5),
            Protocol = "SSH",
            Status = "completed",
            CommandCount = 1,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "ls -la",
                Response = "total 48\ndrwxr-xr-x 12 user staff 384 Dec 26 10:00 .",
                Success = true,
                SequenceNumber = 1
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act
        var export = await _service.ExportSessionAsync(sessionId, userId, PlaybackExportFormat.Csv);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("text/csv", export.MimeType);
        Assert.Contains(".csv", export.FileName);
        Assert.NotNull(export.Data);
        Assert.True(export.Data.Length > 0);

        // Verify CSV contains header
        var csvString = System.Text.Encoding.UTF8.GetString(export.Data);
        Assert.Contains("Sequence,Timestamp,Command Type,Command,Response,Success,Execution Time (ms)", csvString);
    }

    [Fact]
    public async Task ExportSessionAsync_HtmlFormat_ReturnsValidHtmlExport()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "webadmin",
            Platform = "Linux"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-10);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            EndTime = sessionStart.AddMinutes(5),
            Protocol = "SSH",
            Platform = "Linux",
            Status = "completed",
            CommandCount = 1,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "sudo systemctl restart nginx",
                Success = true,
                SequenceNumber = 1
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act
        var export = await _service.ExportSessionAsync(sessionId, userId, PlaybackExportFormat.Html);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("text/html", export.MimeType);
        Assert.Contains(".html", export.FileName);
        Assert.NotNull(export.Data);
        Assert.True(export.Data.Length > 0);

        // Verify HTML structure
        var htmlString = System.Text.Encoding.UTF8.GetString(export.Data);
        Assert.Contains("<!DOCTYPE html>", htmlString);
        Assert.Contains("<html>", htmlString);
        Assert.Contains("Session Playback Export", htmlString);
        Assert.Contains("webadmin", htmlString);
    }

    [Fact]
    public async Task ExportSessionAsync_TextFormat_ReturnsValidTextExport()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var safeId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser"
        };

        var safe = new PrivilegedSafe
        {
            Id = safeId,
            Name = "Test Safe",
            OwnerId = userId
        };

        var account = new PrivilegedAccount
        {
            Id = accountId,
            SafeId = safeId,
            AccountName = "sysadmin"
        };

        var sessionStart = DateTime.UtcNow.AddMinutes(-10);
        var session = new PrivilegedSession
        {
            Id = sessionId,
            AccountCheckoutId = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            StartTime = sessionStart,
            EndTime = sessionStart.AddMinutes(5),
            Protocol = "RDP",
            Status = "completed",
            CommandCount = 1,
            RecordingFormat = "command-log"
        };

        var commands = new List<SessionCommand>
        {
            new SessionCommand
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ExecutedAt = sessionStart.AddSeconds(10),
                Command = "Get-Service | Where-Object {$_.Status -eq 'Running'}",
                Response = "Status: Running, Name: WinRM",
                Success = true,
                SequenceNumber = 1
            }
        };

        _context.Users.Add(user);
        _context.PrivilegedSafes.Add(safe);
        _context.PrivilegedAccounts.Add(account);
        _context.PrivilegedSessions.Add(session);
        _context.SessionCommands.AddRange(commands);
        await _context.SaveChangesAsync();

        // Act
        var export = await _service.ExportSessionAsync(sessionId, userId, PlaybackExportFormat.Text);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("text/plain", export.MimeType);
        Assert.Contains(".txt", export.FileName);
        Assert.NotNull(export.Data);
        Assert.True(export.Data.Length > 0);

        // Verify text content
        var textString = System.Text.Encoding.UTF8.GetString(export.Data);
        Assert.Contains("SESSION PLAYBACK EXPORT", textString);
        Assert.Contains("sysadmin", textString);
        Assert.Contains("Get-Service", textString);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
