using System.Diagnostics;
using System.Text.RegularExpressions;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class CryptoSoftEncryptionService : IFileEncryptionService
{
    private const int CryptoSoftBusyExitCode = -20;
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan BusyRetryTimeout = TimeSpan.FromSeconds(30);
    private static readonly Regex ElapsedTimeRegex = new(@"ElapsedTimeMs=(?<value>-?\d+)", RegexOptions.Compiled);
    private static readonly SemaphoreSlim SingleInstanceExecutionLock = new(1, 1);

    public async Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(settings);

        var targetPath = ResolveTargetPath(settings.CryptoSoftPath);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return -10;
        }

        await SingleInstanceExecutionLock.WaitAsync(cancellationToken);
        try
        {
            return await EncryptWithRetryAsync(targetPath, filePath, settings.CryptoKey, cancellationToken);
        }
        finally
        {
            SingleInstanceExecutionLock.Release();
        }
    }

    private static async Task<long> EncryptWithRetryAsync(string targetPath, string filePath, string key, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(BusyRetryTimeout);

        while (true)
        {
            var result = await ExecuteCryptoSoftAsync(targetPath, filePath, key, cancellationToken);
            if (result != CryptoSoftBusyExitCode)
            {
                return result;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return CryptoSoftBusyExitCode;
            }

            await Task.Delay(BusyRetryDelay, cancellationToken);
        }
    }

    private static async Task<long> ExecuteCryptoSoftAsync(string targetPath, string filePath, string key, CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(targetPath, filePath, key);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch
        {
            return -11;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;
        var parsed = TryParseElapsedTime(output) ?? TryParseElapsedTime(error);

        if (parsed.HasValue)
        {
            return parsed.Value;
        }

        if (process.ExitCode == CryptoSoftBusyExitCode)
        {
            return CryptoSoftBusyExitCode;
        }

        return process.ExitCode == 0 ? -12 : -Math.Abs(process.ExitCode);
    }

    private static ProcessStartInfo CreateStartInfo(string targetPath, string filePath, string key)
    {
        if (Directory.Exists(targetPath))
        {
            var runnablePath = TryResolveRunnablePath(targetPath);
            if (!string.IsNullOrWhiteSpace(runnablePath))
            {
                return CreateStartInfoFromResolvedPath(runnablePath, filePath, key);
            }

            return new ProcessStartInfo
            {
                FileName = targetPath,
                ArgumentList = { filePath, key },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return CreateStartInfoFromResolvedPath(targetPath, filePath, key);
    }

    private static ProcessStartInfo CreateStartInfoFromResolvedPath(string targetPath, string filePath, string key)
    {
        if (targetPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return CreateDotnetProjectStartInfo(targetPath, filePath, key);
        }

        if (targetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { targetPath, filePath, key },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            ApplyDotnetEnvironment(startInfo);
            return startInfo;
        }

        return new ProcessStartInfo
        {
            FileName = targetPath,
            ArgumentList = { filePath, key },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static ProcessStartInfo CreateDotnetProjectStartInfo(string projectPath, string filePath, string key)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", projectPath, "--", filePath, key },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ApplyDotnetEnvironment(startInfo);
        return startInfo;
    }

    private static string ResolveTargetPath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath) || Directory.Exists(configuredPath))
            {
                return configuredPath;
            }

            var relativeMatch = SearchUpwardsForCryptoSoft(configuredPath);
            return string.IsNullOrWhiteSpace(relativeMatch) ? configuredPath : relativeMatch;
        }

        return SearchUpwardsForCryptoSoft("CryptoSoft");
    }

    private static string SearchUpwardsForCryptoSoft(string relativePath)
    {
        foreach (var root in GetSearchRoots())
        {
            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativePath);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    var runnablePath = Directory.Exists(candidate)
                        ? TryResolveRunnablePath(candidate)
                        : candidate;
                    return string.IsNullOrWhiteSpace(runnablePath) ? candidate : runnablePath;
                }

                var projectCandidate = Path.Combine(current.FullName, relativePath, "CryptoSoft.csproj");
                if (File.Exists(projectCandidate))
                {
                    var basePath = Path.Combine(current.FullName, relativePath);
                    var runnablePath = TryResolveRunnablePath(basePath);
                    return string.IsNullOrWhiteSpace(runnablePath) ? basePath : runnablePath;
                }

                current = current.Parent;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }

    private static string TryResolveRunnablePath(string directoryPath)
    {
        var candidates = new[]
        {
            Path.Combine(directoryPath, "CryptoSoft.exe"),
            Path.Combine(directoryPath, "CryptoSoft.dll"),
            Path.Combine(directoryPath, "bin", "Debug", "net8.0", "CryptoSoft.exe"),
            Path.Combine(directoryPath, "bin", "Debug", "net8.0", "CryptoSoft.dll"),
            Path.Combine(directoryPath, "bin", "Release", "net8.0", "CryptoSoft.exe"),
            Path.Combine(directoryPath, "bin", "Release", "net8.0", "CryptoSoft.dll"),
            Path.Combine(directoryPath, "CryptoSoft.csproj")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static void ApplyDotnetEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
    }

    private static long? TryParseElapsedTime(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = ElapsedTimeRegex.Match(output);
        if (!match.Success)
        {
            return null;
        }

        return long.TryParse(match.Groups["value"].Value, out var parsed) ? parsed : null;
    }
}
