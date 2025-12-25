namespace USP.Core.Models.Configuration;

/// <summary>
/// WebAuthn/FIDO2 configuration settings
/// </summary>
public class WebAuthnSettings
{
    public string RelyingPartyId { get; set; } = "localhost";
    public string RelyingPartyName { get; set; } = "USP - Unified Security Platform";
    public string Origin { get; set; } = "https://localhost:8443";
    public int TimestampDriftTolerance { get; set; } = 300000;
    public int ChallengeExpirationMinutes { get; set; } = 5;
}
