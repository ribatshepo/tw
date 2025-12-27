using System;
using Xunit;
using Xunit.Abstractions;
using USP.Infrastructure.Cryptography;

namespace USP.UnitTests.Services;

/// <summary>
/// Tests for ShamirSecretSharing to verify split/combine works correctly
/// </summary>
public class ShamirSecretSharingTests
{
    private readonly ITestOutputHelper _output;

    public ShamirSecretSharingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ShamirSplitCombine_ShouldReconstructSecret()
    {
        // Arrange - Create a simple test secret
        var secret = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        _output.WriteLine($"Original secret: {Convert.ToHexString(secret)}");

        // Act - Split into 5 shares with threshold 3
        var shares = ShamirSecretSharing.Split(secret, shares: 5, threshold: 3);

        _output.WriteLine($"Split into {shares.Length} shares:");
        for (int i = 0; i < shares.Length; i++)
        {
            _output.WriteLine($"  Share {i + 1}: Length = {shares[i].Length}, X = {shares[i][0]}, Data: {Convert.ToHexString(shares[i])}");
        }

        // Reconstruct using first 3 shares
        var reconstructed = ShamirSecretSharing.Combine(new[] { shares[0], shares[1], shares[2] });

        _output.WriteLine($"Reconstructed secret: {Convert.ToHexString(reconstructed)}");

        // Assert
        Assert.Equal(secret, reconstructed);
    }

    [Fact]
    public void ShamirSplitCombine_With32ByteSecret_ShouldWork()
    {
        // Arrange - Create a 32-byte secret (like master key)
        var secret = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            secret[i] = (byte)(i + 1);
        }

        _output.WriteLine($"Original secret (first 8 bytes): {Convert.ToHexString(secret.AsSpan(0, 8))}");

        // Act - Split into 5 shares with threshold 3
        var shares = ShamirSecretSharing.Split(secret, shares: 5, threshold: 3);

        _output.WriteLine($"Split into {shares.Length} shares (showing first 3):");
        for (int i = 0; i < 3; i++)
        {
            _output.WriteLine($"  Share {i + 1}: X = {shares[i][0]}, First 8 bytes: {Convert.ToHexString(shares[i].AsSpan(0, 8))}");
        }

        // Reconstruct using first 3 shares
        var reconstructed = ShamirSecretSharing.Combine(new[] { shares[0], shares[1], shares[2] });

        _output.WriteLine($"Reconstructed secret (first 8 bytes): {Convert.ToHexString(reconstructed.AsSpan(0, 8))}");

        // Assert
        Assert.Equal(secret, reconstructed);
    }

    [Fact]
    public void ShamirSplitCombine_WithDifferentShareCombinations_ShouldWork()
    {
        // Arrange
        var secret = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        _output.WriteLine($"Original secret: {Convert.ToHexString(secret)}");

        // Act - Split into 5 shares with threshold 3
        var shares = ShamirSecretSharing.Split(secret, shares: 5, threshold: 3);

        // Test different combinations of 3 shares
        var combinations = new[]
        {
            new[] { shares[0], shares[1], shares[2] },
            new[] { shares[0], shares[2], shares[4] },
            new[] { shares[1], shares[3], shares[4] },
            new[] { shares[2], shares[3], shares[4] }
        };

        foreach (var combination in combinations)
        {
            var reconstructed = ShamirSecretSharing.Combine(combination);
            _output.WriteLine($"Combination [{combination[0][0]}, {combination[1][0]}, {combination[2][0]}]: {Convert.ToHexString(reconstructed)}");
            Assert.Equal(secret, reconstructed);
        }
    }
}
