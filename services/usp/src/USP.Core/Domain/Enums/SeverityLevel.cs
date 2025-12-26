namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the severity level of a security anomaly or alert
/// </summary>
public enum SeverityLevel
{
    /// <summary>
    /// Low severity - informational
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - warrants attention
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - requires prompt action
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - requires immediate action
    /// </summary>
    Critical = 3
}
