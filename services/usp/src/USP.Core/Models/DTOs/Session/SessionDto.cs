namespace USP.Core.Models.DTOs.Session;

/// <summary>
/// Session data transfer object
/// </summary>
public class SessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivity { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// Session extension request
/// </summary>
public class ExtendSessionRequest
{
    public int AdditionalMinutes { get; set; } = 15;
}
