using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using USP.Core.Services.Cryptography;

namespace USP.Infrastructure.Services.Cryptography;

/// <summary>
/// Shamir's Secret Sharing implementation using GF(256)
/// Splits a secret into N shares where K shares can reconstruct it
/// Based on polynomial interpolation in a finite field
/// </summary>
public class ShamirSecretSharing : IShamirSecretSharing
{
    private readonly ILogger<ShamirSecretSharing> _logger;

    // GF(256) multiplication and division lookup tables
    private static readonly byte[] GfExp = new byte[256];
    private static readonly byte[] GfLog = new byte[256];

    static ShamirSecretSharing()
    {
        // Initialize GF(256) lookup tables
        InitializeGaloisField();
    }

    public ShamirSecretSharing(ILogger<ShamirSecretSharing> logger)
    {
        _logger = logger;
    }

    public byte[][] Split(byte[] secret, int threshold, int totalShares)
    {
        if (secret == null || secret.Length == 0)
        {
            throw new ArgumentException("Secret cannot be empty", nameof(secret));
        }

        if (threshold < 2)
        {
            throw new ArgumentException("Threshold must be at least 2", nameof(threshold));
        }

        if (totalShares < threshold)
        {
            throw new ArgumentException("Total shares must be >= threshold", nameof(totalShares));
        }

        if (totalShares > 255)
        {
            throw new ArgumentException("Total shares cannot exceed 255", nameof(totalShares));
        }

        _logger.LogInformation("Splitting secret into {Total} shares with threshold {Threshold}", totalShares, threshold);

        // Create shares array
        var shares = new byte[totalShares][];
        for (int i = 0; i < totalShares; i++)
        {
            shares[i] = new byte[secret.Length + 1];
            shares[i][0] = (byte)(i + 1); // Share index (x coordinate, never 0)
        }

        // Split each byte of the secret
        for (int byteIdx = 0; byteIdx < secret.Length; byteIdx++)
        {
            // Generate random polynomial coefficients
            var coefficients = new byte[threshold];
            coefficients[0] = secret[byteIdx]; // Secret is the constant term

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(coefficients, 1, threshold - 1);

            // Evaluate polynomial for each share
            for (int shareIdx = 0; shareIdx < totalShares; shareIdx++)
            {
                byte x = (byte)(shareIdx + 1);
                shares[shareIdx][byteIdx + 1] = EvaluatePolynomial(coefficients, x);
            }
        }

        _logger.LogInformation("Successfully split secret into {Count} shares", shares.Length);

        return shares;
    }

    public byte[] Combine(byte[][] shares)
    {
        if (shares == null || shares.Length == 0)
        {
            throw new ArgumentException("Shares cannot be empty", nameof(shares));
        }

        if (shares.Length < 2)
        {
            throw new ArgumentException("At least 2 shares are required", nameof(shares));
        }

        // Validate all shares have the same length
        int secretLength = shares[0].Length - 1;
        foreach (var share in shares)
        {
            if (share.Length != secretLength + 1)
            {
                throw new ArgumentException("All shares must have the same length");
            }
        }

        _logger.LogInformation("Combining {Count} shares to reconstruct secret", shares.Length);

        var secret = new byte[secretLength];

        // Reconstruct each byte using Lagrange interpolation
        for (int byteIdx = 0; byteIdx < secretLength; byteIdx++)
        {
            secret[byteIdx] = LagrangeInterpolate(shares, byteIdx + 1);
        }

        _logger.LogInformation("Successfully reconstructed secret from shares");

        return secret;
    }

    public bool ValidateShares(byte[][] shares, int expectedThreshold)
    {
        if (shares == null || shares.Length < expectedThreshold)
        {
            return false;
        }

        // Check all shares have valid indices and same length
        var indices = new HashSet<byte>();
        int? expectedLength = null;

        foreach (var share in shares)
        {
            if (share == null || share.Length < 2)
            {
                return false;
            }

            byte index = share[0];
            if (index == 0 || !indices.Add(index))
            {
                return false; // Index 0 or duplicate index
            }

            if (expectedLength == null)
            {
                expectedLength = share.Length;
            }
            else if (share.Length != expectedLength)
            {
                return false;
            }
        }

        return true;
    }

    #region Galois Field GF(256) Operations

    private static void InitializeGaloisField()
    {
        // GF(256) with irreducible polynomial x^8 + x^4 + x^3 + x + 1 (0x11b)
        int polynomial = 0x11b;
        int x = 1;

        for (int i = 0; i < 255; i++)
        {
            GfExp[i] = (byte)x;
            GfLog[x] = (byte)i;

            x <<= 1;
            if ((x & 0x100) != 0)
            {
                x ^= polynomial;
            }
        }

        GfExp[255] = 1; // Wrap around
    }

    private static byte GfMultiply(byte a, byte b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        int logSum = GfLog[a] + GfLog[b];
        return GfExp[logSum % 255];
    }

    private static byte GfDivide(byte a, byte b)
    {
        if (a == 0)
        {
            return 0;
        }

        if (b == 0)
        {
            throw new DivideByZeroException("Division by zero in GF(256)");
        }

        int logDiff = GfLog[a] - GfLog[b];
        if (logDiff < 0)
        {
            logDiff += 255;
        }

        return GfExp[logDiff];
    }

    #endregion

    #region Polynomial Operations

    private static byte EvaluatePolynomial(byte[] coefficients, byte x)
    {
        // Evaluate polynomial using Horner's method in GF(256)
        byte result = 0;

        for (int i = coefficients.Length - 1; i >= 0; i--)
        {
            result = (byte)(GfMultiply(result, x) ^ coefficients[i]);
        }

        return result;
    }

    private static byte LagrangeInterpolate(byte[][] shares, int byteIndex)
    {
        // Lagrange interpolation at x=0 to get the constant term (secret)
        byte result = 0;

        for (int i = 0; i < shares.Length; i++)
        {
            byte xi = shares[i][0];
            byte yi = shares[i][byteIndex];

            // Calculate Lagrange basis polynomial
            byte numerator = 1;
            byte denominator = 1;

            for (int j = 0; j < shares.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                byte xj = shares[j][0];

                // Numerator: product of (0 - xj) = product of xj
                numerator = GfMultiply(numerator, xj);

                // Denominator: product of (xi - xj)
                denominator = GfMultiply(denominator, (byte)(xi ^ xj));
            }

            // basis = numerator / denominator
            byte basis = GfDivide(numerator, denominator);

            // Add yi * basis to result
            result ^= GfMultiply(yi, basis);
        }

        return result;
    }

    #endregion
}
