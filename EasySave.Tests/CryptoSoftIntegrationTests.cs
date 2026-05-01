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
}
