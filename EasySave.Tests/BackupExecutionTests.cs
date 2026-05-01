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

    [Fact]
    public async Task CompleteBackupEncryptsConfiguredExtensionsAndStoresEncryptionTime()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-encrypt");
        var targetDirectory = Path.Combine(testRoot, "target-encrypt");
        var logDirectory = Path.Combine(testRoot, "logs-encrypt");
        var statePath = Path.Combine(testRoot, "state-encrypt", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-encrypt", "settings.json");
        Directory.CreateDirectory(sourceDirectory);

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "secret.txt"), "to-encrypt");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "plain.log"), "plain-text");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            EncryptedExtensions = [".txt"]
        });

        var encryptionService = new FakeEncryptionService(
            path => Path.GetFileName(path).Equals("secret.txt", StringComparison.OrdinalIgnoreCase) ? 37 : 0);

        var manager = CreateConfiguredBackupManager(
            logDirectory,
            statePath,
            sourceDirectory,
            targetDirectory,
            BackupType.Complete,
            "Encrypted Job",
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            encryptionService);

        await manager.ExecuteJobAsync(1);

        Assert.Single(encryptionService.EncryptedFiles);
        Assert.EndsWith("secret.txt", encryptionService.EncryptedFiles[0], StringComparison.Ordinal);

        var logEntries = await ReadJsonLogEntriesAsync(logDirectory);
        Assert.Equal(2, logEntries.Count);
        Assert.Contains(logEntries, entry => entry.SourceFilePath.EndsWith("secret.txt", StringComparison.Ordinal) && entry.EncryptionTimeMs == 37);
        Assert.Contains(logEntries, entry => entry.SourceFilePath.EndsWith("plain.log", StringComparison.Ordinal) && entry.EncryptionTimeMs == 0);
    }

    [Fact]
    public async Task BackupStopsWhenBusinessSoftwareIsDetected()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-blocked");
        var targetDirectory = Path.Combine(testRoot, "target-blocked");
        var logDirectory = Path.Combine(testRoot, "logs-blocked");
        var statePath = Path.Combine(testRoot, "state-blocked", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-blocked", "settings.json");
        Directory.CreateDirectory(sourceDirectory);

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "a.txt"), "A");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "b.txt"), "B");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            BusinessSoftwareProcesses = ["calc"]
        });

        var detector = new FakeBusinessSoftwareDetector(
            [BusinessSoftwareDetectionResult.None, new BusinessSoftwareDetectionResult(true, "calc")]);

        var manager = CreateConfiguredBackupManager(
            logDirectory,
            statePath,
            sourceDirectory,
            targetDirectory,
            BackupType.Complete,
            "Blocked Job",
            settingsRepository,
            detector,
            new FakeEncryptionService(_ => 0));

        await manager.ExecuteJobAsync(1);

        var copiedFiles = Directory.Exists(targetDirectory)
            ? Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories)
            : [];
        Assert.Single(copiedFiles);

        var states = await ReadStateEntriesAsync(statePath);
        var state = Assert.Single(states);
        Assert.Equal("Blocked", state.State);

        var logEntries = await ReadJsonLogEntriesAsync(logDirectory);
        Assert.Contains(logEntries, entry => entry.Status == "Blocked" && entry.ErrorMessage!.Contains("calc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAllJobsStopsSequenceAfterBusinessSoftwareDetection()
    {
        var sourceOne = Path.Combine(testRoot, "source-sequence-1");
        var sourceTwo = Path.Combine(testRoot, "source-sequence-2");
        var targetRoot = Path.Combine(testRoot, "target-sequence");
        var logDirectory = Path.Combine(testRoot, "logs-sequence");
        var statePath = Path.Combine(testRoot, "state-sequence", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-sequence", "settings.json");

        Directory.CreateDirectory(sourceOne);
        Directory.CreateDirectory(sourceTwo);
        await File.WriteAllTextAsync(Path.Combine(sourceOne, "first.txt"), "one");
        await File.WriteAllTextAsync(Path.Combine(sourceTwo, "second.txt"), "two");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            BusinessSoftwareProcesses = ["calc"]
        });

        var repository = new BackupJobRepository(Path.Combine(testRoot, "jobs-sequence", "jobs.json"));
        var jobService = new BackupJobService(repository);
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Job 1",
            SourceDirectory = sourceOne,
            TargetDirectory = Path.Combine(targetRoot, "job1"),
            Type = BackupType.Complete
        });
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Job 2",
            SourceDirectory = sourceTwo,
            TargetDirectory = Path.Combine(targetRoot, "job2"),
            Type = BackupType.Complete
        });

        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([BusinessSoftwareDetectionResult.None, new BusinessSoftwareDetectionResult(true, "calc")]),
            new FakeEncryptionService(_ => 0));

        await manager.ExecuteAllJobsAsync();

        Assert.True(File.Exists(Path.Combine(targetRoot, "job1", "first.txt")));
        Assert.False(File.Exists(Path.Combine(targetRoot, "job2", "second.txt")));

        var states = await ReadStateEntriesAsync(statePath);
        Assert.Contains(states, state => state.Name == "Job 1" && state.State == "Blocked");
        Assert.DoesNotContain(states, state => state.Name == "Job 2");
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

    private BackupManager CreateConfiguredBackupManager(
        string logDirectory,
        string statePath,
        string sourceDirectory,
        string targetDirectory,
        BackupType backupType,
        string jobName,
        AppSettingsRepository settingsRepository,
        IBusinessSoftwareDetector businessSoftwareDetector,
        IFileEncryptionService fileEncryptionService)
    {
        var repository = new BackupJobRepository(Path.Combine(testRoot, $"{jobName}-config", "jobs.json"));
        var jobService = new BackupJobService(repository);
        var stateManager = new StateManager(statePath);

        jobService.AddJobAsync(new BackupJob
        {
            Name = jobName,
            SourceDirectory = sourceDirectory,
            TargetDirectory = targetDirectory,
            Type = backupType
        }).GetAwaiter().GetResult();

        return new BackupManager(
            jobService,
            stateManager,
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            businessSoftwareDetector,
            fileEncryptionService);
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

    private sealed class FakeEncryptionService(Func<string, long> encryptResultFactory) : IFileEncryptionService
    {
        public List<string> EncryptedFiles { get; } = [];

        public Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default)
        {
            EncryptedFiles.Add(filePath);
            return Task.FromResult(encryptResultFactory(filePath));
        }
    }

    private sealed class FakeBusinessSoftwareDetector(IEnumerable<BusinessSoftwareDetectionResult> results) : IBusinessSoftwareDetector
    {
        private readonly Queue<BusinessSoftwareDetectionResult> queuedResults = new(results);

        public BusinessSoftwareDetectionResult Detect(AppSettings settings)
        {
            if (queuedResults.Count == 0)
            {
                return BusinessSoftwareDetectionResult.None;
            }

            return queuedResults.Dequeue();
        }
    }
}
