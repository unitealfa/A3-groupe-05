using EasySave.Core.Configuration;
using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Strategies;

namespace EasySave.Core.Services;

public sealed class BackupManager
{
    private readonly object executionRegistryLock = new();
    private readonly BackupJobService jobService;
    private readonly StateManager stateManager;
    private readonly Func<AppSettings, ILoggerService> loggerFactory;
    private readonly AppSettingsRepository? settingsRepository;
    private readonly IBusinessSoftwareDetector businessSoftwareDetector;
    private readonly IFileEncryptionService fileEncryptionService;
    private readonly IFileTransferService fileTransferService;
    private readonly PriorityFileCoordinator globalPriorityFileCoordinator = new();
    private readonly LargeFileTransferCoordinator globalLargeFileTransferCoordinator = new();
    private readonly Dictionary<string, BackupExecutionSession> executionSessions = new(StringComparer.OrdinalIgnoreCase);

    public BackupManager(BackupJobService jobService, StateManager stateManager, string logDirectory)
        : this(
            jobService,
            stateManager,
            _ => new JsonLoggerService(logDirectory),
            settingsRepository: null,
            new ProcessBusinessSoftwareDetector(),
            new CryptoSoftEncryptionService(),
            new FileSystemFileTransferService())
    {
    }

    public BackupManager(BackupJobService jobService, StateManager stateManager, ILoggerService logger)
        : this(
            jobService,
            stateManager,
            _ => logger,
            settingsRepository: null,
            new ProcessBusinessSoftwareDetector(),
            new CryptoSoftEncryptionService(),
            new FileSystemFileTransferService())
    {
    }

    public BackupManager(
        BackupJobService jobService,
        StateManager stateManager,
        Func<AppSettings, ILoggerService> loggerFactory,
        AppSettingsRepository? settingsRepository,
        IBusinessSoftwareDetector businessSoftwareDetector,
        IFileEncryptionService fileEncryptionService,
        IFileTransferService fileTransferService)
    {
        this.jobService = jobService;
        this.stateManager = stateManager;
        this.loggerFactory = loggerFactory;
        this.settingsRepository = settingsRepository;
        this.businessSoftwareDetector = businessSoftwareDetector;
        this.fileEncryptionService = fileEncryptionService;
        this.fileTransferService = fileTransferService;
    }

    public async Task ExecuteJobAsync(int jobIndex, CancellationToken cancellationToken = default)
    {
        var executionTask = await StartJobAsync(jobIndex, cancellationToken);
        await executionTask;
    }

    public async Task ExecuteAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var executionTasks = await StartAllJobsAsync(cancellationToken);
        await Task.WhenAll(executionTasks);
    }

    public async Task ExecuteJobsAsync(IEnumerable<int> jobIndexes, CancellationToken cancellationToken = default)
    {
        var executionTasks = await StartJobsAsync(jobIndexes, cancellationToken);
        await Task.WhenAll(executionTasks);
    }

    public async Task<Task> StartJobAsync(int jobIndex, CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        if (jobIndex < 1 || jobIndex > jobs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(jobIndex), "Backup job index is out of range.");
        }

        return await StartJobInternalAsync(jobs[jobIndex - 1], cancellationToken);
    }

    public async Task<IReadOnlyList<Task>> StartAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        return await StartJobsInternalAsync(jobs, cancellationToken);
    }

    public async Task<IReadOnlyList<Task>> StartJobsAsync(IEnumerable<int> jobIndexes, CancellationToken cancellationToken = default)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        var selectedJobs = new List<BackupJob>();

        foreach (var jobIndex in jobIndexes)
        {
            if (jobIndex < 1 || jobIndex > jobs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(jobIndex), "Backup job index is out of range.");
            }

            selectedJobs.Add(jobs[jobIndex - 1]);
        }

        return await StartJobsInternalAsync(selectedJobs, cancellationToken);
    }

    public async Task<bool> PauseJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var session = GetSession(jobName);
        if (session is null)
        {
            return false;
        }

        session.PauseController.Pause();
        return true;
    }

    public async Task<bool> ResumeJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var session = GetSession(jobName);
        if (session is null)
        {
            return false;
        }

        session.PauseController.Resume();
        await stateManager.SetStateValueAsync(jobName, "Active", cancellationToken);
        return true;
    }

    public async Task<bool> StopJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var session = GetSession(jobName);
        if (session is null)
        {
            return false;
        }

        session.PauseController.Resume();
        await stateManager.SetStateValueAsync(jobName, "Stopped", cancellationToken);
        session.CancellationTokenSource.Cancel();
        return true;
    }

    public async Task PauseAllJobsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var session in GetSessionsSnapshot())
        {
            session.PauseController.Pause();
        }
    }

    public async Task ResumeAllJobsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var session in GetSessionsSnapshot())
        {
            session.PauseController.Resume();
            await stateManager.SetStateValueAsync(session.JobName, "Active", cancellationToken);
        }
    }

    public async Task StopAllJobsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var session in GetSessionsSnapshot())
        {
            session.PauseController.Resume();
            await stateManager.SetStateValueAsync(session.JobName, "Stopped", cancellationToken);
            session.CancellationTokenSource.Cancel();
        }
    }

    public bool IsJobRunning(string jobName)
    {
        return GetSession(jobName) is not null;
    }

    private async Task<IReadOnlyList<Task>> StartJobsInternalAsync(IEnumerable<BackupJob> jobs, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var job in jobs)
        {
            tasks.Add(await StartJobInternalAsync(job, cancellationToken));
        }

        return tasks;
    }

    private async Task<Task> StartJobInternalAsync(BackupJob job, CancellationToken cancellationToken)
    {
        BackupJobService.ValidateJob(job);

        var settings = await LoadSettingsAsync(cancellationToken);
        var session = CreateOrReplaceSession(job.Name);
        var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.CancellationTokenSource.Token);

        session.ExecutionTask = ExecuteJobWithSessionAsync(job, settings, session, linkedTokenSource);
        return session.ExecutionTask;
    }

    private async Task ExecuteJobWithSessionAsync(
        BackupJob job,
        AppSettings settings,
        BackupExecutionSession session,
        CancellationTokenSource linkedTokenSource)
    {
        var strategy = BackupStrategyFactory.Create(job.Type);
        var context = new BackupExecutionContext(
            stateManager,
            loggerFactory(settings),
            settings,
            businessSoftwareDetector,
            fileEncryptionService,
            globalPriorityFileCoordinator,
            globalLargeFileTransferCoordinator,
            fileTransferService,
            session.PauseController);

        try
        {
            await strategy.ExecuteAsync(job, context, linkedTokenSource.Token);
        }
        catch (OperationCanceledException) when (session.CancellationTokenSource.IsCancellationRequested)
        {
        }
        finally
        {
            linkedTokenSource.Dispose();
            RemoveSession(job.Name, session);
        }
    }

    private async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        return settingsRepository is null
            ? new AppSettings()
            : await settingsRepository.LoadAsync(cancellationToken);
    }

    private BackupExecutionSession CreateOrReplaceSession(string jobName)
    {
        lock (executionRegistryLock)
        {
            if (executionSessions.TryGetValue(jobName, out var existingSession))
            {
                existingSession.PauseController.Resume();
                existingSession.CancellationTokenSource.Cancel();
            }

            var session = new BackupExecutionSession(jobName);
            executionSessions[jobName] = session;
            return session;
        }
    }

    private BackupExecutionSession? GetSession(string jobName)
    {
        lock (executionRegistryLock)
        {
            return executionSessions.TryGetValue(jobName, out var session) ? session : null;
        }
    }

    private List<BackupExecutionSession> GetSessionsSnapshot()
    {
        lock (executionRegistryLock)
        {
            return executionSessions.Values.ToList();
        }
    }

    private void RemoveSession(string jobName, BackupExecutionSession session)
    {
        lock (executionRegistryLock)
        {
            if (executionSessions.TryGetValue(jobName, out var currentSession) && ReferenceEquals(currentSession, session))
            {
                executionSessions.Remove(jobName);
            }
        }

        session.CancellationTokenSource.Dispose();
    }

    private sealed class BackupExecutionSession
    {
        public BackupExecutionSession(string jobName)
        {
            JobName = jobName;
        }

        public string JobName { get; }

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        public ExecutionPauseController PauseController { get; } = new();

        public Task ExecutionTask { get; set; } = Task.CompletedTask;
    }
}
