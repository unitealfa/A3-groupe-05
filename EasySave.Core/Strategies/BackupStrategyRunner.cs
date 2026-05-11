using System.Diagnostics;
using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Core.Strategies;

internal static class BackupStrategyRunner
{
    private sealed record PlannedTransfer(FileInfo SourceFile, string DestinationPath);

    public static async Task ExecuteAsync(
        BackupJob job,
        BackupExecutionContext context,
        Func<FileInfo, FileInfo, bool> shouldCopy,
        CancellationToken cancellationToken)
    {
        var targetRoot = Path.GetFullPath(job.TargetDirectory);
        var sourcePaths = SourceSelectionParser
            .Parse(job.SourceDirectory)
            .Select(Path.GetFullPath)
            .ToList();
        var allTransfers = BuildTransfers(sourcePaths, targetRoot);
        var plannedFiles = allTransfers
            .Where(transfer => shouldCopy(transfer.SourceFile, new FileInfo(transfer.DestinationPath)))
            .ToList();
        var remainingPriorityFiles = plannedFiles.Count(transfer => context.Settings.IsPriorityFile(transfer.SourceFile.FullName));

        context.PriorityFileCoordinator.RegisterPriorityFiles(remainingPriorityFiles);

        Directory.CreateDirectory(targetRoot);

        var totalSize = plannedFiles.Sum(file => file.SourceFile.Length);
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

        try
        {
            foreach (var transfer in plannedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WaitForBusinessSoftwareToStopAsync(context, state, cancellationToken);

                if (context.PauseController.IsPaused)
                {
                    state.State = "Paused";
                    await context.StateManager.UpdateAsync(state, cancellationToken);
                }

                var resumedFromPause = await context.PauseController.WaitWhilePausedAsync(cancellationToken);
                if (resumedFromPause)
                {
                    state.State = "Active";
                    await context.StateManager.UpdateAsync(state, cancellationToken);
                }

                var sourceFile = transfer.SourceFile;
                var destinationPath = transfer.DestinationPath;
                var isPriorityFile = context.Settings.IsPriorityFile(sourceFile.FullName);

                if (!isPriorityFile)
                {
                    await context.PriorityFileCoordinator.WaitUntilNonPriorityTransfersAllowedAsync(cancellationToken);
                }

                state.CurrentSourceFilePath = sourceFile.FullName;
                state.CurrentDestinationFilePath = destinationPath;
                await context.StateManager.UpdateAsync(state, cancellationToken);

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await using var largeFileTransferLease = await context.LargeFileTransferCoordinator
                        .AcquireAsync(sourceFile.Length, context.Settings, cancellationToken);
                    await context.FileTransferService.CopyAsync(sourceFile.FullName, destinationPath, overwrite: true, cancellationToken);
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
                finally
                {
                    if (isPriorityFile)
                    {
                        remainingPriorityFiles--;
                        context.PriorityFileCoordinator.CompletePriorityFile();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            context.PriorityFileCoordinator.ReleaseUnprocessedPriorityFiles(remainingPriorityFiles);
            throw;
        }

        state.State = hasError ? "Error" : "Finished";
        state.CurrentSourceFilePath = string.Empty;
        state.CurrentDestinationFilePath = string.Empty;
        state.RemainingFiles = 0;
        state.RemainingSize = 0;
        state.Progression = plannedFiles.Count == 0 ? 100 : state.Progression;
        await context.StateManager.UpdateAsync(state, cancellationToken);
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
                        new FileInfo(filePath),
                        EnsureUniqueDestinationPath(destinationPath, usedDestinations)));
                }

                continue;
            }

            var sourceFile = new FileInfo(sourcePath);
            var destinationDirectory = isMultiSource
                ? Path.Combine(targetRoot, sourceFile.Directory?.Name ?? "files")
                : targetRoot;
            var fileDestinationPath = Path.Combine(destinationDirectory, sourceFile.Name);

            transfers.Add(new PlannedTransfer(
                sourceFile,
                EnsureUniqueDestinationPath(fileDestinationPath, usedDestinations)));
        }

        return transfers;
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

    private static async Task WaitForBusinessSoftwareToStopAsync(
        BackupExecutionContext context,
        BackupState state,
        CancellationToken cancellationToken)
    {
        var detection = context.BusinessSoftwareDetector.Detect(context.Settings);
        if (!detection.IsDetected)
        {
            context.IsBlockedByBusinessSoftware = false;
            return;
        }

        if (!context.IsBlockedByBusinessSoftware)
        {
            context.IsBlockedByBusinessSoftware = true;
            state.State = "Paused";
            await context.StateManager.UpdateAsync(state, cancellationToken);
        }

        while (detection.IsDetected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            detection = context.BusinessSoftwareDetector.Detect(context.Settings);
        }

        context.IsBlockedByBusinessSoftware = false;
        state.State = "Active";
        await context.StateManager.UpdateAsync(state, cancellationToken);
    }
}
