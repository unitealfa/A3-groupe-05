using EasySave.Core.Models;

namespace EasySave.Core.Services;

public static class BackupPreviewService
{
    public sealed record PlannedTransfer(string SourceFilePath, string DestinationFilePath, long FileSize);

    public static IReadOnlyList<PlannedTransfer> GetPlannedTransfers(BackupJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var targetRoot = Path.GetFullPath(job.TargetDirectory);
        var sourcePaths = SourceSelectionParser
            .Parse(job.SourceDirectory)
            .Select(Path.GetFullPath)
            .ToList();

        return BuildTransfers(sourcePaths, targetRoot);
    }

    public static IReadOnlyList<PlannedTransfer> GetDifferentialPendingTransfers(BackupJob job)
    {
        return GetPlannedTransfers(job)
            .Where(transfer => ShouldCopyDifferential(
                new FileInfo(transfer.SourceFilePath),
                new FileInfo(transfer.DestinationFilePath)))
            .ToList();
    }

    private static List<PlannedTransfer> BuildTransfers(IReadOnlyList<string> sourcePaths, string targetRoot)
    {
        var isMultiSource = sourcePaths.Count > 1;
        var usedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var transfers = new List<PlannedTransfer>();

        foreach (var sourcePath in sourcePaths)
        {
            if (Directory.Exists(sourcePath))
            {
                var sourceRoot = Path.GetFullPath(sourcePath);
                var rootDirectoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceRoot));

                foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sourceRoot, filePath);
                    var destinationPath = isMultiSource
                        ? Path.Combine(targetRoot, rootDirectoryName, relativePath)
                        : Path.Combine(targetRoot, relativePath);

                    transfers.Add(new PlannedTransfer(
                        filePath,
                        EnsureUniqueDestinationPath(destinationPath, usedDestinations),
                        new FileInfo(filePath).Length));
                }

                continue;
            }

            var sourceFile = new FileInfo(sourcePath);
            var destinationDirectory = isMultiSource
                ? Path.Combine(targetRoot, sourceFile.Directory?.Name ?? "files")
                : targetRoot;
            var fileDestinationPath = Path.Combine(destinationDirectory, sourceFile.Name);

            transfers.Add(new PlannedTransfer(
                sourceFile.FullName,
                EnsureUniqueDestinationPath(fileDestinationPath, usedDestinations),
                sourceFile.Exists ? sourceFile.Length : 0));
        }

        return transfers;
    }

    private static bool ShouldCopyDifferential(FileInfo sourceFile, FileInfo destinationFile)
    {
        if (!destinationFile.Exists)
        {
            return true;
        }

        return sourceFile.LastWriteTimeUtc > destinationFile.LastWriteTimeUtc ||
               sourceFile.Length != destinationFile.Length;
    }

    private static string EnsureUniqueDestinationPath(string destinationPath, ISet<string> usedDestinations)
    {
        var candidate = destinationPath;
        var counter = 2;

        while (!usedDestinations.Add(candidate))
        {
            var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
            var filename = Path.GetFileNameWithoutExtension(destinationPath);
            var extension = Path.GetExtension(destinationPath);
            candidate = Path.Combine(directory, $"{filename} ({counter}){extension}");
            counter++;
        }

        return candidate;
    }
}
