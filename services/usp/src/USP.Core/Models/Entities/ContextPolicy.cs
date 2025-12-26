namespace USP.Core.Models.Entities;

/// <summary>
/// Context-aware access control policy entity
/// Defines time, location, device, and risk-based access restrictions
/// </summary>
public class ContextPolicy
{
    public Guid Id { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public string Action { get; set; } = "*"; // *, read, write, delete

    // Time-based restrictions
    public bool EnableTimeRestriction { get; set; }

    public string? AllowedDaysOfWeek { get; set; } // CSV: Monday,Tuesday,Wednesday

    public TimeSpan? AllowedStartTime { get; set; }

    public TimeSpan? AllowedEndTime { get; set; }

    // Location-based restrictions
    public bool EnableLocationRestriction { get; set; }

    public string[]? AllowedCountries { get; set; }

    public string[]? DeniedCountries { get; set; }

    public string[]? AllowedNetworkZones { get; set; } // internal, vpn, external

    // Device-based restrictions
    public bool EnableDeviceRestriction { get; set; }

    public bool RequireCompliantDevice { get; set; }

    public string[]? AllowedDeviceTypes { get; set; } // desktop, mobile, tablet

    // Risk-based restrictions
    public bool EnableRiskRestriction { get; set; }

    public int? MaxAllowedRiskScore { get; set; } // 0-100

    public bool DenyImpossibleTravel { get; set; }

    public bool RequireMfaOnHighRisk { get; set; }

    public bool RequireApprovalOnHighRisk { get; set; }

    public int? HighRiskThreshold { get; set; } // Default 70

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }
}
