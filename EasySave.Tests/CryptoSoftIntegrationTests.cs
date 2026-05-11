using System.Diagnostics;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Tests;

public sealed class CryptoSoftIntegrationTests : IDisposable
{
    private readonly string testRoot;

    public CryptoSoftIntegrationTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), $"cryptosoft-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public async Task CryptoSoftEncryptionServiceEncryptsAndDecryptsFileThroughExternalProject()
    {
        var filePath = Path.Combine(testRoot, "secret.txt");
        const string originalContent = "Sensitive payload";
        await File.WriteAllTextAsync(filePath, originalContent);

        var settings = new AppSettings
        {
            CryptoSoftPath = FindCryptoSoftProjectPath(),
            CryptoKey = "unit-test-key"
        };

        var service = new CryptoSoftEncryptionService();

        var firstRun = await service.EncryptAsync(filePath, settings);
        var encryptedContent = await File.ReadAllTextAsync(filePath);
        var secondRun = await service.EncryptAsync(filePath, settings);
        var decryptedContent = await File.ReadAllTextAsync(filePath);

        Assert.True(firstRun >= 0);
        Assert.NotEqual(originalContent, encryptedContent);
        Assert.True(secondRun >= 0);
        Assert.Equal(originalContent, decryptedContent);
    }

    [Fact]
    public async Task CryptoSoftRejectsASecondInstanceWhileBusy()
    {
        var firstFilePath = Path.Combine(testRoot, "busy-first.txt");
        var secondFilePath = Path.Combine(testRoot, "busy-second.txt");
        await File.WriteAllTextAsync(firstFilePath, "first");
        await File.WriteAllTextAsync(secondFilePath, "second");

        var previousDelay = Environment.GetEnvironmentVariable("CRYPTOSOFT_DELAY_MS");
        Environment.SetEnvironmentVariable("CRYPTOSOFT_DELAY_MS", "1200");

        try
        {
            using var firstProcess = new Process { StartInfo = CreateCryptoSoftStartInfo(firstFilePath, "busy-key") };
            using var secondProcess = new Process { StartInfo = CreateCryptoSoftStartInfo(secondFilePath, "busy-key") };

            Assert.True(firstProcess.Start());
            await Task.Delay(250);
            Assert.True(secondProcess.Start());

            var firstOutputTask = firstProcess.StandardOutput.ReadToEndAsync();
            var secondOutputTask = secondProcess.StandardOutput.ReadToEndAsync();
            await firstProcess.WaitForExitAsync();
            await secondProcess.WaitForExitAsync();

            var firstOutput = await firstOutputTask;
            var secondOutput = await secondOutputTask;

            Assert.True(firstProcess.ExitCode >= 0, $"Expected first CryptoSoft run to succeed but got {firstProcess.ExitCode}. Output: {firstOutput}");
            Assert.Equal(-20, secondProcess.ExitCode);
            Assert.Contains("already running", secondOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CRYPTOSOFT_DELAY_MS", previousDelay);
        }
    }

    [Fact]
    public async Task CryptoSoftEncryptionServiceSerializesConcurrentCallsDespiteMonoInstanceRestriction()
    {
        var firstFilePath = Path.Combine(testRoot, "parallel-first.txt");
        var secondFilePath = Path.Combine(testRoot, "parallel-second.txt");
        await File.WriteAllTextAsync(firstFilePath, "first-payload");
        await File.WriteAllTextAsync(secondFilePath, "second-payload");

        var settings = new AppSettings
        {
            CryptoSoftPath = FindCryptoSoftProjectPath(),
            CryptoKey = "parallel-key"
        };

        var previousDelay = Environment.GetEnvironmentVariable("CRYPTOSOFT_DELAY_MS");
        Environment.SetEnvironmentVariable("CRYPTOSOFT_DELAY_MS", "300");

        try
        {
            var service = new CryptoSoftEncryptionService();

            var results = await Task.WhenAll(
                service.EncryptAsync(firstFilePath, settings),
                service.EncryptAsync(secondFilePath, settings));

            Assert.All(results, result => Assert.True(result >= 0));
            Assert.NotEqual("first-payload", await File.ReadAllTextAsync(firstFilePath));
            Assert.NotEqual("second-payload", await File.ReadAllTextAsync(secondFilePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CRYPTOSOFT_DELAY_MS", previousDelay);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string FindCryptoSoftProjectPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "CryptoSoft", "CryptoSoft.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("CryptoSoft project was not found from the test execution directory.");
    }

    private static ProcessStartInfo CreateCryptoSoftStartInfo(string filePath, string key)
    {
        var projectPath = FindCryptoSoftProjectPath();
        return new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", projectPath, "--", filePath, key },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}
