using System.Diagnostics;
using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Core.Strategies;

internal static class BackupStrategyRunner
{
    private const int LiveProgressUpdateStepBytes = 1024 * 1024;
    private static readonly TimeSpan LiveProgressUpdateInterval = TimeSpan.FromMilliseconds(250);
    private sealed record PlannedTransfer(FileInfo SourceFile, string DestinationPath);

    public static async Task ExecuteAsync(
        BackupJob job,
        BackupExecutionContext context,
        Func<FileInfo, FileInfo, bool> shouldCopy,
        CancellationToken cancellationToken)
    {
        var registrationCompleted = false;
        var priorityJobRegistered = false;
        try
        {
            var targetRoot = Path.GetFullPath(job.TargetDirectory);
            var allTransfers = BackupPreviewService
                .GetPlannedTransfers(job)
                .Select(transfer => new PlannedTransfer(new FileInfo(transfer.SourceFilePath), transfer.DestinationFilePath))
                .ToList();
            var plannedFiles = allTransfers
                .Where(transfer => shouldCopy(transfer.SourceFile, new FileInfo(transfer.DestinationPath)))
                .OrderByDescending(transfer => context.Settings.IsPriorityFile(transfer.SourceFile.FullName))
                .ToList();
            var remainingPriorityFiles = plannedFiles.Count(transfer => context.Settings.IsPriorityFile(transfer.SourceFile.FullName));
            var hasPriorityFiles = remainingPriorityFiles > 0;

            context.PriorityFileCoordinator.RegisterPriorityFiles(remainingPriorityFiles);
            if (hasPriorityFiles)
            {
                context.PriorityFileCoordinator.RegisterPriorityJob();
                priorityJobRegistered = true;
            }
            context.PriorityFileCoordinator.CompleteRegistrationForJob();
            registrationCompleted = true;

            Directory.CreateDirectory(targetRoot);

            var totalSize = plannedFiles.Sum(file => file.SourceFile.Length);
            var state = new BackupState
            {
                Name = job.Name,
                State = "Active",
                CurrentSourceFilePath = job.SourceDirectory,
                CurrentDestinationFilePath = job.TargetDirectory,
                ErrorMessage = string.Empty,
                TotalFilesToCopy = plannedFiles.Count,
                TotalFilesSize = totalSize,
                RemainingFiles = plannedFiles.Count,
                RemainingSize = totalSize
            };
            await context.StateManager.UpdateAsync(state, cancellationToken);

            var copiedFiles = 0;
            var remainingSize = totalSize;
            var hasError = false;

            if (!hasPriorityFiles)
            {
                state.State = "Blocked";
                await context.StateManager.UpdateAsync(state, cancellationToken);
                await context.PriorityFileCoordinator.WaitUntilNonPriorityTransfersAllowedAsync(cancellationToken);
                state.State = "Active";
                await context.StateManager.UpdateAsync(state, cancellationToken);
            }

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

                    state.CurrentSourceFilePath = sourceFile.FullName;
                    state.CurrentDestinationFilePath = destinationPath;
                    await context.StateManager.UpdateAsync(state, cancellationToken);

                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        long lastReportedTransferredBytes = 0;
                        long lastPersistedTransferredBytes = 0;
                        var lastLiveProgressUpdateAt = DateTime.UtcNow;
                        if (context.Settings.IsLargeFile(sourceFile.Length))
                        {
                            state.State = "Blocked";
                            await context.StateManager.UpdateAsync(state, cancellationToken);
                        }

                        await using var largeFileTransferLease = await context.LargeFileTransferCoordinator
                            .AcquireAsync(sourceFile.Length, context.Settings, cancellationToken);
                        if (context.Settings.IsLargeFile(sourceFile.Length))
                        {
                            state.State = "Active";
                            await context.StateManager.UpdateAsync(state, cancellationToken);
                        }
                        await context.FileTransferService.CopyAsync(
                            sourceFile.FullName,
                            destinationPath,
                            overwrite: true,
                            async transferredBytes =>
                            {
                                var transferredDelta = transferredBytes - lastReportedTransferredBytes;
                                if (transferredDelta <= 0)
                                {
                                    return;
                                }

                                lastReportedTransferredBytes = transferredBytes;
                                var liveRemainingSize = Math.Max(0, remainingSize - transferredBytes);
                                UpdateProgressWithPartialFile(state, plannedFiles.Count, copiedFiles, liveRemainingSize, transferredBytes, sourceFile.Length);

                                var shouldPersistLiveProgress =
                                    transferredBytes >= sourceFile.Length ||
                                    transferredBytes - lastPersistedTransferredBytes >= LiveProgressUpdateStepBytes ||
                                    DateTime.UtcNow - lastLiveProgressUpdateAt >= LiveProgressUpdateInterval;
                                if (!shouldPersistLiveProgress)
                                {
                                    return;
                                }

                                lastPersistedTransferredBytes = transferredBytes;
                                lastLiveProgressUpdateAt = DateTime.UtcNow;
                                await context.StateManager.UpdateAsync(state, cancellationToken);
                            },
                            cancellationToken);
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
                                state.ErrorMessage = $"Encryption failed with code {encryptionTimeMs}.";
                                status = "Error";
                                errorMessage = state.ErrorMessage;
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
                        state.ErrorMessage = exception.Message;
                        UpdateProgress(state, plannedFiles.Count, copiedFiles, remainingSize);
                        await context.Logger.LogAsync(CreateLogEntry(job, sourceFile.FullName, destinationPath, sourceFile.Length, ToNegativeMetric(stopwatch.ElapsedMilliseconds), -1, "Error", exception.Message), cancellationToken);
                        await context.StateManager.UpdateAsync(state, cancellationToken);
                        break;
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

            if (hasError && remainingPriorityFiles > 0)
            {
                context.PriorityFileCoordinator.ReleaseUnprocessedPriorityFiles(remainingPriorityFiles);
                remainingPriorityFiles = 0;
            }

            state.State = hasError ? "Error" : "Finished";
            if (!hasError)
            {
                state.CurrentSourceFilePath = job.SourceDirectory;
                state.CurrentDestinationFilePath = job.TargetDirectory;
            }
            if (!hasError)
            {
                state.ErrorMessage = string.Empty;
            }
            state.RemainingFiles = 0;
            state.RemainingSize = 0;
            state.Progression = plannedFiles.Count == 0 ? 100 : state.Progression;
            await context.StateManager.UpdateAsync(state, cancellationToken);

            if (!hasError)
            {
                await context.Logger.LogAsync(CreateLogEntry(
                    job,
                    job.SourceDirectory,
                    job.TargetDirectory,
                    totalSize,
                    0,
                    0,
                    "Success",
                    plannedFiles.Count == 0
                        ? allTransfers.Count == 0
                            ? "Backup launched with no file to copy."
                            : "No file changed."
                        : "Backup finished."), cancellationToken);
            }
        }
        finally
        {
            if (priorityJobRegistered)
            {
                context.PriorityFileCoordinator.CompletePriorityJob();
            }

            if (!registrationCompleted)
            {
                context.PriorityFileCoordinator.CompleteRegistrationForJob();
            }
        }
    }

    private static void UpdateProgress(BackupState state, int totalFiles, int copiedFiles, long remainingSize)
    {
        state.RemainingFiles = Math.Max(0, totalFiles - copiedFiles);
        state.RemainingSize = Math.Max(0, remainingSize);
        state.Progression = totalFiles == 0 ? 100 : Math.Round((double)copiedFiles / totalFiles * 100, 2);
    }

    private static void UpdateProgressWithPartialFile(
        BackupState state,
        int totalFiles,
        int copiedFiles,
        long remainingSize,
        long currentFileTransferredBytes,
        long currentFileTotalBytes)
    {
        state.RemainingFiles = Math.Max(0, totalFiles - copiedFiles);
        state.RemainingSize = Math.Max(0, remainingSize);

        if (totalFiles == 0)
        {
            state.Progression = 100;
            return;
        }

        var partialFileProgress = currentFileTotalBytes <= 0
            ? 0
            : Math.Clamp((double)currentFileTransferredBytes / currentFileTotalBytes, 0, 1);
        state.Progression = Math.Round(((copiedFiles + partialFileProgress) / totalFiles) * 100, 2);
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
        if (string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase) &&
            transferTimeMs >= 0 &&
            encryptionTimeMs >= 0)
        {
            transferTimeMs = ToNegativeMetric(transferTimeMs);
        }

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

    private static long ToNegativeMetric(long value)
    {
        return value <= 0 ? -1 : -Math.Abs(value);
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
