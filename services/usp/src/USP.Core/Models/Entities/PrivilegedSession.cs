namespace USP.Core.Models.Entities;

/// <summary>
/// Privileged session recording for audit and compliance
/// </summary>
public class PrivilegedSession
{
    public Guid Id { get; set; }
    public Guid AccountCheckoutId { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string Protocol { get; set; } = string.Empty; // SSH, RDP, Database, Web
    public string Platform { get; set; } = string.Empty; // PostgreSQL, MySQL, Windows, Linux
    public string? HostAddress { get; set; }
    public int? Port { get; set; }
    public string? RecordingPath { get; set; } // Path to session recording file
    public long RecordingSize { get; set; } = 0; // Size in bytes
    public string SessionType { get; set; } = string.Empty; // interactive, automated, query_only
    public int CommandCount { get; set; } = 0;
    public int QueryCount { get; set; } = 0;
    public bool SuspiciousActivityDetected { get; set; } = false;
    public string? SuspiciousActivityDetails { get; set; } // JSON: details of suspicious activities
    public string Status { get; set; } = "active"; // active, completed, terminated, error
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; } // JSON: additional session metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual AccountCheckout Checkout { get; set; } = null!;
    public virtual PrivilegedAccount Account { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<SessionCommand> Commands { get; set; } = new List<SessionCommand>();
}

/// <summary>
/// Individual command/query executed in a privileged session
/// </summary>
public class SessionCommand
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string CommandType { get; set; } = string.Empty; // SQL, Shell, PowerShell, API
    public string Command { get; set; } = string.Empty; // The actual command/query
    public string? Response { get; set; } // Command output/response
    public int ResponseSize { get; set; } = 0; // Size of response in bytes
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; } = 0;
    public bool IsSuspicious { get; set; } = false;
    public string? SuspiciousReason { get; set; }
    public int SequenceNumber { get; set; } // Order of command in session

    // Navigation property
    public virtual PrivilegedSession Session { get; set; } = null!;
}
