using EasySave.Core.Models;

namespace EasySave.Core.Services;

public interface IBusinessSoftwareDetector
{
    BusinessSoftwareDetectionResult Detect(AppSettings settings);
}

public sealed record BusinessSoftwareDetectionResult(bool IsDetected, string ProcessName)
{
    public static readonly BusinessSoftwareDetectionResult None = new(false, string.Empty);
}
