namespace EasySave.Core.Services;

public static class SourceSelectionParser
{
    public static IReadOnlyList<string> Parse(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsExistingSource(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }
}
