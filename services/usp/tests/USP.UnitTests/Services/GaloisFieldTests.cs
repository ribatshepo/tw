using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace USP.UnitTests.Services;

/// <summary>
/// Tests to verify Galois Field GF(256) arithmetic is working correctly
/// </summary>
public class GaloisFieldTests
{
    private readonly ITestOutputHelper _output;

    public GaloisFieldTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GFMultiply_Identity_ShouldWork()
    {
        // Test that multiplying by 1 returns the original value
        for (byte b = 1; b < 10; b++)
        {
            byte result = InvokeGFMultiply(1, b);
            _output.WriteLine($"GFMultiply(1, {b}) = {result}");
            Assert.Equal(b, result);
        }
    }

    [Fact]
    public void GFMultiply_Zero_ShouldReturnZero()
    {
        for (byte b = 0; b < 10; b++)
        {
            byte result = InvokeGFMultiply(0, b);
            Assert.Equal(0, result);

            result = InvokeGFMultiply(b, 0);
            Assert.Equal(0, result);
        }
    }

    [Fact]
    public void GFDivide_Inverse_ShouldWork()
    {
        // Test that a / a = 1
        for (byte a = 1; a < 10; a++)
        {
            byte result = InvokeGFDivide(a, a);
            _output.WriteLine($"GFDivide({a}, {a}) = {result}");
            Assert.Equal(1, result);
        }
    }

    [Fact]
    public void LogExpTables_ShouldBeInverse()
    {
        var expTable = GetExpTable();
        var logTable = GetLogTable();

        _output.WriteLine("Checking first 10 entries:");
        for (int i = 0; i < 10; i++)
        {
            byte exp = expTable[i];
            _output.WriteLine($"ExpTable[{i}] = {exp}");

            if (exp != 0)
            {
                byte log = logTable[exp];
                _output.WriteLine($"  LogTable[{exp}] = {log}");

                // LogTable[ExpTable[i]] should equal i (for i < 255)
                if (i < 255)
                {
                    Assert.Equal((byte)i, log);
                }
            }
        }
    }

    // Helper methods to invoke private static methods using reflection
    private static byte InvokeGFMultiply(byte a, byte b)
    {
        var type = typeof(USP.Infrastructure.Cryptography.ShamirSecretSharing);
        var method = type.GetMethod("GFMultiply", BindingFlags.NonPublic | BindingFlags.Static);
        return (byte)method!.Invoke(null, new object[] { a, b })!;
    }

    private static byte InvokeGFDivide(byte a, byte b)
    {
        var type = typeof(USP.Infrastructure.Cryptography.ShamirSecretSharing);
        var method = type.GetMethod("GFDivide", BindingFlags.NonPublic | BindingFlags.Static);
        return (byte)method!.Invoke(null, new object[] { a, b })!;
    }

    private static byte[] GetExpTable()
    {
        var type = typeof(USP.Infrastructure.Cryptography.ShamirSecretSharing);
        var field = type.GetField("ExpTable", BindingFlags.NonPublic | BindingFlags.Static);
        return (byte[])field!.GetValue(null)!;
    }

    private static byte[] GetLogTable()
    {
        var type = typeof(USP.Infrastructure.Cryptography.ShamirSecretSharing);
        var field = type.GetField("LogTable", BindingFlags.NonPublic | BindingFlags.Static);
        return (byte[])field!.GetValue(null)!;
    }
}
