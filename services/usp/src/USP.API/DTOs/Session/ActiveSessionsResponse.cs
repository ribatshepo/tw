namespace USP.API.DTOs.Session;

/// <summary>
/// Response model for listing active sessions.
/// </summary>
public class ActiveSessionsResponse
{
    /// <summary>
    /// List of active sessions.
    /// </summary>
    public List<SessionResponse> Sessions { get; set; } = new();

    /// <summary>
    /// Total count of active sessions.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current session ID (if applicable).
    /// </summary>
    public string? CurrentSessionId { get; set; }
}
