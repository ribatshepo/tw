using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Adaptive authentication policy engine for risk-based authentication
/// Evaluates policies, triggers step-up challenges, and validates authentication
/// </summary>
public interface IAdaptiveAuthPolicyEngine
{
    /// <summary>
    /// Evaluate authentication policies for a user based on risk assessment
    /// Determines if step-up authentication is required
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="riskScore">Current risk score (0-100)</param>
    /// <param name="resourcePath">Resource being accessed</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="deviceFingerprint">Device fingerprint</param>
    /// <returns>Policy evaluation result with required actions</returns>
    Task<PolicyEvaluationResultDto> EvaluatePolicyAsync(
        Guid userId,
        int riskScore,
        string? resourcePath = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceFingerprint = null);

    /// <summary>
    /// Initiate step-up authentication challenge
    /// Creates a step-up session and returns challenge details
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="requiredFactors">Required authentication factors</param>
    /// <param name="resourcePath">Resource requiring step-up</param>
    /// <param name="validityMinutes">How long step-up is valid</param>
    /// <returns>Step-up challenge details</returns>
    Task<StepUpChallengeDto> InitiateStepUpAsync(
        Guid userId,
        List<string> requiredFactors,
        string? resourcePath = null,
        int validityMinutes = 15);

    /// <summary>
    /// Validate step-up authentication response
    /// Verifies that user completed required factors
    /// </summary>
    /// <param name="sessionToken">Step-up session token</param>
    /// <param name="userId">User ID</param>
    /// <param name="factor">Factor being validated (totp, sms, webauthn, etc.)</param>
    /// <param name="credential">Factor credential/response</param>
    /// <returns>Validation result</returns>
    Task<StepUpValidationResultDto> ValidateStepUpFactorAsync(
        string sessionToken,
        Guid userId,
        string factor,
        string credential);

    /// <summary>
    /// Complete step-up authentication session
    /// Marks session as completed if all required factors verified
    /// </summary>
    /// <param name="sessionToken">Step-up session token</param>
    /// <param name="userId">User ID</param>
    /// <returns>Completion result with session details</returns>
    Task<StepUpCompletionResultDto> CompleteStepUpAsync(
        string sessionToken,
        Guid userId);

    /// <summary>
    /// Check if user has valid step-up session for resource
    /// Used to bypass step-up if recently completed
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="resourcePath">Resource path</param>
    /// <returns>True if valid step-up session exists</returns>
    Task<bool> HasValidStepUpSessionAsync(Guid userId, string? resourcePath = null);

    /// <summary>
    /// Record authentication event for audit and analytics
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="eventType">Event type (login, step_up, etc.)</param>
    /// <param name="riskScore">Risk score at event time</param>
    /// <param name="outcome">Event outcome</param>
    /// <param name="factorsUsed">Factors used in authentication</param>
    /// <param name="policyId">Policy applied (if any)</param>
    /// <param name="metadata">Additional event metadata</param>
    Task RecordAuthenticationEventAsync(
        Guid userId,
        string eventType,
        int riskScore,
        string outcome,
        List<string>? factorsUsed = null,
        Guid? policyId = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Get authentication events for a user
    /// Used for audit trails and analytics
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="eventType">Filter by event type (optional)</param>
    /// <param name="startDate">Start date (optional)</param>
    /// <param name="endDate">End date (optional)</param>
    /// <param name="limit">Maximum results to return</param>
    /// <returns>List of authentication events</returns>
    Task<List<AuthenticationEventDto>> GetAuthenticationEventsAsync(
        Guid userId,
        string? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100);

    /// <summary>
    /// Create or update adaptive authentication policy
    /// </summary>
    /// <param name="request">Policy configuration</param>
    /// <returns>Created/updated policy</returns>
    Task<AdaptiveAuthPolicyDto> CreateOrUpdatePolicyAsync(CreateAdaptiveAuthPolicyDto request);

    /// <summary>
    /// Get policy by ID
    /// </summary>
    /// <param name="policyId">Policy ID</param>
    /// <returns>Policy details</returns>
    Task<AdaptiveAuthPolicyDto> GetPolicyAsync(Guid policyId);

    /// <summary>
    /// Get all active policies
    /// </summary>
    /// <returns>List of active policies ordered by priority</returns>
    Task<List<AdaptiveAuthPolicyDto>> GetActivePoliciesAsync();

    /// <summary>
    /// Delete policy
    /// </summary>
    /// <param name="policyId">Policy ID</param>
    Task DeletePolicyAsync(Guid policyId);

    /// <summary>
    /// Get authentication statistics for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="days">Number of days to include</param>
    /// <returns>Authentication statistics</returns>
    Task<AuthenticationStatisticsDto> GetAuthenticationStatisticsAsync(Guid userId, int days = 30);
}
