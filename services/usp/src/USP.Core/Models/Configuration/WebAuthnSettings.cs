namespace USP.Core.Models.Configuration;

/// <summary>
/// WebAuthn/FIDO2 configuration settings
/// </summary>
public class WebAuthnSettings
{
    public string RelyingPartyId { get; set; } = string.Empty;
    public string RelyingPartyName { get; set; } = "USP - Unified Security Platform";
    public string Origin { get; set; } = string.Empty;
    public int TimestampDriftTolerance { get; set; } = 300000;
    public int ChallengeExpirationMinutes { get; set; } = 5;
}
