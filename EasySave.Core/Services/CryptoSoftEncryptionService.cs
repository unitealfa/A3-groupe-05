using System.Diagnostics;
using System.Text.RegularExpressions;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class CryptoSoftEncryptionService : IFileEncryptionService
{
    private static readonly Regex ElapsedTimeRegex = new(@"ElapsedTimeMs=(?<value>-?\d+)", RegexOptions.Compiled);

    public async Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(settings);

        var targetPath = ResolveTargetPath(settings.CryptoSoftPath);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return -10;
        }

        var startInfo = CreateStartInfo(targetPath, filePath, settings.CryptoKey);
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

        return parsed ?? process.ExitCode;
    }

    private static ProcessStartInfo CreateStartInfo(string targetPath, string filePath, string key)
    {
        if (Directory.Exists(targetPath))
        {
            var projectPath = Path.Combine(targetPath, "CryptoSoft.csproj");
            if (File.Exists(projectPath))
            {
                return CreateDotnetProjectStartInfo(projectPath, filePath, key);
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

        if (targetPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return CreateDotnetProjectStartInfo(targetPath, filePath, key);
        }

        if (targetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { targetPath, filePath, key },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
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
                    return candidate;
                }

                var projectCandidate = Path.Combine(current.FullName, relativePath, "CryptoSoft.csproj");
                if (File.Exists(projectCandidate))
                {
                    return Path.Combine(current.FullName, relativePath);
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
