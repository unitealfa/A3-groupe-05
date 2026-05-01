using System.Text.Json;
using EasyLog;
using EasySave.Core.Configuration;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Tests;

public sealed class BackupExecutionTests : IDisposable
{
    private readonly string testRoot;

    public BackupExecutionTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), $"easysave-execution-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public async Task CompleteBackupCopiesAllFilesPreservesTreeAndWritesLogs()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-complete");
        var targetDirectory = Path.Combine(testRoot, "target-complete");
        var logDirectory = Path.Combine(testRoot, "logs-complete");
        var statePath = Path.Combine(testRoot, "state-complete", "state.json");
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "nested"));

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "root.txt"), "root-content");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "nested", "child.txt"), "child-content");

        var manager = CreateBackupManager(logDirectory, statePath, sourceDirectory, targetDirectory, BackupType.Complete, "Complete Job");

        await manager.ExecuteJobAsync(1);

        Assert.Equal("root-content", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "root.txt")));
        Assert.Equal("child-content", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "nested", "child.txt")));

        var states = await ReadStateEntriesAsync(statePath);
        var state = Assert.Single(states);
        Assert.Equal("Complete Job", state.Name);
        Assert.Equal("Finished", state.State);
        Assert.Equal(2, state.TotalFilesToCopy);
        Assert.Equal(100, state.Progression);
        Assert.Equal(0, state.RemainingFiles);

        var logEntries = await ReadJsonLogEntriesAsync(logDirectory);
        Assert.Equal(2, logEntries.Count);
        Assert.All(logEntries, entry => Assert.Equal("Success", entry.Status));
        Assert.Contains(logEntries, entry => entry.DestinationFilePath.EndsWith(Path.Combine("nested", "child.txt"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task DifferentialBackupCopiesOnlyMissingOrChangedFiles()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-differential");
        var targetDirectory = Path.Combine(testRoot, "target-differential");
        var logDirectory = Path.Combine(testRoot, "logs-differential");
        var statePath = Path.Combine(testRoot, "state-differential", "state.json");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(targetDirectory);

        var unchangedSourcePath = Path.Combine(sourceDirectory, "unchanged.txt");
        var updatedSourcePath = Path.Combine(sourceDirectory, "updated.txt");
        var missingSourcePath = Path.Combine(sourceDirectory, "missing.txt");

        var unchangedTargetPath = Path.Combine(targetDirectory, "unchanged.txt");
        var updatedTargetPath = Path.Combine(targetDirectory, "updated.txt");

        await File.WriteAllTextAsync(unchangedSourcePath, "same-content");
        await File.WriteAllTextAsync(updatedSourcePath, "new-content");
        await File.WriteAllTextAsync(missingSourcePath, "missing-content");

        await File.WriteAllTextAsync(unchangedTargetPath, "same-content");
        await File.WriteAllTextAsync(updatedTargetPath, "old");

        var synchronizedTime = new DateTime(2026, 4, 26, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(unchangedSourcePath, synchronizedTime);
        File.SetLastWriteTimeUtc(unchangedTargetPath, synchronizedTime);

        File.SetLastWriteTimeUtc(updatedTargetPath, synchronizedTime.AddMinutes(-5));
        File.SetLastWriteTimeUtc(updatedSourcePath, synchronizedTime);

        var unchangedTimestampBeforeExecution = File.GetLastWriteTimeUtc(unchangedTargetPath);

        var manager = CreateBackupManager(logDirectory, statePath, sourceDirectory, targetDirectory, BackupType.Differential, "Differential Job");

        await manager.ExecuteJobAsync(1);

        Assert.Equal("same-content", await File.ReadAllTextAsync(unchangedTargetPath));
        Assert.Equal(unchangedTimestampBeforeExecution, File.GetLastWriteTimeUtc(unchangedTargetPath));
        Assert.Equal("new-content", await File.ReadAllTextAsync(updatedTargetPath));
        Assert.Equal("missing-content", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "missing.txt")));

        var states = await ReadStateEntriesAsync(statePath);
        var state = Assert.Single(states);
        Assert.Equal("Differential Job", state.Name);
        Assert.Equal("Finished", state.State);
        Assert.Equal(2, state.TotalFilesToCopy);
        Assert.Equal(100, state.Progression);

        var logEntries = await ReadJsonLogEntriesAsync(logDirectory);
        Assert.Equal(2, logEntries.Count);
        Assert.DoesNotContain(logEntries, entry => entry.SourceFilePath.EndsWith("unchanged.txt", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry => entry.SourceFilePath.EndsWith("updated.txt", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry => entry.SourceFilePath.EndsWith("missing.txt", StringComparison.Ordinal));
    }

    private BackupManager CreateBackupManager(
        string logDirectory,
        string statePath,
        string sourceDirectory,
        string targetDirectory,
        BackupType backupType,
        string jobName)
    {
        var repository = new BackupJobRepository(Path.Combine(testRoot, $"{jobName}-config", "jobs.json"));
        var jobService = new BackupJobService(repository);
        var stateManager = new StateManager(statePath);
        var logger = new JsonLoggerService(logDirectory);

        jobService.AddJobAsync(new BackupJob
        {
            Name = jobName,
            SourceDirectory = sourceDirectory,
            TargetDirectory = targetDirectory,
            Type = backupType
        }).GetAwaiter().GetResult();

        return new BackupManager(jobService, stateManager, logger);
    }

    private static async Task<List<BackupState>> ReadStateEntriesAsync(string statePath)
    {
        var content = await File.ReadAllTextAsync(statePath);
        return JsonSerializer.Deserialize<List<BackupState>>(content) ?? [];
    }

    private static async Task<List<LogEntry>> ReadJsonLogEntriesAsync(string logDirectory)
    {
        var logPath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");
        var content = await File.ReadAllTextAsync(logPath);
        return JsonSerializer.Deserialize<List<LogEntry>>(content) ?? [];
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
