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
    public async Task BackupEncryptsAllFilesByDefaultWhenNoExtensionFilterIsConfigured()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-encrypt-default");
        var targetDirectory = Path.Combine(testRoot, "target-encrypt-default");
        var logDirectory = Path.Combine(testRoot, "logs-encrypt-default");
        var statePath = Path.Combine(testRoot, "state-encrypt-default", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-encrypt-default", "settings.json");
        Directory.CreateDirectory(sourceDirectory);

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "secret.txt"), "txt-content");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "plain.log"), "log-content");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings());

        var encryptionService = new FakeEncryptionService(_ => 21);

        var manager = CreateConfiguredBackupManager(
            logDirectory,
            statePath,
            sourceDirectory,
            targetDirectory,
            BackupType.Complete,
            "Encrypt Default Job",
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            encryptionService);

        await manager.ExecuteJobAsync(1);

        Assert.Equal(2, encryptionService.EncryptedFiles.Count);
        Assert.Contains(encryptionService.EncryptedFiles, path => path.EndsWith("secret.txt", StringComparison.Ordinal));
        Assert.Contains(encryptionService.EncryptedFiles, path => path.EndsWith("plain.log", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BackupPausesWhileBusinessSoftwareIsDetectedAndResumesAutomatically()
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

        var detector = new ToggleBusinessSoftwareDetector();
        var transferService = new DelayedTransferService(TimeSpan.FromMilliseconds(150));

        var manager = CreateConfiguredBackupManager(
            logDirectory,
            statePath,
            sourceDirectory,
            targetDirectory,
            BackupType.Complete,
            "Paused Job",
            settingsRepository,
            detector,
            new FakeEncryptionService(_ => 0),
            transferService);

        var executionTask = await manager.StartJobAsync(1);
        await transferService.FirstTransferStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        detector.SetDetected("calc");

        await WaitUntilAsync(async () =>
        {
            var states = await ReadStateEntriesAsync(statePath);
            return states.Any(state => state.Name == "Paused Job" && state.State == "Paused");
        });

        Assert.True(File.Exists(Path.Combine(targetDirectory, "a.txt")));
        Assert.False(File.Exists(Path.Combine(targetDirectory, "b.txt")));

        detector.Clear();
        await executionTask;

        Assert.True(File.Exists(Path.Combine(targetDirectory, "b.txt")));

        var states = await ReadStateEntriesAsync(statePath);
        var state = Assert.Single(states);
        Assert.Equal("Finished", state.State);
    }

    [Fact]
    public async Task CompleteBackupCopiesSingleSourceFileToTargetRoot()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-single-file");
        var targetDirectory = Path.Combine(testRoot, "target-single-file");
        var logDirectory = Path.Combine(testRoot, "logs-single-file");
        var statePath = Path.Combine(testRoot, "state-single-file", "state.json");
        Directory.CreateDirectory(sourceDirectory);

        var sourceFilePath = Path.Combine(sourceDirectory, "photo.jpg");
        await File.WriteAllTextAsync(sourceFilePath, "photo-content");

        var manager = CreateBackupManager(logDirectory, statePath, sourceFilePath, targetDirectory, BackupType.Complete, "Single File Job");

        await manager.ExecuteJobAsync(1);

        Assert.True(File.Exists(Path.Combine(targetDirectory, "photo.jpg")));
    }

    [Fact]
    public async Task CompleteBackupCopiesMultipleSourceFilesWithoutCollisions()
    {
        var sourceOne = Path.Combine(testRoot, "source-multi-1");
        var sourceTwo = Path.Combine(testRoot, "source-multi-2");
        var targetDirectory = Path.Combine(testRoot, "target-multi-file");
        var logDirectory = Path.Combine(testRoot, "logs-multi-file");
        var statePath = Path.Combine(testRoot, "state-multi-file", "state.json");
        Directory.CreateDirectory(sourceOne);
        Directory.CreateDirectory(sourceTwo);

        var firstFile = Path.Combine(sourceOne, "shared.txt");
        var secondFile = Path.Combine(sourceTwo, "shared.txt");
        await File.WriteAllTextAsync(firstFile, "first");
        await File.WriteAllTextAsync(secondFile, "second");

        var manager = CreateBackupManager(
            logDirectory,
            statePath,
            $"{firstFile};{secondFile}",
            targetDirectory,
            BackupType.Complete,
            "Multiple File Job");

        await manager.ExecuteJobAsync(1);

        Assert.True(File.Exists(Path.Combine(targetDirectory, "source-multi-1", "shared.txt")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "source-multi-2", "shared.txt")));
    }

    [Fact]
    public async Task ExecuteAllJobsRunsJobsInParallel()
    {
        var sourceOne = Path.Combine(testRoot, "source-parallel-1");
        var sourceTwo = Path.Combine(testRoot, "source-parallel-2");
        var targetRoot = Path.Combine(testRoot, "target-parallel");
        var logDirectory = Path.Combine(testRoot, "logs-parallel");
        var statePath = Path.Combine(testRoot, "state-parallel", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-parallel", "settings.json");

        Directory.CreateDirectory(sourceOne);
        Directory.CreateDirectory(sourceTwo);
        await File.WriteAllTextAsync(Path.Combine(sourceOne, "first.txt"), "one");
        await File.WriteAllTextAsync(Path.Combine(sourceTwo, "second.txt"), "two");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            EncryptedExtensions = ["*"]
        });

        var repository = new BackupJobRepository(Path.Combine(testRoot, "jobs-parallel", "jobs.json"));
        var jobService = new BackupJobService(repository);
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Parallel Job 1",
            SourceDirectory = sourceOne,
            TargetDirectory = Path.Combine(targetRoot, "job1"),
            Type = BackupType.Complete
        });
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Parallel Job 2",
            SourceDirectory = sourceTwo,
            TargetDirectory = Path.Combine(targetRoot, "job2"),
            Type = BackupType.Complete
        });

        var encryptionService = new ConcurrentProbeEncryptionService(TimeSpan.FromMilliseconds(250));

        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            encryptionService,
            new FileSystemFileTransferService());

        await manager.ExecuteAllJobsAsync();

        Assert.True(File.Exists(Path.Combine(targetRoot, "job1", "first.txt")));
        Assert.True(File.Exists(Path.Combine(targetRoot, "job2", "second.txt")));
        Assert.True(encryptionService.MaxConcurrentCalls >= 2, $"Expected overlapping execution but max concurrency was {encryptionService.MaxConcurrentCalls}.");
    }

    [Fact]
    public async Task ExecuteAllJobsBlocksNonPriorityFilesWhilePriorityExtensionsArePending()
    {
        var prioritySource = Path.Combine(testRoot, "source-priority");
        var regularSource = Path.Combine(testRoot, "source-regular");
        var targetRoot = Path.Combine(testRoot, "target-priority");
        var logDirectory = Path.Combine(testRoot, "logs-priority");
        var statePath = Path.Combine(testRoot, "state-priority", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-priority", "settings.json");

        Directory.CreateDirectory(prioritySource);
        Directory.CreateDirectory(regularSource);
        await File.WriteAllTextAsync(Path.Combine(prioritySource, "urgent.prio"), "priority");
        await File.WriteAllTextAsync(Path.Combine(regularSource, "later.txt"), "regular");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            EncryptedExtensions = ["*"],
            PriorityExtensions = [".prio"]
        });

        var repository = new BackupJobRepository(Path.Combine(testRoot, "jobs-priority", "jobs.json"));
        var jobService = new BackupJobService(repository);
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Priority Job",
            SourceDirectory = prioritySource,
            TargetDirectory = Path.Combine(targetRoot, "priority"),
            Type = BackupType.Complete
        });
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Regular Job",
            SourceDirectory = regularSource,
            TargetDirectory = Path.Combine(targetRoot, "regular"),
            Type = BackupType.Complete
        });

        var encryptionService = new ConcurrentProbeEncryptionService(TimeSpan.FromMilliseconds(200));
        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            encryptionService,
            new FileSystemFileTransferService());

        await manager.ExecuteAllJobsAsync();

        Assert.True(File.Exists(Path.Combine(targetRoot, "priority", "urgent.prio")));
        Assert.True(File.Exists(Path.Combine(targetRoot, "regular", "later.txt")));
        Assert.Equal(1, encryptionService.MaxConcurrentCalls);
    }

    [Fact]
    public async Task ExecuteAllJobsAllowsOnlyOneLargeFileTransferAtATime()
    {
        var sourceOne = Path.Combine(testRoot, "source-large-1");
        var sourceTwo = Path.Combine(testRoot, "source-large-2");
        var targetRoot = Path.Combine(testRoot, "target-large");
        var logDirectory = Path.Combine(testRoot, "logs-large");
        var statePath = Path.Combine(testRoot, "state-large", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-large", "settings.json");

        Directory.CreateDirectory(sourceOne);
        Directory.CreateDirectory(sourceTwo);
        await File.WriteAllBytesAsync(Path.Combine(sourceOne, "big-one.bin"), new byte[2_048]);
        await File.WriteAllBytesAsync(Path.Combine(sourceTwo, "big-two.bin"), new byte[2_048]);

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            LargeFileThresholdKo = 1
        });

        var repository = new BackupJobRepository(Path.Combine(testRoot, "jobs-large", "jobs.json"));
        var jobService = new BackupJobService(repository);
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Large Job 1",
            SourceDirectory = sourceOne,
            TargetDirectory = Path.Combine(targetRoot, "job1"),
            Type = BackupType.Complete
        });
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Large Job 2",
            SourceDirectory = sourceTwo,
            TargetDirectory = Path.Combine(targetRoot, "job2"),
            Type = BackupType.Complete
        });

        var transferService = new ConcurrentProbeTransferService(
            TimeSpan.FromMilliseconds(200),
            path => new FileInfo(path).Length > 1_024);

        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            new FakeEncryptionService(_ => 0),
            transferService);

        await manager.ExecuteAllJobsAsync();

        Assert.True(File.Exists(Path.Combine(targetRoot, "job1", "big-one.bin")));
        Assert.True(File.Exists(Path.Combine(targetRoot, "job2", "big-two.bin")));
        Assert.Equal(1, transferService.MaxConcurrentLargeTransfers);
    }

    [Fact]
    public async Task ExecuteAllJobsStillAllowsSmallTransfersDuringLargeTransfer()
    {
        var largeSource = Path.Combine(testRoot, "source-large-mixed");
        var smallSource = Path.Combine(testRoot, "source-small-mixed");
        var targetRoot = Path.Combine(testRoot, "target-mixed");
        var logDirectory = Path.Combine(testRoot, "logs-mixed");
        var statePath = Path.Combine(testRoot, "state-mixed", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-mixed", "settings.json");

        Directory.CreateDirectory(largeSource);
        Directory.CreateDirectory(smallSource);
        await File.WriteAllBytesAsync(Path.Combine(largeSource, "large.bin"), new byte[2_048]);
        await File.WriteAllBytesAsync(Path.Combine(smallSource, "small.txt"), new byte[512]);

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings
        {
            LargeFileThresholdKo = 1
        });

        var repository = new BackupJobRepository(Path.Combine(testRoot, "jobs-mixed", "jobs.json"));
        var jobService = new BackupJobService(repository);
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Large Mixed Job",
            SourceDirectory = largeSource,
            TargetDirectory = Path.Combine(targetRoot, "large"),
            Type = BackupType.Complete
        });
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Small Mixed Job",
            SourceDirectory = smallSource,
            TargetDirectory = Path.Combine(targetRoot, "small"),
            Type = BackupType.Complete
        });

        var transferService = new ConcurrentProbeTransferService(
            TimeSpan.FromMilliseconds(200),
            path => new FileInfo(path).Length > 1_024);

        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            new FakeEncryptionService(_ => 0),
            transferService);

        await manager.ExecuteAllJobsAsync();

        Assert.True(File.Exists(Path.Combine(targetRoot, "large", "large.bin")));
        Assert.True(File.Exists(Path.Combine(targetRoot, "small", "small.txt")));
        Assert.Equal(1, transferService.MaxConcurrentLargeTransfers);
        Assert.True(transferService.MaxConcurrentTransfers >= 2, $"Expected large and small transfers to overlap, but max concurrent transfers was {transferService.MaxConcurrentTransfers}.");
    }

    [Fact]
    public async Task ExecuteAllJobsPauseAllJobsWhileBusinessSoftwareIsRunningAndResumeAutomatically()
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
        await File.WriteAllTextAsync(Path.Combine(sourceOne, "third.txt"), "three");
        await File.WriteAllTextAsync(Path.Combine(sourceTwo, "second.txt"), "two");
        await File.WriteAllTextAsync(Path.Combine(sourceTwo, "fourth.txt"), "four");

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

        var detector = new ToggleBusinessSoftwareDetector();
        var transferService = new CountingDelayedTransferService(TimeSpan.FromMilliseconds(150));
        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            detector,
            new FakeEncryptionService(_ => 0),
            transferService);

        var executionTasks = await manager.StartAllJobsAsync();
        await transferService.WaitForTransfersStartedAsync(2, TimeSpan.FromSeconds(2));
        detector.SetDetected("calc");

        await WaitUntilAsync(async () =>
        {
            var states = await ReadStateEntriesAsync(statePath);
            return states.Count(state => state.State == "Paused") == 2;
        });

        Assert.Single(Directory.GetFiles(Path.Combine(targetRoot, "job1"), "*", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(Path.Combine(targetRoot, "job2"), "*", SearchOption.AllDirectories));

        detector.Clear();
        await Task.WhenAll(executionTasks);

        var states = await ReadStateEntriesAsync(statePath);
        Assert.Equal(2, states.Count(state => state.State == "Finished"));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(targetRoot, "job1"), "*", SearchOption.AllDirectories).Length);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(targetRoot, "job2"), "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task PauseResumeAndStopControlTheSelectedJobInRealTime()
    {
        var sourceDirectory = Path.Combine(testRoot, "source-runtime-control");
        var targetDirectory = Path.Combine(testRoot, "target-runtime-control");
        var logDirectory = Path.Combine(testRoot, "logs-runtime-control");
        var statePath = Path.Combine(testRoot, "state-runtime-control", "state.json");
        var settingsPath = Path.Combine(testRoot, "config-runtime-control", "settings.json");

        Directory.CreateDirectory(sourceDirectory);
        var firstFile = Path.Combine(sourceDirectory, "a-first.txt");
        var secondFile = Path.Combine(sourceDirectory, "b-second.txt");
        await File.WriteAllTextAsync(firstFile, "first");
        await File.WriteAllTextAsync(secondFile, "second");

        var settingsRepository = new AppSettingsRepository(settingsPath);
        await settingsRepository.SaveAsync(new AppSettings());

        var repository = new BackupJobRepository(Path.Combine(testRoot, "jobs-runtime-control", "jobs.json"));
        var jobService = new BackupJobService(repository);
        await jobService.AddJobAsync(new BackupJob
        {
            Name = "Runtime Control Job",
            SourceDirectory = $"{firstFile};{secondFile}",
            TargetDirectory = targetDirectory,
            Type = BackupType.Complete
        });

        var transferService = new DelayedTransferService(TimeSpan.FromMilliseconds(150));
        var manager = new BackupManager(
            jobService,
            new StateManager(statePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            new FakeEncryptionService(_ => 0),
            transferService);

        var executionTask = await manager.StartJobAsync(1);
        await transferService.FirstTransferStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var firstDestinationPath = Path.Combine(targetDirectory, Path.GetFileName(sourceDirectory), "a-first.txt");
        var secondDestinationPath = Path.Combine(targetDirectory, Path.GetFileName(sourceDirectory), "b-second.txt");

        Assert.True(await manager.PauseJobAsync("Runtime Control Job"));
        await WaitUntilAsync(async () =>
        {
            var states = await ReadStateEntriesAsync(statePath);
            return states.Any(state => state.Name == "Runtime Control Job" && state.State == "Paused");
        });

        Assert.True(File.Exists(firstDestinationPath));
        Assert.False(File.Exists(secondDestinationPath));

        Assert.True(await manager.ResumeJobAsync("Runtime Control Job"));
        await executionTask;

        Assert.True(File.Exists(secondDestinationPath));
        var resumedStates = await ReadStateEntriesAsync(statePath);
        Assert.Contains(resumedStates, state => state.Name == "Runtime Control Job" && state.State == "Finished");

        var stopSourceDirectory = Path.Combine(testRoot, "source-runtime-stop");
        var stopTargetDirectory = Path.Combine(testRoot, "target-runtime-stop");
        var stopStatePath = Path.Combine(testRoot, "state-runtime-stop", "state.json");
        var stopJobsPath = Path.Combine(testRoot, "jobs-runtime-stop", "jobs.json");
        Directory.CreateDirectory(stopSourceDirectory);
        var stopFile = Path.Combine(stopSourceDirectory, "stop-me.txt");
        await File.WriteAllTextAsync(stopFile, "stop-content");

        var stopJobService = new BackupJobService(new BackupJobRepository(stopJobsPath));
        await stopJobService.AddJobAsync(new BackupJob
        {
            Name = "Stop Job",
            SourceDirectory = stopFile,
            TargetDirectory = stopTargetDirectory,
            Type = BackupType.Complete
        });

        var stopTransferService = new DelayedTransferService(TimeSpan.FromMilliseconds(400));
        var stopManager = new BackupManager(
            stopJobService,
            new StateManager(stopStatePath),
            _ => new JsonLoggerService(logDirectory),
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            new FakeEncryptionService(_ => 0),
            stopTransferService);

        var stopTask = await stopManager.StartJobAsync(1);
        await stopTransferService.FirstTransferStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(await stopManager.StopJobAsync("Stop Job"));
        await stopTask;

        var stoppedStates = await ReadStateEntriesAsync(stopStatePath);
        Assert.Contains(stoppedStates, state => state.Name == "Stop Job" && state.State == "Stopped");
        Assert.False(File.Exists(Path.Combine(stopTargetDirectory, "stop-me.txt")));
    }

    private BackupManager CreateBackupManager(
        string logDirectory,
        string statePath,
        string sourceDirectory,
        string targetDirectory,
        BackupType backupType,
        string jobName)
    {
        var settingsRepository = new AppSettingsRepository(Path.Combine(testRoot, $"{jobName}-config", "settings.json"));
        settingsRepository.SaveAsync(new AppSettings
        {
            EncryptedExtensions = [".crypt-only"]
        }).GetAwaiter().GetResult();

        return CreateConfiguredBackupManager(
            logDirectory,
            statePath,
            sourceDirectory,
            targetDirectory,
            backupType,
            jobName,
            settingsRepository,
            new FakeBusinessSoftwareDetector([]),
            new FakeEncryptionService(_ => 0));
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
        IFileEncryptionService fileEncryptionService,
        IFileTransferService? fileTransferService = null)
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
            fileEncryptionService,
            fileTransferService ?? new FileSystemFileTransferService());
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

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 3000, int pollDelayMs = 50)
    {
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(pollDelayMs);
        }

        throw new TimeoutException("Condition was not met before timeout.");
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

    private sealed class DelayedEncryptionService(TimeSpan delay) : IFileEncryptionService
    {
        public async Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            return 1;
        }
    }

    private sealed class ConcurrentProbeEncryptionService(TimeSpan delay) : IFileEncryptionService
    {
        private int currentConcurrentCalls;
        private int maxConcurrentCalls;

        public int MaxConcurrentCalls => maxConcurrentCalls;

        public async Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default)
        {
            var activeCalls = Interlocked.Increment(ref currentConcurrentCalls);
            UpdateMax(activeCalls);

            try
            {
                await Task.Delay(delay, cancellationToken);
                return 1;
            }
            finally
            {
                Interlocked.Decrement(ref currentConcurrentCalls);
            }
        }

        private void UpdateMax(int activeCalls)
        {
            int snapshot;
            do
            {
                snapshot = maxConcurrentCalls;
                if (activeCalls <= snapshot)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref maxConcurrentCalls, activeCalls, snapshot) != snapshot);
        }
    }

    private sealed class DelayedByFileEncryptionService(Func<string, TimeSpan> delayFactory) : IFileEncryptionService
    {
        public async Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayFactory(filePath), cancellationToken);
            return 1;
        }
    }

    private sealed class ConcurrentProbeTransferService(
        TimeSpan delay,
        Func<string, bool> isLargeFilePredicate) : IFileTransferService
    {
        private int currentConcurrentTransfers;
        private int currentConcurrentLargeTransfers;
        private int maxConcurrentTransfers;
        private int maxConcurrentLargeTransfers;

        public int MaxConcurrentTransfers => maxConcurrentTransfers;

        public int MaxConcurrentLargeTransfers => maxConcurrentLargeTransfers;

        public async Task CopyAsync(string sourceFilePath, string destinationFilePath, bool overwrite, CancellationToken cancellationToken = default)
        {
            var isLargeFile = isLargeFilePredicate(sourceFilePath);
            var activeTransfers = Interlocked.Increment(ref currentConcurrentTransfers);
            UpdateMax(ref maxConcurrentTransfers, activeTransfers);

            if (isLargeFile)
            {
                var activeLargeTransfers = Interlocked.Increment(ref currentConcurrentLargeTransfers);
                UpdateMax(ref maxConcurrentLargeTransfers, activeLargeTransfers);
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
                await File.WriteAllTextAsync(destinationFilePath, await File.ReadAllTextAsync(sourceFilePath, cancellationToken), cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref currentConcurrentTransfers);
                if (isLargeFile)
                {
                    Interlocked.Decrement(ref currentConcurrentLargeTransfers);
                }
            }
        }

        private static void UpdateMax(ref int target, int candidate)
        {
            int snapshot;
            do
            {
                snapshot = target;
                if (candidate <= snapshot)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, candidate, snapshot) != snapshot);
        }
    }

    private sealed class DelayedTransferService(TimeSpan delay) : IFileTransferService
    {
        private int transferCount;

        public TaskCompletionSource FirstTransferStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task CopyAsync(string sourceFilePath, string destinationFilePath, bool overwrite, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref transferCount) == 1)
            {
                FirstTransferStarted.TrySetResult();
            }

            await Task.Delay(delay, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);

            await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var destinationStream = new FileStream(destinationFilePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }
    }

    private sealed class CountingDelayedTransferService(TimeSpan delay) : IFileTransferService
    {
        private readonly TaskCompletionSource startedTransfersCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int transferCount;

        public async Task CopyAsync(string sourceFilePath, string destinationFilePath, bool overwrite, CancellationToken cancellationToken = default)
        {
            var startedTransfers = Interlocked.Increment(ref transferCount);
            if (startedTransfers >= 2)
            {
                startedTransfersCompletionSource.TrySetResult();
            }

            await Task.Delay(delay, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);

            await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var destinationStream = new FileStream(destinationFilePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }

        public Task WaitForTransfersStartedAsync(int minimumTransfers, TimeSpan timeout)
        {
            if (minimumTransfers <= 1 && Volatile.Read(ref transferCount) >= minimumTransfers)
            {
                return Task.CompletedTask;
            }

            return startedTransfersCompletionSource.Task.WaitAsync(timeout);
        }
    }

    private sealed class FakeBusinessSoftwareDetector(IEnumerable<BusinessSoftwareDetectionResult> results) : IBusinessSoftwareDetector
    {
        private readonly Queue<BusinessSoftwareDetectionResult> queuedResults = new(results);
        private readonly object syncLock = new();

        public BusinessSoftwareDetectionResult Detect(AppSettings settings)
        {
            lock (syncLock)
            {
                if (queuedResults.Count == 0)
                {
                    return BusinessSoftwareDetectionResult.None;
                }

                return queuedResults.Dequeue();
            }
        }
    }

    private sealed class ToggleBusinessSoftwareDetector : IBusinessSoftwareDetector
    {
        private readonly object syncLock = new();
        private string detectedProcessName = string.Empty;

        public BusinessSoftwareDetectionResult Detect(AppSettings settings)
        {
            lock (syncLock)
            {
                return string.IsNullOrWhiteSpace(detectedProcessName)
                    ? BusinessSoftwareDetectionResult.None
                    : new BusinessSoftwareDetectionResult(true, detectedProcessName);
            }
        }

        public void SetDetected(string processName)
        {
            lock (syncLock)
            {
                detectedProcessName = processName;
            }
        }

        public void Clear()
        {
            lock (syncLock)
            {
                detectedProcessName = string.Empty;
            }
        }
    }
}
