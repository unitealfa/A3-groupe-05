using System.Text.Json;
using EasySave.Core.Models;

namespace EasySave.Core.Configuration;

public sealed class AppSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly string settingsFilePath;

    public AppSettingsRepository(string settingsFilePath)
    {
        this.settingsFilePath = settingsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadSettingsUnsafeAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
            await using var stream = File.Create(settingsFilePath);
            await JsonSerializer.SerializeAsync(stream, Normalize(settings), JsonOptions, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task<AppSettings> ReadSettingsUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsFilePath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
        return Normalize(settings ?? new AppSettings());
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.Language = settings.Language is "fr" or "en" ? settings.Language : "en";
        settings.LogFormatName = string.Equals(settings.LogFormatName, "xml", StringComparison.OrdinalIgnoreCase) ? "xml" : "json";
        settings.EncryptedExtensions = settings.GetNormalizedEncryptedExtensions().ToList();
        settings.PriorityExtensions = settings.GetNormalizedPriorityExtensions().ToList();
        settings.LargeFileThresholdKo = Math.Max(0, settings.LargeFileThresholdKo);
        if (settings.EncryptedExtensions.Count == 0)
        {
            settings.EncryptedExtensions = ["*"];
        }

        settings.BusinessSoftwareProcesses = settings.GetNormalizedBusinessSoftwareProcesses().ToList();
        settings.CryptoKey = string.IsNullOrWhiteSpace(settings.CryptoKey) ? "EasySave" : settings.CryptoKey.Trim();
        settings.CryptoSoftPath = settings.CryptoSoftPath?.Trim() ?? string.Empty;
        return settings;
    }
}
