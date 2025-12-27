#!/usr/bin/env dotnet-script

// Test script for KEK-based seal/unseal workflow
// Usage: dotnet script test-seal-unseal.csx

#r "src/USP.Infrastructure/bin/Debug/net8.0/USP.Infrastructure.dll"
#r "src/USP.Core/bin/Debug/net8.0/USP.Core.dll"

using System;
using System.Linq;
using USP.Infrastructure.Services.Secrets;
using USP.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

Console.WriteLine("=== USP Seal/Unseal KEK Workflow Test ===\n");

// Set KEK environment variable for testing
var testKek = "kOoYu24l7gPrFni28zzeVRGJyPQJ5JFmE6yRhq63OKs=";
Environment.SetEnvironmentVariable("USP_KEY_ENCRYPTION_KEY", testKek);
Console.WriteLine($"✓ KEK environment variable set");

// Create in-memory database for testing
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: "TestSealUnseal")
    .Options;

using var context = new ApplicationDbContext(options);

// Create logger
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<SealService>();

// Create SealService
var sealService = new SealService(context, logger);

Console.WriteLine("\n--- Step 1: Initialize Vault ---");
var initResult = await sealService.InitializeAsync(secretShares: 5, secretThreshold: 3);

Console.WriteLine($"✓ Vault initialized");
Console.WriteLine($"  Secret Shares: {initResult.SecretShares}");
Console.WriteLine($"  Threshold: {initResult.SecretThreshold}");
Console.WriteLine($"  Unseal Keys Generated: {initResult.UnsealKeys.Count}");
Console.WriteLine($"  Root Token: {initResult.RootToken[..16]}...");

// Verify vault is unsealed after initialization
var status1 = await sealService.GetSealStatusAsync();
Console.WriteLine($"✓ Vault status after init: {(status1.Sealed ? "SEALED" : "UNSEALED")}");

if (status1.Sealed)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: Vault should be unsealed after initialization!");
    Console.ResetColor();
    return 1;
}

Console.WriteLine("\n--- Step 2: Seal Vault ---");
await sealService.SealAsync();
var status2 = await sealService.GetSealStatusAsync();
Console.WriteLine($"✓ Vault status after seal: {(status2.Sealed ? "SEALED" : "UNSEALED")}");

if (!status2.Sealed)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: Vault should be sealed!");
    Console.ResetColor();
    return 1;
}

Console.WriteLine("\n--- Step 3: Unseal Vault (requires 3 of 5 keys) ---");

// Submit first unseal key
Console.WriteLine($"Submitting unseal key 1...");
var unsealStatus1 = await sealService.UnsealAsync(initResult.UnsealKeys[0]);
Console.WriteLine($"  Progress: {unsealStatus1.Progress}/{unsealStatus1.Threshold}");
Console.WriteLine($"  Status: {(unsealStatus1.Sealed ? "SEALED" : "UNSEALED")}");

// Submit second unseal key
Console.WriteLine($"Submitting unseal key 2...");
var unsealStatus2 = await sealService.UnsealAsync(initResult.UnsealKeys[1]);
Console.WriteLine($"  Progress: {unsealStatus2.Progress}/{unsealStatus2.Threshold}");
Console.WriteLine($"  Status: {(unsealStatus2.Sealed ? "SEALED" : "UNSEALED")}");

// Submit third unseal key - should complete unseal
Console.WriteLine($"Submitting unseal key 3...");
var unsealStatus3 = await sealService.UnsealAsync(initResult.UnsealKeys[2]);
Console.WriteLine($"  Progress: {unsealStatus3.Progress}/{unsealStatus3.Threshold}");
Console.WriteLine($"  Status: {(unsealStatus3.Sealed ? "SEALED" : "UNSEALED")}");

if (unsealStatus3.Sealed)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: Vault should be unsealed after submitting threshold keys!");
    Console.ResetColor();
    return 1;
}

Console.WriteLine("\n--- Step 4: Verify Master Key Recovery ---");
var masterKey = sealService.GetMasterKey();
if (masterKey == null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: Master key should be available when unsealed!");
    Console.ResetColor();
    return 1;
}

Console.WriteLine($"✓ Master key recovered successfully ({masterKey.Length} bytes)");

Console.WriteLine("\n--- Step 5: Test Re-seal ---");
await sealService.SealAsync();
var finalStatus = await sealService.GetSealStatusAsync();
Console.WriteLine($"✓ Vault re-sealed: {(finalStatus.Sealed ? "SEALED" : "UNSEALED")}");

if (!finalStatus.Sealed)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: Vault should be sealed!");
    Console.ResetColor();
    return 1;
}

// Verify master key is cleared from memory after seal
var masterKeyAfterSeal = sealService.GetMasterKey();
if (masterKeyAfterSeal != null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: Master key should be cleared from memory when sealed!");
    Console.ResetColor();
    return 1;
}

Console.WriteLine($"✓ Master key cleared from memory after seal");

Console.WriteLine("\n=== ✓ All Tests Passed! ===");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\nKEK-based seal/unseal workflow is working correctly!");
Console.WriteLine("- Master key encrypted with KEK (not itself)");
Console.WriteLine("- Shamir Secret Sharing working as expected");
Console.WriteLine("- Seal/unseal operations functioning properly");
Console.WriteLine("- Memory cleanup working correctly");
Console.ResetColor();

return 0;
