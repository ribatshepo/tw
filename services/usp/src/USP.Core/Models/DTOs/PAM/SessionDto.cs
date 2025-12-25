namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Request to start a new privileged session
/// </summary>
public class StartSessionRequest
{
    public string Protocol { get; set; } = string.Empty; // SSH, RDP, Database, Web
    public string Platform { get; set; } = string.Empty; // PostgreSQL, MySQL, Windows, Linux
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string SessionType { get; set; } = "interactive"; // interactive, automated, query_only
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; } // JSON: additional session metadata
}

/// <summary>
/// Session recording details
/// </summary>
public class SessionRecordingDto
{
    public Guid Id { get; set; }
    public Guid AccountCheckoutId { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string? RecordingPath { get; set; }
    public long RecordingSize { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public int CommandCount { get; set; }
    public int QueryCount { get; set; }
    public bool SuspiciousActivityDetected { get; set; }
    public string? SuspiciousActivityDetails { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Request to record a command in a session
/// </summary>
public class RecordCommandRequest
{
    public string CommandType { get; set; } = string.Empty; // SQL, Shell, PowerShell, API
    public string Command { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int ResponseSize { get; set; } = 0;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; } = 0;
}

/// <summary>
/// Session command details
/// </summary>
public class SessionCommandDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int ResponseSize { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
    public int SequenceNumber { get; set; }
}

/// <summary>
/// Session statistics
/// </summary>
public class SessionStatisticsDto
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int TerminatedSessions { get; set; }
    public int SuspiciousSessions { get; set; }
    public long TotalCommands { get; set; }
    public long TotalQueries { get; set; }
    public long TotalRecordingSize { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public List<SessionsByProtocolDto> SessionsByProtocol { get; set; } = new();
    public List<SessionsByPlatformDto> SessionsByPlatform { get; set; } = new();
    public List<TopUserSessionsDto> TopUsersBySessions { get; set; } = new();
}

/// <summary>
/// Sessions grouped by protocol
/// </summary>
public class SessionsByProtocolDto
{
    public string Protocol { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SuspiciousCount { get; set; }
}

/// <summary>
/// Sessions grouped by platform
/// </summary>
public class SessionsByPlatformDto
{
    public string Platform { get; set; } = string.Empty;
    public int Count { get; set; }
    public long TotalCommands { get; set; }
}

/// <summary>
/// Top users by session count
/// </summary>
public class TopUserSessionsDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int SuspiciousSessionCount { get; set; }
}
