using System;
using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;

namespace USP.UnitTests.Services;

/// <summary>
/// Diagnostic test to verify KEK encryption/decryption works correctly
/// </summary>
public class SealServiceKEKDiagnostic
{
    private readonly ITestOutputHelper _output;

    public SealServiceKEKDiagnostic(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void KEK_EncryptionDecryption_ShouldWork()
    {
        // Generate test KEK
        var kek = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(kek);
        }
        _output.WriteLine($"KEK: {Convert.ToBase64String(kek)}");

        // Generate test master key
        var masterKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(masterKey);
        }
        _output.WriteLine($"Master Key: {Convert.ToBase64String(masterKey)}");

        // Encrypt master key with KEK
        byte[] encryptedMasterKey;
        using (var aes = Aes.Create())
        {
            aes.Key = kek;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var ciphertext = encryptor.TransformFinalBlock(masterKey, 0, masterKey.Length);

            encryptedMasterKey = new byte[aes.IV.Length + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, encryptedMasterKey, 0, aes.IV.Length);
            Buffer.BlockCopy(ciphertext, 0, encryptedMasterKey, aes.IV.Length, ciphertext.Length);
        }
        _output.WriteLine($"Encrypted Master Key Length: {encryptedMasterKey.Length} bytes");

        // Decrypt master key with KEK
        byte[] decryptedMasterKey;
        using (var aes = Aes.Create())
        {
            aes.Key = kek;

            // Extract IV
            var iv = new byte[16]; // AES IV is always 16 bytes
            Buffer.BlockCopy(encryptedMasterKey, 0, iv, 0, iv.Length);
            aes.IV = iv;

            // Extract ciphertext
            var ciphertext = new byte[encryptedMasterKey.Length - iv.Length];
            Buffer.BlockCopy(encryptedMasterKey, iv.Length, ciphertext, 0, ciphertext.Length);

            // Decrypt
            using var decryptor = aes.CreateDecryptor();
            decryptedMasterKey = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }
        _output.WriteLine($"Decrypted Master Key: {Convert.ToBase64String(decryptedMasterKey)}");

        // Verify
        Assert.Equal(masterKey, decryptedMasterKey);
        _output.WriteLine("✓ KEK encryption/decryption works correctly!");
    }
}
