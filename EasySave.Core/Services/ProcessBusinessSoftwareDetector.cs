using System.Diagnostics;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class ProcessBusinessSoftwareDetector : IBusinessSoftwareDetector
{
    private static readonly Dictionary<string, string[]> ProcessAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["calc"] = ["calc", "calculator", "win32calc"],
        ["calculator"] = ["calc", "calculator", "win32calc"],
        ["win32calc"] = ["calc", "calculator", "win32calc"],
        ["notepad"] = ["notepad", "blocnotes"],
        ["blocnotes"] = ["notepad", "blocnotes"]
    };

    public BusinessSoftwareDetectionResult Detect(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuredNames = ExpandAliases(settings.GetNormalizedBusinessSoftwareProcesses());
        if (configuredNames.Count == 0)
        {
            return BusinessSoftwareDetectionResult.None;
        }

        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    if (ExpandAliases([process.ProcessName]).Overlaps(configuredNames))
                    {
                        return new BusinessSoftwareDetectionResult(true, process.ProcessName);
                    }
                }
            }
        }
        catch
        {
            return BusinessSoftwareDetectionResult.None;
        }

        return BusinessSoftwareDetectionResult.None;
    }

    private static HashSet<string> ExpandAliases(IEnumerable<string> names)
    {
        var expandedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var normalizedName = name.Trim();
            expandedNames.Add(normalizedName);

            if (ProcessAliases.TryGetValue(normalizedName, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    expandedNames.Add(alias);
                }
            }
        }

        return expandedNames;
    }
}
