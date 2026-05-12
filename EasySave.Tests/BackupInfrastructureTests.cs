using System.Text.Json;
using System.Xml.Linq;
using EasyLog;
using EasySave.Core.Configuration;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Tests;

public sealed class BackupInfrastructureTests : IDisposable
{
    private readonly string testRoot;

    public BackupInfrastructureTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), $"easysave-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void ValidateJobRejectsSourceDirectoryThatDoesNotExist()
    {
        var job = new BackupJob
        {
            Name = "Job 1",
            SourceDirectory = Path.Combine(testRoot, "missing-source"),
            TargetDirectory = Path.Combine(testRoot, "target"),
            Type = BackupType.Complete
        };

        Assert.Throws<DirectoryNotFoundException>(() => BackupJobService.ValidateJob(job));
    }

    [Fact]
    public void ValidateJobAcceptsExistingSingleSourceFile()
    {
        var sourceDirectory = Path.Combine(testRoot, "single-file-source");
        var sourceFile = Path.Combine(sourceDirectory, "image.png");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(sourceFile, "image");

        var job = new BackupJob
        {
            Name = "Job File",
            SourceDirectory = sourceFile,
            TargetDirectory = Path.Combine(testRoot, "target-file"),
            Type = BackupType.Complete
        };

        BackupJobService.ValidateJob(job);
    }

    [Fact]
    public void ValidateJobRejectsTargetDirectoryThatMatchesSourceDirectory()
    {
        var sourceDirectory = Path.Combine(testRoot, "same-folder-source");
        Directory.CreateDirectory(sourceDirectory);

        var job = new BackupJob
        {
            Name = "Same Folder",
            SourceDirectory = sourceDirectory,
            TargetDirectory = sourceDirectory,
            Type = BackupType.Complete
        };

        var exception = Assert.Throws<InvalidOperationException>(() => BackupJobService.ValidateJob(job));
        Assert.Contains("target directory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateJobRejectsTargetDirectoryInsideSourceDirectory()
    {
        var sourceDirectory = Path.Combine(testRoot, "nested-source");
        Directory.CreateDirectory(sourceDirectory);

        var job = new BackupJob
        {
            Name = "Nested Folder",
            SourceDirectory = sourceDirectory,
            TargetDirectory = Path.Combine(sourceDirectory, "backup"),
            Type = BackupType.Complete
        };

        var exception = Assert.Throws<InvalidOperationException>(() => BackupJobService.ValidateJob(job));
        Assert.Contains("target directory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddJobAsyncAllowsMoreThanFiveJobs()
    {
        var repository = new BackupJobRepository(Path.Combine(testRoot, "config", "jobs.json"));
        var service = new BackupJobService(repository);
        var sourceDirectory = Path.Combine(testRoot, "source");
        var targetDirectory = Path.Combine(testRoot, "target");
        Directory.CreateDirectory(sourceDirectory);

        for (var index = 0; index < 8; index++)
        {
            await service.AddJobAsync(new BackupJob
            {
                Name = $"Job {index + 1}",
                SourceDirectory = sourceDirectory,
                TargetDirectory = Path.Combine(targetDirectory, index.ToString()),
                Type = BackupType.Complete
            });
        }

        var jobs = await service.GetJobsAsync();
        Assert.Equal(8, jobs.Count);
    }

    [Fact]
    public async Task AddJobAsyncRejectsDuplicateJobNameIgnoringCase()
    {
        var repository = new BackupJobRepository(Path.Combine(testRoot, "duplicate-config", "jobs.json"));
        var service = new BackupJobService(repository);
        var sourceDirectory = Path.Combine(testRoot, "duplicate-source");
        var targetDirectory = Path.Combine(testRoot, "duplicate-target");
        Directory.CreateDirectory(sourceDirectory);

        await service.AddJobAsync(new BackupJob
        {
            Name = "Work Backup",
            SourceDirectory = sourceDirectory,
            TargetDirectory = Path.Combine(targetDirectory, "one"),
            Type = BackupType.Complete
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddJobAsync(new BackupJob
        {
            Name = "work backup",
            SourceDirectory = sourceDirectory,
            TargetDirectory = Path.Combine(targetDirectory, "two"),
            Type = BackupType.Differential
        }));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateJobAsyncRejectsRenameToExistingJobNameIgnoringCase()
    {
        var repository = new BackupJobRepository(Path.Combine(testRoot, "rename-config", "jobs.json"));
        var service = new BackupJobService(repository);
        var sourceDirectory = Path.Combine(testRoot, "rename-source");
        var targetDirectory = Path.Combine(testRoot, "rename-target");
        Directory.CreateDirectory(sourceDirectory);

        await service.AddJobAsync(new BackupJob
        {
            Name = "Job 1",
            SourceDirectory = sourceDirectory,
            TargetDirectory = Path.Combine(targetDirectory, "one"),
            Type = BackupType.Complete
        });

        await service.AddJobAsync(new BackupJob
        {
            Name = "Job 2",
            SourceDirectory = sourceDirectory,
            TargetDirectory = Path.Combine(targetDirectory, "two"),
            Type = BackupType.Complete
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateJobAsync("Job 2", new BackupJob
        {
            Name = "job 1",
            SourceDirectory = sourceDirectory,
            TargetDirectory = Path.Combine(targetDirectory, "two-updated"),
            Type = BackupType.Differential
        }));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteJobAsyncRemovesExistingJob()
    {
        var repository = new BackupJobRepository(Path.Combine(testRoot, "delete-config", "jobs.json"));
        var service = new BackupJobService(repository);
        var sourceDirectory = Path.Combine(testRoot, "delete-source");
        var targetDirectory = Path.Combine(testRoot, "delete-target");
        Directory.CreateDirectory(sourceDirectory);

        await service.AddJobAsync(new BackupJob
        {
            Name = "Delete Me",
            SourceDirectory = sourceDirectory,
            TargetDirectory = targetDirectory,
            Type = BackupType.Complete
        });

        await service.DeleteJobAsync("delete me");

        var jobs = await service.GetJobsAsync();
        Assert.Empty(jobs);
    }

    [Fact]
    public async Task StateManagerRemoveStateAsyncDeletesMatchingState()
    {
        var statePath = Path.Combine(testRoot, "remove-state", "state.json");
        var stateManager = new StateManager(statePath);

        await stateManager.UpdateAsync(new BackupState
        {
            Name = "Job To Remove",
            State = "Finished"
        });

        await stateManager.RemoveStateAsync("job to remove");

        var states = await stateManager.GetStatesAsync();
        Assert.Empty(states);
    }

    [Fact]
    public async Task StateManagerCreatesAndUpdatesStateJson()
    {
        var statePath = Path.Combine(testRoot, "state", "state.json");
        var stateManager = new StateManager(statePath);

        await stateManager.UpdateAsync(new BackupState
        {
            Name = "Job 1",
            State = "Active",
            TotalFilesToCopy = 2,
            TotalFilesSize = 42,
            Progression = 50,
            RemainingFiles = 1,
            RemainingSize = 12,
            CurrentSourceFilePath = "src.txt",
            CurrentDestinationFilePath = "dst.txt"
        });

        Assert.True(File.Exists(statePath));
        var stateContent = await File.ReadAllTextAsync(statePath);
        Assert.Contains("\"Name\": \"Job 1\"", stateContent);
        Assert.Contains("\"State\": \"Active\"", stateContent);
        Assert.Contains("\"CurrentSourceFilePath\": \"src.txt\"", stateContent);
    }

    [Fact]
    public async Task JsonLoggerServiceCreatesIndentedDailyLogWithMainFields()
    {
        var logDirectory = Path.Combine(testRoot, "logs");
        var logger = new JsonLoggerService(logDirectory);

        await logger.LogAsync(new LogEntry
        {
            Timestamp = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Local),
            BackupName = "Job 1",
            SourceFilePath = @"C:\Source\file.txt",
            DestinationFilePath = @"D:\Target\file.txt",
            FileSize = 123,
            TransferTimeMs = 45,
            EncryptionTimeMs = 0,
            Status = "Success"
        });

        var logPath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");
        Assert.True(File.Exists(logPath));

        var content = await File.ReadAllTextAsync(logPath);
        Assert.Contains(Environment.NewLine, content);
        Assert.Contains("\"BackupName\": \"Job 1\"", content);
        Assert.Contains("\"SourceFilePath\": \"C:\\\\Source\\\\file.txt\"", content);
        Assert.Contains("\"DestinationFilePath\": \"D:\\\\Target\\\\file.txt\"", content);
        Assert.Contains("\"FileSize\": 123", content);
        Assert.Contains("\"TransferTimeMs\": 45", content);
        Assert.Contains("\"EncryptionTimeMs\": 0", content);

        var entries = JsonSerializer.Deserialize<List<LogEntry>>(content);
        Assert.NotNull(entries);
        Assert.Single(entries);
    }

    [Fact]
    public async Task XmlLoggerServiceCreatesDailyXmlLogWithMainFields()
    {
        var logDirectory = Path.Combine(testRoot, "xml-logs");
        var logger = new XmlLoggerService(logDirectory);

        await logger.LogAsync(new LogEntry
        {
            Timestamp = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Local),
            BackupName = "Job XML",
            SourceFilePath = @"C:\Source\xml.txt",
            DestinationFilePath = @"D:\Target\xml.txt",
            FileSize = 456,
            TransferTimeMs = 78,
            EncryptionTimeMs = 12,
            Status = "Success"
        });

        var logPath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");
        Assert.True(File.Exists(logPath));

        var document = XDocument.Load(logPath);
        var logEntry = document.Root?.Element("LogEntry");

        Assert.NotNull(document.Root);
        Assert.Equal("LogEntries", document.Root!.Name.LocalName);
        Assert.NotNull(logEntry);
        Assert.Equal("Job XML", logEntry!.Element("BackupName")?.Value);
        Assert.Equal(@"C:\Source\xml.txt", logEntry.Element("SourceFilePath")?.Value);
        Assert.Equal(@"D:\Target\xml.txt", logEntry.Element("DestinationFilePath")?.Value);
        Assert.Equal("456", logEntry.Element("FileSize")?.Value);
        Assert.Equal("78", logEntry.Element("TransferTimeMs")?.Value);
        Assert.Equal("12", logEntry.Element("EncryptionTimeMs")?.Value);
        Assert.Equal("Success", logEntry.Element("Status")?.Value);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
