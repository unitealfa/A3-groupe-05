using System.Diagnostics;
using EasyLog;
using EasySave.Core.Models;

namespace EasySave.Core.Strategies;

internal static class BackupStrategyRunner
{
    public static async Task ExecuteAsync(
        BackupJob job,
        BackupExecutionContext context,
        Func<FileInfo, FileInfo, bool> shouldCopy,
        CancellationToken cancellationToken)
    {
        var sourceRoot = Path.GetFullPath(job.SourceDirectory);
        var targetRoot = Path.GetFullPath(job.TargetDirectory);
        var allSourceFiles = Directory
            .EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Select(filePath => new FileInfo(filePath))
            .ToList();
        var plannedFiles = allSourceFiles
            .Where(sourceFile => shouldCopy(sourceFile, new FileInfo(GetDestinationPath(sourceRoot, targetRoot, sourceFile.FullName))))
            .ToList();

        Directory.CreateDirectory(targetRoot);

        var totalSize = plannedFiles.Sum(file => file.Length);
        var state = new BackupState
        {
            Name = job.Name,
            State = "Active",
            TotalFilesToCopy = plannedFiles.Count,
            TotalFilesSize = totalSize,
            RemainingFiles = plannedFiles.Count,
            RemainingSize = totalSize
        };
        await context.StateManager.UpdateAsync(state, cancellationToken);

        var copiedFiles = 0;
        var remainingSize = totalSize;
        var hasError = false;

        foreach (var sourceFile in plannedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = GetDestinationPath(sourceRoot, targetRoot, sourceFile.FullName);
            state.CurrentSourceFilePath = sourceFile.FullName;
            state.CurrentDestinationFilePath = destinationPath;
            await context.StateManager.UpdateAsync(state, cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                sourceFile.CopyTo(destinationPath, overwrite: true);
                stopwatch.Stop();

                copiedFiles++;
                remainingSize -= sourceFile.Length;
                UpdateProgress(state, plannedFiles.Count, copiedFiles, remainingSize);
                await context.Logger.LogAsync(CreateLogEntry(job, sourceFile.FullName, destinationPath, sourceFile.Length, stopwatch.ElapsedMilliseconds, "Success"), cancellationToken);
                await context.StateManager.UpdateAsync(state, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                stopwatch.Stop();
                hasError = true;
                copiedFiles++;
                remainingSize -= sourceFile.Length;
                state.State = "Error";
                UpdateProgress(state, plannedFiles.Count, copiedFiles, remainingSize);
                await context.Logger.LogAsync(CreateLogEntry(job, sourceFile.FullName, destinationPath, sourceFile.Length, stopwatch.ElapsedMilliseconds, "Error", exception.Message), cancellationToken);
                await context.StateManager.UpdateAsync(state, cancellationToken);
            }
        }

        state.State = hasError ? "Error" : "Finished";
        state.CurrentSourceFilePath = string.Empty;
        state.CurrentDestinationFilePath = string.Empty;
        state.RemainingFiles = 0;
        state.RemainingSize = 0;
        state.Progression = plannedFiles.Count == 0 ? 100 : state.Progression;
        await context.StateManager.UpdateAsync(state, cancellationToken);
    }

    private static string GetDestinationPath(string sourceRoot, string targetRoot, string sourceFilePath)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, sourceFilePath);
        return Path.Combine(targetRoot, relativePath);
    }

    private static void UpdateProgress(BackupState state, int totalFiles, int copiedFiles, long remainingSize)
    {
        state.RemainingFiles = Math.Max(0, totalFiles - copiedFiles);
        state.RemainingSize = Math.Max(0, remainingSize);
        state.Progression = totalFiles == 0 ? 100 : Math.Round((double)copiedFiles / totalFiles * 100, 2);
    }

    private static LogEntry CreateLogEntry(
        BackupJob job,
        string sourceFilePath,
        string destinationFilePath,
        long fileSize,
        long transferTimeMs,
        string status,
        string? errorMessage = null)
    {
        return new LogEntry
        {
            Timestamp = DateTime.Now,
            BackupName = job.Name,
            SourceFilePath = sourceFilePath,
            DestinationFilePath = destinationFilePath,
            FileSize = fileSize,
            TransferTimeMs = transferTimeMs,
            Status = status,
            ErrorMessage = errorMessage
        };
    }
}
