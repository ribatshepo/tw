using System.Security.Cryptography;

namespace USP.Infrastructure.Cryptography;

/// <summary>
/// Implements Shamir's Secret Sharing algorithm over GF(256).
/// Allows splitting a secret into n shares where any k shares can reconstruct
/// the secret, but k-1 shares reveal nothing about the secret.
/// </summary>
public static class ShamirSecretSharing
{
    // Precomputed logarithm and exponentiation tables for GF(256)
    // Using irreducible polynomial x^8 + x^4 + x^3 + x + 1 (0x11B)
    private static readonly byte[] LogTable = new byte[256];
    private static readonly byte[] ExpTable = new byte[256];

    static ShamirSecretSharing()
    {
        InitializeGaloisField();
    }

    /// <summary>
    /// Splits a secret into shares using Shamir's Secret Sharing.
    /// </summary>
    /// <param name="secret">The secret to split (arbitrary length)</param>
    /// <param name="shares">Total number of shares to create</param>
    /// <param name="threshold">Minimum number of shares needed to reconstruct</param>
    /// <returns>Array of shares, each with the same length as the secret</returns>
    public static byte[][] Split(byte[] secret, int shares, int threshold)
    {
        if (secret == null || secret.Length == 0)
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));

        if (shares < 2 || shares > 255)
            throw new ArgumentException("Number of shares must be between 2 and 255", nameof(shares));

        if (threshold < 2 || threshold > shares)
            throw new ArgumentException("Threshold must be between 2 and total shares", nameof(threshold));

        var result = new byte[shares][];

        // Initialize result arrays
        for (int i = 0; i < shares; i++)
        {
            result[i] = new byte[secret.Length + 1];
            result[i][0] = (byte)(i + 1); // Share index (x-coordinate, never 0)
        }

        // For each byte of the secret, create a polynomial and evaluate it
        for (int byteIndex = 0; byteIndex < secret.Length; byteIndex++)
        {
            var polynomial = GeneratePolynomial(secret[byteIndex], threshold);

            // Evaluate polynomial at each share's x-coordinate
            for (int shareIndex = 0; shareIndex < shares; shareIndex++)
            {
                byte x = (byte)(shareIndex + 1);
                byte y = EvaluatePolynomial(polynomial, x);
                result[shareIndex][byteIndex + 1] = y;
            }
        }

        return result;
    }

    /// <summary>
    /// Combines shares to reconstruct the secret using Lagrange interpolation.
    /// </summary>
    /// <param name="shares">Array of shares (minimum threshold required)</param>
    /// <returns>The reconstructed secret</returns>
    public static byte[] Combine(byte[][] shares)
    {
        if (shares == null || shares.Length == 0)
            throw new ArgumentException("Shares cannot be null or empty", nameof(shares));

        // All shares must have the same length
        int secretLength = shares[0].Length - 1;
        foreach (var share in shares)
        {
            if (share.Length != secretLength + 1)
                throw new ArgumentException("All shares must have the same length");
        }

        var secret = new byte[secretLength];

        // Extract x-coordinates
        var xCoords = shares.Select(s => s[0]).ToArray();

        // For each byte position, perform Lagrange interpolation
        for (int byteIndex = 0; byteIndex < secretLength; byteIndex++)
        {
            var yCoords = shares.Select(s => s[byteIndex + 1]).ToArray();
            secret[byteIndex] = LagrangeInterpolate(xCoords, yCoords);
        }

        return secret;
    }

    /// <summary>
    /// Generates a random polynomial of degree (threshold - 1) with the given constant term.
    /// </summary>
    private static byte[] GeneratePolynomial(byte constantTerm, int threshold)
    {
        var polynomial = new byte[threshold];
        polynomial[0] = constantTerm; // f(0) = secret

        // Generate random coefficients for remaining terms
        using var rng = RandomNumberGenerator.Create();
        var randomBytes = new byte[threshold - 1];
        rng.GetBytes(randomBytes);

        for (int i = 1; i < threshold; i++)
        {
            polynomial[i] = randomBytes[i - 1];
        }

        return polynomial;
    }

    /// <summary>
    /// Evaluates a polynomial at a given x using Horner's method in GF(256).
    /// </summary>
    private static byte EvaluatePolynomial(byte[] polynomial, byte x)
    {
        byte result = 0;

        // Horner's method: f(x) = a0 + x(a1 + x(a2 + ...))
        for (int i = polynomial.Length - 1; i >= 0; i--)
        {
            result = GFAdd(GFMultiply(result, x), polynomial[i]);
        }

        return result;
    }

    /// <summary>
    /// Performs Lagrange interpolation to find f(0) from given points.
    /// </summary>
    private static byte LagrangeInterpolate(byte[] xCoords, byte[] yCoords)
    {
        byte result = 0;

        for (int i = 0; i < xCoords.Length; i++)
        {
            byte xi = xCoords[i];
            byte yi = yCoords[i];

            // Calculate Lagrange basis polynomial L_i(0)
            byte numerator = 1;
            byte denominator = 1;

            for (int j = 0; j < xCoords.Length; j++)
            {
                if (i == j) continue;

                byte xj = xCoords[j];

                // numerator *= (0 - xj) = -xj = xj (since -x = x in GF(2^n))
                numerator = GFMultiply(numerator, xj);

                // denominator *= (xi - xj)
                denominator = GFMultiply(denominator, GFSubtract(xi, xj));
            }

            // basis = numerator / denominator
            byte basis = GFDivide(numerator, denominator);

            // result += yi * basis
            result = GFAdd(result, GFMultiply(yi, basis));
        }

        return result;
    }

    // Galois Field GF(256) arithmetic operations

    /// <summary>
    /// Addition in GF(256) is XOR.
    /// </summary>
    private static byte GFAdd(byte a, byte b) => (byte)(a ^ b);

    /// <summary>
    /// Subtraction in GF(256) is also XOR (same as addition).
    /// </summary>
    private static byte GFSubtract(byte a, byte b) => (byte)(a ^ b);

    /// <summary>
    /// Multiplication in GF(256) using logarithm tables.
    /// </summary>
    private static byte GFMultiply(byte a, byte b)
    {
        if (a == 0 || b == 0)
            return 0;

        int logA = LogTable[a];
        int logB = LogTable[b];
        int logResult = (logA + logB) % 255;

        return ExpTable[logResult];
    }

    /// <summary>
    /// Division in GF(256) using logarithm tables.
    /// </summary>
    private static byte GFDivide(byte a, byte b)
    {
        if (b == 0)
            throw new DivideByZeroException("Division by zero in GF(256)");

        if (a == 0)
            return 0;

        int logA = LogTable[a];
        int logB = LogTable[b];
        int logResult = (logA - logB + 255) % 255;

        return ExpTable[logResult];
    }

    /// <summary>
    /// Initializes the logarithm and exponentiation tables for GF(256).
    /// Uses the irreducible polynomial x^8 + x^4 + x^3 + x^2 + 1 (0x11D) which is standard for Shamir.
    /// </summary>
    private static void InitializeGaloisField()
    {
        int polynomial = 0x11D; // x^8 + x^4 + x^3 + x^2 + 1 (standard for Shamir)
        int x = 1;

        for (int i = 0; i < 255; i++)
        {
            ExpTable[i] = (byte)x;
            LogTable[x] = (byte)i;

            // Multiply by 2 (left shift in GF)
            x = (x << 1);

            // If overflow, reduce by polynomial (x^8 term becomes lower terms)
            if ((x & 0x100) != 0)
            {
                x ^= polynomial;
            }
        }

        // Ensure wraparound works correctly
        ExpTable[255] = ExpTable[0]; // α^255 = α^0 = 1
    }
}
