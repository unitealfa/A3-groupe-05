using EasySave.Core.Configuration;
using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Strategies;

namespace EasySave.Core.Services;

public sealed class BackupManager
{
    private readonly BackupJobService jobService;
    private readonly StateManager stateManager;
    private readonly Func<AppSettings, ILoggerService> loggerFactory;
    private readonly AppSettingsRepository? settingsRepository;
    private readonly IBusinessSoftwareDetector businessSoftwareDetector;
    private readonly IFileEncryptionService fileEncryptionService;

    public BackupManager(BackupJobService jobService, StateManager stateManager, string logDirectory)
        : this(
            jobService,
            stateManager,
            _ => new JsonLoggerService(logDirectory),
            settingsRepository: null,
            new ProcessBusinessSoftwareDetector(),
            new CryptoSoftEncryptionService())
    {
    }

    public BackupManager(BackupJobService jobService, StateManager stateManager, ILoggerService logger)
        : this(
            jobService,
            stateManager,
            _ => logger,
            settingsRepository: null,
            new ProcessBusinessSoftwareDetector(),
            new CryptoSoftEncryptionService())
    {
    }

    public BackupManager(
        BackupJobService jobService,
        StateManager stateManager,
        Func<AppSettings, ILoggerService> loggerFactory,
        AppSettingsRepository? settingsRepository,
        IBusinessSoftwareDetector businessSoftwareDetector,
        IFileEncryptionService fileEncryptionService)
    {
        this.jobService = jobService;
        this.stateManager = stateManager;
        this.loggerFactory = loggerFactory;
        this.settingsRepository = settingsRepository;
        this.businessSoftwareDetector = businessSoftwareDetector;
        this.fileEncryptionService = fileEncryptionService;
    }

    public async Task ExecuteJobAsync(int jobIndex, CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        if (jobIndex < 1 || jobIndex > jobs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(jobIndex), "Backup job index is out of range.");
        }

        await ExecuteJobInternalAsync(jobs[jobIndex - 1], cancellationToken);
    }

    public async Task ExecuteAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        foreach (var job in jobs)
        {
            var blocked = await ExecuteJobInternalAsync(job, cancellationToken);
            if (blocked)
            {
                break;
            }
        }
    }

    public async Task ExecuteJobsAsync(IEnumerable<int> jobIndexes, CancellationToken cancellationToken = default)
    {
        foreach (var jobIndex in jobIndexes)
        {
            var jobs = await jobService.GetJobsAsync(cancellationToken);
            if (jobIndex < 1 || jobIndex > jobs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(jobIndex), "Backup job index is out of range.");
            }

            var blocked = await ExecuteJobInternalAsync(jobs[jobIndex - 1], cancellationToken);
            if (blocked)
            {
                break;
            }
        }
    }

    private async Task<bool> ExecuteJobInternalAsync(BackupJob job, CancellationToken cancellationToken)
    {
        BackupJobService.ValidateJob(job);

        var strategy = BackupStrategyFactory.Create(job.Type);
        var settings = settingsRepository is null
            ? new AppSettings()
            : await settingsRepository.LoadAsync(cancellationToken);
        var context = new BackupExecutionContext(
            stateManager,
            loggerFactory(settings),
            settings,
            businessSoftwareDetector,
            fileEncryptionService);

        await strategy.ExecuteAsync(job, context, cancellationToken);
        return context.IsBlockedByBusinessSoftware;
    }
}
