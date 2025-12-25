using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.Audit;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

public class SessionRecordingService : ISessionRecordingService
{
    private readonly ApplicationDbContext _context;
    private readonly ISafeManagementService _safeService;
    private readonly IAuditService _auditService;
    private readonly ILogger<SessionRecordingService> _logger;

    // Suspicious command patterns
    private static readonly string[] SuspiciousPatterns = new[]
    {
        "DROP DATABASE", "DROP TABLE", "DELETE FROM", "TRUNCATE",
        "CREATE USER", "ALTER USER", "GRANT ALL", "GRANT SUPER",
        "sudo rm -rf", "rm -rf /", "chmod 777", "passwd",
        "curl", "wget", "nc -", "netcat", "/bin/sh", "/bin/bash",
        "SELECT * FROM", "UNION SELECT", "'; DROP", "1=1", "OR 1=1"
    };

    public SessionRecordingService(
        ApplicationDbContext context,
        ISafeManagementService safeService,
        IAuditService auditService,
        ILogger<SessionRecordingService> logger)
    {
        _context = context;
        _safeService = safeService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<SessionRecordingDto> StartSessionAsync(
        Guid checkoutId,
        Guid userId,
        StartSessionRequest request)
    {
        // Get checkout to validate
        var checkout = await _context.AccountCheckouts
            .Include(c => c.Account)
                .ThenInclude(a => a.Safe)
            .FirstOrDefaultAsync(c => c.Id == checkoutId && c.UserId == userId);

        if (checkout == null)
            throw new InvalidOperationException("Invalid checkout");

        if (checkout.Status != "active")
            throw new InvalidOperationException("Checkout is not active");

        // Create session
        var session = new PrivilegedSession
        {
            Id = Guid.NewGuid(),
            AccountCheckoutId = checkoutId,
            AccountId = checkout.AccountId,
            UserId = userId,
            StartTime = DateTime.UtcNow,
            Protocol = request.Protocol,
            Platform = request.Platform,
            HostAddress = request.HostAddress,
            Port = request.Port,
            SessionType = request.SessionType,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            Metadata = request.Metadata,
            Status = "active"
        };

        _context.PrivilegedSessions.Add(session);
        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "session_started",
            "PrivilegedSession",
            session.Id.ToString(),
            null,
            new
            {
                checkoutId,
                accountId = checkout.AccountId,
                protocol = request.Protocol,
                platform = request.Platform,
                sessionType = request.SessionType
            });

        _logger.LogInformation(
            "Privileged session started: {SessionId} for checkout {CheckoutId}",
            session.Id,
            checkoutId);

        return await MapToDto(session);
    }

    public async Task<bool> EndSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return false;

        // Verify ownership or admin
        if (session.UserId != userId)
        {
            var hasAccess = await _safeService.HasSafeAccessAsync(
                session.Account.SafeId,
                userId,
                "manage");

            if (!hasAccess)
                return false;
        }

        if (session.Status != "active")
            return false;

        session.EndTime = DateTime.UtcNow;
        session.Status = "completed";

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            userId,
            "session_ended",
            "PrivilegedSession",
            sessionId.ToString(),
            null,
            new
            {
                duration = (session.EndTime.Value - session.StartTime).TotalMinutes,
                commandCount = session.CommandCount,
                suspiciousActivity = session.SuspiciousActivityDetected
            });

        _logger.LogInformation(
            "Privileged session ended: {SessionId}",
            sessionId);

        return true;
    }

    public async Task<SessionCommandDto> RecordCommandAsync(
        Guid sessionId,
        RecordCommandRequest request)
    {
        var session = await _context.PrivilegedSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            throw new InvalidOperationException("Session not found");

        if (session.Status != "active")
            throw new InvalidOperationException("Session is not active");

        // Check for suspicious patterns
        var isSuspicious = false;
        var suspiciousReason = string.Empty;

        foreach (var pattern in SuspiciousPatterns)
        {
            if (request.Command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                isSuspicious = true;
                suspiciousReason = $"Matched suspicious pattern: {pattern}";
                break;
            }
        }

        // Create command record
        var command = new SessionCommand
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ExecutedAt = DateTime.UtcNow,
            CommandType = request.CommandType,
            Command = request.Command,
            Response = request.Response,
            ResponseSize = request.ResponseSize,
            Success = request.Success,
            ErrorMessage = request.ErrorMessage,
            ExecutionTimeMs = request.ExecutionTimeMs,
            IsSuspicious = isSuspicious,
            SuspiciousReason = suspiciousReason,
            SequenceNumber = session.CommandCount + 1
        };

        _context.SessionCommands.Add(command);

        // Update session statistics
        session.CommandCount++;
        if (request.CommandType.Equals("SQL", StringComparison.OrdinalIgnoreCase))
        {
            session.QueryCount++;
        }

        if (isSuspicious)
        {
            session.SuspiciousActivityDetected = true;

            var details = new List<object>();
            if (!string.IsNullOrEmpty(session.SuspiciousActivityDetails))
            {
                // Append to existing details
                details.Add(new
                {
                    timestamp = DateTime.UtcNow,
                    sequenceNumber = command.SequenceNumber,
                    command = request.Command,
                    reason = suspiciousReason
                });
            }
            else
            {
                details.Add(new
                {
                    timestamp = DateTime.UtcNow,
                    sequenceNumber = command.SequenceNumber,
                    command = request.Command,
                    reason = suspiciousReason
                });
            }

            session.SuspiciousActivityDetails = System.Text.Json.JsonSerializer.Serialize(details);

            // Log suspicious activity
            _logger.LogWarning(
                "Suspicious command detected in session {SessionId}: {Command} - {Reason}",
                sessionId,
                request.Command,
                suspiciousReason);
        }

        await _context.SaveChangesAsync();

        return new SessionCommandDto
        {
            Id = command.Id,
            SessionId = command.SessionId,
            ExecutedAt = command.ExecutedAt,
            CommandType = command.CommandType,
            Command = command.Command,
            Response = command.Response,
            ResponseSize = command.ResponseSize,
            Success = command.Success,
            ErrorMessage = command.ErrorMessage,
            ExecutionTimeMs = command.ExecutionTimeMs,
            IsSuspicious = command.IsSuspicious,
            SuspiciousReason = command.SuspiciousReason,
            SequenceNumber = command.SequenceNumber
        };
    }

    public async Task<SessionRecordingDto?> GetSessionByIdAsync(Guid sessionId, Guid userId)
    {
        var session = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
                .ThenInclude(a => a.Safe)
            .Include(s => s.Checkout)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return null;

        // Check access
        var hasAccess = await _safeService.HasSafeAccessAsync(
            session.Account.SafeId,
            userId,
            "read");

        if (!hasAccess && session.UserId != userId)
            return null;

        return await MapToDto(session);
    }

    public async Task<List<SessionRecordingDto>> GetUserSessionsAsync(Guid userId, int? limit = 50)
    {
        var sessions = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
            .Include(s => s.Checkout)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartTime)
            .Take(limit ?? 50)
            .ToListAsync();

        var dtos = new List<SessionRecordingDto>();
        foreach (var session in sessions)
        {
            dtos.Add(await MapToDto(session));
        }

        return dtos;
    }

    public async Task<List<SessionRecordingDto>> GetAccountSessionsAsync(
        Guid accountId,
        Guid userId,
        int? limit = 50)
    {
        // Get account to check access
        var account = await _context.PrivilegedAccounts.FindAsync(accountId);
        if (account == null)
            return new List<SessionRecordingDto>();

        // Check if user has read access to the safe
        var hasAccess = await _safeService.HasSafeAccessAsync(account.SafeId, userId, "read");
        if (!hasAccess)
            return new List<SessionRecordingDto>();

        var sessions = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
            .Include(s => s.Checkout)
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.StartTime)
            .Take(limit ?? 50)
            .ToListAsync();

        var dtos = new List<SessionRecordingDto>();
        foreach (var session in sessions)
        {
            dtos.Add(await MapToDto(session));
        }

        return dtos;
    }

    public async Task<List<SessionRecordingDto>> GetActiveSessionsAsync(Guid userId)
    {
        // Get all safes accessible by user
        var safes = await _safeService.GetSafesAsync(userId);
        var safeIds = safes.Select(s => s.Id).ToList();

        var sessions = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
                .ThenInclude(a => a.Safe)
            .Include(s => s.Checkout)
            .Where(s => s.Status == "active" &&
                       (s.UserId == userId || safeIds.Contains(s.Account.SafeId)))
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();

        var dtos = new List<SessionRecordingDto>();
        foreach (var session in sessions)
        {
            dtos.Add(await MapToDto(session));
        }

        return dtos;
    }

    public async Task<List<SessionCommandDto>> GetSessionCommandsAsync(
        Guid sessionId,
        Guid userId,
        int? limit = 100)
    {
        // Get session to check access
        var session = await _context.PrivilegedSessions
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return new List<SessionCommandDto>();

        // Check access
        var hasAccess = await _safeService.HasSafeAccessAsync(
            session.Account.SafeId,
            userId,
            "read");

        if (!hasAccess && session.UserId != userId)
            return new List<SessionCommandDto>();

        var commands = await _context.SessionCommands
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.SequenceNumber)
            .Take(limit ?? 100)
            .ToListAsync();

        return commands.Select(c => new SessionCommandDto
        {
            Id = c.Id,
            SessionId = c.SessionId,
            ExecutedAt = c.ExecutedAt,
            CommandType = c.CommandType,
            Command = c.Command,
            Response = c.Response,
            ResponseSize = c.ResponseSize,
            Success = c.Success,
            ErrorMessage = c.ErrorMessage,
            ExecutionTimeMs = c.ExecutionTimeMs,
            IsSuspicious = c.IsSuspicious,
            SuspiciousReason = c.SuspiciousReason,
            SequenceNumber = c.SequenceNumber
        }).ToList();
    }

    public async Task<bool> TerminateSessionAsync(Guid sessionId, Guid adminUserId, string reason)
    {
        var session = await _context.PrivilegedSessions
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return false;

        // Check if user has manage access to the safe
        var hasAccess = await _safeService.HasSafeAccessAsync(
            session.Account.SafeId,
            adminUserId,
            "manage");

        if (!hasAccess)
            return false;

        if (session.Status != "active")
            return false;

        session.EndTime = DateTime.UtcNow;
        session.Status = "terminated";

        await _context.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(
            adminUserId,
            "session_terminated",
            "PrivilegedSession",
            sessionId.ToString(),
            null,
            new
            {
                reason,
                originalUserId = session.UserId,
                duration = (session.EndTime.Value - session.StartTime).TotalMinutes
            });

        _logger.LogWarning(
            "Privileged session terminated by admin {AdminUserId}: {SessionId} - {Reason}",
            adminUserId,
            sessionId,
            reason);

        return true;
    }

    public async Task<List<SessionRecordingDto>> GetSuspiciousSessionsAsync(Guid userId)
    {
        // Get all safes accessible by user
        var safes = await _safeService.GetSafesAsync(userId);
        var safeIds = safes.Select(s => s.Id).ToList();

        var sessions = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
                .ThenInclude(a => a.Safe)
            .Include(s => s.Checkout)
            .Where(s => s.SuspiciousActivityDetected &&
                       (s.UserId == userId || safeIds.Contains(s.Account.SafeId)))
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();

        var dtos = new List<SessionRecordingDto>();
        foreach (var session in sessions)
        {
            dtos.Add(await MapToDto(session));
        }

        return dtos;
    }

    public async Task<SessionStatisticsDto> GetSessionStatisticsAsync(Guid userId)
    {
        // Get all safes accessible by user
        var safes = await _safeService.GetSafesAsync(userId);
        var safeIds = safes.Select(s => s.Id).ToList();

        var sessions = await _context.PrivilegedSessions
            .Include(s => s.User)
            .Include(s => s.Account)
            .Where(s => s.UserId == userId || safeIds.Contains(s.Account.SafeId))
            .ToListAsync();

        var totalSessions = sessions.Count;
        var activeSessions = sessions.Count(s => s.Status == "active");
        var completedSessions = sessions.Count(s => s.Status == "completed");
        var terminatedSessions = sessions.Count(s => s.Status == "terminated");
        var suspiciousSessions = sessions.Count(s => s.SuspiciousActivityDetected);
        var totalCommands = sessions.Sum(s => s.CommandCount);
        var totalQueries = sessions.Sum(s => s.QueryCount);
        var totalRecordingSize = sessions.Sum(s => s.RecordingSize);

        var completedSessionsWithDuration = sessions
            .Where(s => s.EndTime.HasValue)
            .ToList();

        var avgDuration = completedSessionsWithDuration.Any()
            ? TimeSpan.FromMinutes(
                completedSessionsWithDuration.Average(s =>
                    (s.EndTime!.Value - s.StartTime).TotalMinutes))
            : TimeSpan.Zero;

        var sessionsByProtocol = sessions
            .GroupBy(s => s.Protocol)
            .Select(g => new SessionsByProtocolDto
            {
                Protocol = g.Key,
                Count = g.Count(),
                SuspiciousCount = g.Count(s => s.SuspiciousActivityDetected)
            })
            .ToList();

        var sessionsByPlatform = sessions
            .GroupBy(s => s.Platform)
            .Select(g => new SessionsByPlatformDto
            {
                Platform = g.Key,
                Count = g.Count(),
                TotalCommands = g.Sum(s => s.CommandCount)
            })
            .ToList();

        var topUsers = sessions
            .GroupBy(s => new { s.UserId, s.User.Email })
            .Select(g => new TopUserSessionsDto
            {
                UserId = g.Key.UserId,
                UserEmail = g.Key.Email ?? string.Empty,
                SessionCount = g.Count(),
                SuspiciousSessionCount = g.Count(s => s.SuspiciousActivityDetected)
            })
            .OrderByDescending(u => u.SessionCount)
            .Take(10)
            .ToList();

        return new SessionStatisticsDto
        {
            TotalSessions = totalSessions,
            ActiveSessions = activeSessions,
            CompletedSessions = completedSessions,
            TerminatedSessions = terminatedSessions,
            SuspiciousSessions = suspiciousSessions,
            TotalCommands = totalCommands,
            TotalQueries = totalQueries,
            TotalRecordingSize = totalRecordingSize,
            AverageSessionDuration = avgDuration,
            SessionsByProtocol = sessionsByProtocol,
            SessionsByPlatform = sessionsByPlatform,
            TopUsersBySessions = topUsers
        };
    }

    public async Task<List<SessionCommandDto>> SearchCommandsAsync(
        Guid userId,
        string searchTerm,
        int? limit = 50)
    {
        // Get all safes accessible by user
        var safes = await _safeService.GetSafesAsync(userId);
        var safeIds = safes.Select(s => s.Id).ToList();

        var commands = await _context.SessionCommands
            .Include(c => c.Session)
                .ThenInclude(s => s.Account)
            .Where(c => (c.Session.UserId == userId || safeIds.Contains(c.Session.Account.SafeId)) &&
                       (c.Command.Contains(searchTerm) || c.Response!.Contains(searchTerm)))
            .OrderByDescending(c => c.ExecutedAt)
            .Take(limit ?? 50)
            .ToListAsync();

        return commands.Select(c => new SessionCommandDto
        {
            Id = c.Id,
            SessionId = c.SessionId,
            ExecutedAt = c.ExecutedAt,
            CommandType = c.CommandType,
            Command = c.Command,
            Response = c.Response,
            ResponseSize = c.ResponseSize,
            Success = c.Success,
            ErrorMessage = c.ErrorMessage,
            ExecutionTimeMs = c.ExecutionTimeMs,
            IsSuspicious = c.IsSuspicious,
            SuspiciousReason = c.SuspiciousReason,
            SequenceNumber = c.SequenceNumber
        }).ToList();
    }

    private async Task<SessionRecordingDto> MapToDto(PrivilegedSession session)
    {
        TimeSpan? duration = null;
        if (session.EndTime.HasValue)
        {
            duration = session.EndTime.Value - session.StartTime;
        }

        return new SessionRecordingDto
        {
            Id = session.Id,
            AccountCheckoutId = session.AccountCheckoutId,
            AccountId = session.AccountId,
            UserId = session.UserId,
            UserEmail = session.User?.Email ?? string.Empty,
            AccountName = session.Account?.AccountName ?? string.Empty,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Protocol = session.Protocol,
            Platform = session.Platform,
            HostAddress = session.HostAddress,
            Port = session.Port,
            RecordingPath = session.RecordingPath,
            RecordingSize = session.RecordingSize,
            SessionType = session.SessionType,
            CommandCount = session.CommandCount,
            QueryCount = session.QueryCount,
            SuspiciousActivityDetected = session.SuspiciousActivityDetected,
            SuspiciousActivityDetails = session.SuspiciousActivityDetails,
            Status = session.Status,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            Duration = duration
        };
    }
}
