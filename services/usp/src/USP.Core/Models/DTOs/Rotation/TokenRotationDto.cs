namespace USP.Core.Models.DTOs.Rotation;

public class TokenRotationDto
{
    public Guid Id { get; set; }
    public string TokenType { get; set; } = string.Empty; // JWT, OAuth, PAT, Session
    public string TokenName { get; set; } = string.Empty;
    public int RotationIntervalMinutes { get; set; }
    public bool AutoRotate { get; set; }
    public DateTime? NextRotationDate { get; set; }
    public DateTime? LastRotationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTokenRotationRequest
{
    public string TokenType { get; set; } = string.Empty;
    public string TokenName { get; set; } = string.Empty;
    public int RotationIntervalMinutes { get; set; } = 60;
    public bool AutoRotate { get; set; } = true;
    public Dictionary<string, string>? TokenMetadata { get; set; }
}

public class TokenRotationResultDto
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? NewToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}
