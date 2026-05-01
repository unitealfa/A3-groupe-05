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

        var initialDetection = context.BusinessSoftwareDetector.Detect(context.Settings);
        if (initialDetection.IsDetected)
        {
            await StopForBusinessSoftwareAsync(job, context, state, initialDetection.ProcessName, cancellationToken);
            return;
        }

        var copiedFiles = 0;
        var remainingSize = totalSize;
        var hasError = false;
        var blockedByBusinessSoftware = false;
        var blockingProcessName = string.Empty;

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

                var encryptionTimeMs = 0L;
                string status = "Success";
                string? errorMessage = null;

                if (context.Settings.ShouldEncrypt(destinationPath))
                {
                    encryptionTimeMs = await context.FileEncryptionService.EncryptAsync(destinationPath, context.Settings, cancellationToken);
                    if (encryptionTimeMs < 0)
                    {
                        hasError = true;
                        state.State = "Error";
                        status = "Error";
                        errorMessage = $"Encryption failed with code {encryptionTimeMs}.";
                    }
                }

                copiedFiles++;
                remainingSize -= sourceFile.Length;
                UpdateProgress(state, plannedFiles.Count, copiedFiles, remainingSize);
                await context.Logger.LogAsync(CreateLogEntry(job, sourceFile.FullName, destinationPath, sourceFile.Length, stopwatch.ElapsedMilliseconds, encryptionTimeMs, status, errorMessage), cancellationToken);
                await context.StateManager.UpdateAsync(state, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                hasError = true;
                copiedFiles++;
                remainingSize -= sourceFile.Length;
                state.State = "Error";
                UpdateProgress(state, plannedFiles.Count, copiedFiles, remainingSize);
                await context.Logger.LogAsync(CreateLogEntry(job, sourceFile.FullName, destinationPath, sourceFile.Length, stopwatch.ElapsedMilliseconds, -1, "Error", exception.Message), cancellationToken);
                await context.StateManager.UpdateAsync(state, cancellationToken);
            }

            var detection = context.BusinessSoftwareDetector.Detect(context.Settings);
            if (detection.IsDetected)
            {
                blockedByBusinessSoftware = true;
                blockingProcessName = detection.ProcessName;
                break;
            }
        }

        if (blockedByBusinessSoftware)
        {
            await StopForBusinessSoftwareAsync(job, context, state, blockingProcessName, cancellationToken);
            return;
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
        long encryptionTimeMs,
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
            EncryptionTimeMs = encryptionTimeMs,
            Status = status,
            ErrorMessage = errorMessage
        };
    }

    private static async Task StopForBusinessSoftwareAsync(
        BackupJob job,
        BackupExecutionContext context,
        BackupState state,
        string processName,
        CancellationToken cancellationToken)
    {
        context.IsBlockedByBusinessSoftware = true;
        state.State = "Blocked";
        state.CurrentSourceFilePath = string.Empty;
        state.CurrentDestinationFilePath = string.Empty;

        await context.Logger.LogAsync(
            new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = job.Name,
                Status = "Blocked",
                TransferTimeMs = 0,
                EncryptionTimeMs = 0,
                ErrorMessage = $"Business software detected: {processName}"
            },
            cancellationToken);

        await context.StateManager.UpdateAsync(state, cancellationToken);
    }
}
