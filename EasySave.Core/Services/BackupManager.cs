using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Strategies;

namespace EasySave.Core.Services;

public sealed class BackupManager
{
    private readonly BackupJobService jobService;
    private readonly StateManager stateManager;
    private readonly ILoggerService logger;

    public BackupManager(BackupJobService jobService, StateManager stateManager, string logDirectory)
        : this(jobService, stateManager, new JsonLoggerService(logDirectory))
    {
    }

    internal BackupManager(BackupJobService jobService, StateManager stateManager, ILoggerService logger)
    {
        this.jobService = jobService;
        this.stateManager = stateManager;
        this.logger = logger;
    }

    public async Task ExecuteJobAsync(int jobIndex, CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        if (jobIndex < 1 || jobIndex > jobs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(jobIndex), "Backup job index is out of range.");
        }

        await ExecuteJobAsync(jobs[jobIndex - 1], cancellationToken);
    }

    public async Task ExecuteAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        foreach (var job in jobs)
        {
            await ExecuteJobAsync(job, cancellationToken);
        }
    }

    public async Task ExecuteJobsAsync(IEnumerable<int> jobIndexes, CancellationToken cancellationToken = default)
    {
        foreach (var jobIndex in jobIndexes)
        {
            await ExecuteJobAsync(jobIndex, cancellationToken);
        }
    }

    private async Task ExecuteJobAsync(BackupJob job, CancellationToken cancellationToken)
    {
        BackupJobService.ValidateJob(job);

        var strategy = CreateStrategy(job.Type);
        var context = new BackupExecutionContext(stateManager, logger);

        await strategy.ExecuteAsync(job, context, cancellationToken);
    }

    private static IBackupStrategy CreateStrategy(BackupType backupType)
    {
        return backupType switch
        {
            BackupType.Complete => new CompleteBackupStrategy(),
            BackupType.Differential => new DifferentialBackupStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(backupType), "Unsupported backup type.")
        };
    }
}
