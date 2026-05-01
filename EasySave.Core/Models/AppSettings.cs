using EasyLog;

namespace EasySave.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";

    public string LogFormatName { get; set; } = "json";

    public List<string> EncryptedExtensions { get; set; } = [];

    public List<string> BusinessSoftwareProcesses { get; set; } = [];

    public string CryptoSoftPath { get; set; } = string.Empty;

    public string CryptoKey { get; set; } = "EasySave";

    public LogFormat LogFormat => string.Equals(LogFormatName, "xml", StringComparison.OrdinalIgnoreCase)
        ? LogFormat.Xml
        : LogFormat.Json;

    public bool ShouldEncrypt(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return GetNormalizedEncryptedExtensions()
            .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetNormalizedEncryptedExtensions()
    {
        return EncryptedExtensions
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Select(value => value.StartsWith('.') ? value : $".{value}")
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
