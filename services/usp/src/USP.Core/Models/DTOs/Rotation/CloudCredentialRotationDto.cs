namespace USP.Core.Models.DTOs.Rotation;

public class CloudCredentialRotationDto
{
    public Guid Id { get; set; }
    public string ProviderName { get; set; } = string.Empty; // AWS, Azure, GCP
    public string CredentialType { get; set; } = string.Empty; // IAMAccessKey, ServicePrincipal, ServiceAccountKey
    public string ResourceName { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public int RotationIntervalDays { get; set; }
    public bool AutoRotate { get; set; }
    public DateTime? NextRotationDate { get; set; }
    public DateTime? LastRotationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateCloudCredentialRotationRequest
{
    public string ProviderName { get; set; } = string.Empty;
    public string CredentialType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public int RotationIntervalDays { get; set; } = 90;
    public bool AutoRotate { get; set; } = true;
    public bool EnforceLeastPrivilege { get; set; } = true;
    public Dictionary<string, string>? ProviderConfig { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class CloudCredentialRotationResultDto
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? NewCredentialId { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string>? CredentialMetadata { get; set; }
}
