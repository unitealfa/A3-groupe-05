using EasyLog;

namespace EasySave.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";

    public string LogFormatName { get; set; } = "json";

    public List<string> EncryptedExtensions { get; set; } = ["*"];

    public List<string> PriorityExtensions { get; set; } = [];

    public List<string> BusinessSoftwareProcesses { get; set; } = [];

    public int LargeFileThresholdKo { get; set; }

    public string CryptoSoftPath { get; set; } = string.Empty;

    public string CryptoKey { get; set; } = "EasySave";

    public LogFormat LogFormat => string.Equals(LogFormatName, "xml", StringComparison.OrdinalIgnoreCase)
        ? LogFormat.Xml
        : LogFormat.Json;

    public bool ShouldEncrypt(string filePath)
    {
        var normalizedExtensions = GetNormalizedEncryptedExtensions();
        if (normalizedExtensions.Count == 0 ||
            normalizedExtensions.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return true;
        }

        return normalizedExtensions
            .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetNormalizedEncryptedExtensions()
    {
        return NormalizeExtensions(EncryptedExtensions);
    }

    public bool IsPriorityFile(string filePath)
    {
        var normalizedExtensions = GetNormalizedPriorityExtensions();
        if (normalizedExtensions.Count == 0)
        {
            return false;
        }

        if (normalizedExtensions.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return normalizedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetNormalizedPriorityExtensions()
    {
        return NormalizeExtensions(PriorityExtensions);
    }

    public long GetLargeFileThresholdBytes()
    {
        return LargeFileThresholdKo <= 0
            ? 0
            : LargeFileThresholdKo * 1024L;
    }

    public bool IsLargeFile(long fileSizeBytes)
    {
        var thresholdBytes = GetLargeFileThresholdBytes();
        return thresholdBytes > 0 && fileSizeBytes > thresholdBytes;
    }

    private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Select(value => value is "*" or "*.*" or ".*" ? "*" : value)
            .Select(value => value == "*" || value.StartsWith('.') ? value : $".{value}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetNormalizedBusinessSoftwareProcesses()
    {
        return BusinessSoftwareProcesses
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value.Trim()))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
