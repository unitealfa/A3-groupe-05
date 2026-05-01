using System.Diagnostics;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class ProcessBusinessSoftwareDetector : IBusinessSoftwareDetector
{
    public BusinessSoftwareDetectionResult Detect(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuredNames = settings.GetNormalizedBusinessSoftwareProcesses();
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
                    if (configuredNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
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
}
