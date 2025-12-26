namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the compliance framework for reporting
/// </summary>
public enum ComplianceFramework
{
    /// <summary>
    /// SOC 2 Type II (Service Organization Control 2)
    /// </summary>
    SOC2 = 0,

    /// <summary>
    /// HIPAA (Health Insurance Portability and Accountability Act)
    /// </summary>
    HIPAA = 1,

    /// <summary>
    /// PCI-DSS (Payment Card Industry Data Security Standard)
    /// </summary>
    PCIDSS = 2,

    /// <summary>
    /// ISO 27001 (Information Security Management)
    /// </summary>
    ISO27001 = 3,

    /// <summary>
    /// NIST Cybersecurity Framework
    /// </summary>
    NIST = 4,

    /// <summary>
    /// GDPR (General Data Protection Regulation)
    /// </summary>
    GDPR = 5,

    /// <summary>
    /// CCPA (California Consumer Privacy Act)
    /// </summary>
    CCPA = 6
}
