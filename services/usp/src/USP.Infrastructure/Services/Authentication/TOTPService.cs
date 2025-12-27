using OtpNet;
using USP.Core.Interfaces.Services.Authentication;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Implementation of TOTP service using OtpNet library
/// </summary>
public class TOTPService : ITOTPService
{
    private const int SecretLength = 20; // 160 bits
    private const int CodeDigits = 6;
    private const int TimeStep = 30; // seconds

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(SecretLength);
        return Base32Encoding.ToString(key);
    }

    public string GenerateProvisioningUri(string email, string secret, string issuer = "USP Security Platform")
    {
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes, step: TimeStep, totpSize: CodeDigits);

        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?" +
               $"secret={secret}&" +
               $"issuer={Uri.EscapeDataString(issuer)}&" +
               $"algorithm=SHA1&" +
               $"digits={CodeDigits}&" +
               $"period={TimeStep}";
    }

    public bool VerifyCode(string secret, string code, int toleranceSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (code.Length != CodeDigits || !code.All(char.IsDigit))
        {
            return false;
        }

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes, step: TimeStep, totpSize: CodeDigits);

            // Verify with time tolerance (allows for clock skew)
            var window = new VerificationWindow(
                previous: toleranceSteps,
                future: toleranceSteps);

            return totp.VerifyTotp(code, out _, window);
        }
        catch
        {
            return false;
        }
    }

    public string GenerateCode(string secret)
    {
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes, step: TimeStep, totpSize: CodeDigits);
        return totp.ComputeTotp();
    }
}
