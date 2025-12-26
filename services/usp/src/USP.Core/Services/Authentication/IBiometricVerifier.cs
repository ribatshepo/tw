namespace USP.Core.Services.Authentication;

/// <summary>
/// Interface for biometric verification implementations
/// </summary>
public interface IBiometricVerifier
{
    /// <summary>
    /// Verify a biometric template against a stored template
    /// </summary>
    /// <param name="storedTemplateData">Stored biometric template (base64 encoded)</param>
    /// <param name="providedTemplateData">Provided biometric template (base64 encoded)</param>
    /// <param name="biometricType">Type of biometric (Fingerprint, Face, Iris, Voice)</param>
    /// <returns>Tuple of (isMatch, confidenceScore 0-100)</returns>
    Task<(bool isMatch, int confidenceScore)> VerifyAsync(
        string storedTemplateData,
        string providedTemplateData,
        string biometricType);

    /// <summary>
    /// Verify liveness detection for face biometrics
    /// </summary>
    /// <param name="imageData">Face image data (base64 encoded)</param>
    /// <returns>Tuple of (isLive, livenessScore 0-100)</returns>
    Task<(bool isLive, int livenessScore)> VerifyLivenessAsync(string imageData);

    /// <summary>
    /// Extract biometric template from raw biometric data
    /// </summary>
    /// <param name="rawBiometricData">Raw biometric data (base64 encoded)</param>
    /// <param name="biometricType">Type of biometric</param>
    /// <returns>Extracted template (base64 encoded)</returns>
    Task<string> ExtractTemplateAsync(string rawBiometricData, string biometricType);

    /// <summary>
    /// Calculate quality score for a biometric template
    /// </summary>
    /// <param name="templateData">Biometric template (base64 encoded)</param>
    /// <param name="biometricType">Type of biometric</param>
    /// <returns>Quality score (0-100)</returns>
    Task<int> CalculateQualityScoreAsync(string templateData, string biometricType);
}
