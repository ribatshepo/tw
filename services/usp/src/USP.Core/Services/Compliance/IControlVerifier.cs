using USP.Core.Models.DTOs.Compliance;

namespace USP.Core.Services.Compliance;

/// <summary>
/// Base interface for compliance control verifiers
/// Each verifier implements specific verification logic for a control type
/// </summary>
public interface IControlVerifier
{
    /// <summary>
    /// Verify a compliance control
    /// </summary>
    /// <param name="controlId">Control to verify</param>
    /// <returns>Verification result with evidence</returns>
    Task<ControlVerificationResultDto> VerifyAsync(Guid controlId);

    /// <summary>
    /// Collect evidence for a control
    /// </summary>
    /// <param name="controlId">Control ID</param>
    /// <returns>Collected evidence</returns>
    Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId);

    /// <summary>
    /// Check if this verifier can handle a specific control
    /// </summary>
    /// <param name="controlType">Control type or category</param>
    /// <returns>True if verifier can handle this control</returns>
    bool CanVerify(string controlType);
}
