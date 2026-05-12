using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyLog;
using EasySave.Core.Configuration;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppSettingsRepository settingsRepository;
    private readonly BackupJobService jobService;
    private readonly BackupManager backupManager;
    private readonly StateManager stateManager;
    private readonly IBusinessSoftwareDetector businessSoftwareDetector;
    private readonly CancellationTokenSource runtimeRefreshCancellationTokenSource = new();
    private readonly List<BackupJob> selectedJobs = [];
    private bool isRefreshingStates;

    [ObservableProperty]
    private Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private ObservableCollection<BackupJob> jobs = [];

    [ObservableProperty]
    private ObservableCollection<BackupState> states = [];

    [ObservableProperty]
    private ObservableCollection<DashboardJobRow> dashboardJobs = [];

    [ObservableProperty]
    private ObservableCollection<JobListRow> jobListRows = [];

    [ObservableProperty]
    private BackupJob? selectedJob;

    [ObservableProperty]
    private string jobName = string.Empty;

    [ObservableProperty]
    private string sourceDirectory = string.Empty;

    [ObservableProperty]
    private string targetDirectory = string.Empty;

    [ObservableProperty]
    private BackupType selectedBackupType = BackupType.Complete;

    [ObservableProperty]
    private string selectedLanguage = "en";

    [ObservableProperty]
    private string selectedLogFormat = "json";

    [ObservableProperty]
    private string encryptedExtensionsText = "*";

    [ObservableProperty]
    private string priorityExtensionsText = string.Empty;

    [ObservableProperty]
    private string businessSoftwareProcessesText = "calc";

    [ObservableProperty]
    private string largeFileThresholdKoText = "0";

    [ObservableProperty]
    private string cryptoSoftPath = Path.Combine(AppPaths.BaseDirectory, "CryptoSoft");

    [ObservableProperty]
    private string cryptoKey = "EasySave";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string logPreviewText = string.Empty;

    [ObservableProperty]
    private string logPreviewPath = string.Empty;

    [ObservableProperty]
    private string logPreviewInfo = string.Empty;

    [ObservableProperty]
    private int selectedSectionIndex;

    [ObservableProperty]
    private string jobFilterText = string.Empty;

    [ObservableProperty]
    private bool isEditingJob;

    [ObservableProperty]
    private string editingOriginalJobName = string.Empty;

    [ObservableProperty]
    private bool isSettingsGuideVisible;

    [ObservableProperty]
    private bool isJobFormOverlayVisible;

    [ObservableProperty]
    private bool isDeleteConfirmationVisible;

    [ObservableProperty]
    private BackupJob? pendingDeleteJob;

    [ObservableProperty]
    private string pendingDeleteJobName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SelectionOption> languageOptions = [];

    [ObservableProperty]
    private ObservableCollection<SelectionOption> logFormatOptions = [];

    [ObservableProperty]
    private ObservableCollection<BackupTypeOption> backupTypeOptions = [];

    public MainWindowViewModel()
    {
        AppPaths.EnsureDirectories();

        settingsRepository = new AppSettingsRepository(AppPaths.SettingsFilePath);
        var repository = new BackupJobRepository(AppPaths.JobsFilePath);
        jobService = new BackupJobService(repository);
        stateManager = new StateManager(AppPaths.StateFilePath);
        businessSoftwareDetector = new ProcessBusinessSoftwareDetector();
        backupManager = new BackupManager(
            jobService,
            stateManager,
            CreateLogger,
            settingsRepository,
            businessSoftwareDetector,
            new CryptoSoftEncryptionService(),
            new FileSystemFileTransferService());

        RefreshJobsCommand = new AsyncRelayCommand(RefreshJobsAsync);
        RefreshStatesCommand = new AsyncRelayCommand(RefreshStatesAsync);
        AddJobCommand = new AsyncRelayCommand(AddJobAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        RunSelectedJobCommand = new AsyncRelayCommand(RunSelectedJobAsync);
        RunAllJobsCommand = new AsyncRelayCommand(RunAllJobsAsync);
        PauseSelectedJobCommand = new AsyncRelayCommand(PauseSelectedJobAsync);
        PauseAllJobsCommand = new AsyncRelayCommand(PauseAllJobsAsync);
        StopSelectedJobCommand = new AsyncRelayCommand(StopSelectedJobAsync);
        StopAllJobsCommand = new AsyncRelayCommand(StopAllJobsAsync);
        ResetJobFormCommand = new RelayCommand(ResetJobForm);
        NavigateToSectionCommand = new RelayCommand<int>(NavigateToSection);
        OpenDashboardJobCommand = new RelayCommand<BackupJob?>(OpenDashboardJob);
        RunDashboardJobCommand = new AsyncRelayCommand<BackupJob?>(RunDashboardJobAsync);
        StopDashboardJobCommand = new AsyncRelayCommand<BackupJob?>(StopDashboardJobAsync);
        DeleteJobCommand = new RelayCommand<BackupJob?>(RequestDeleteJob);
        PrepareCreateJobCommand = new RelayCommand(PrepareCreateJob);
        EditJobCommand = new RelayCommand<BackupJob?>(EditJob);
        ToggleSettingsGuideCommand = new RelayCommand(ToggleSettingsGuide);
        CloseSettingsGuideCommand = new RelayCommand(CloseSettingsGuide);
        CloseJobFormOverlayCommand = new RelayCommand(CloseJobFormOverlay);
        ConfirmDeleteJobCommand = new AsyncRelayCommand(ConfirmDeleteJobAsync);
        CancelDeleteJobCommand = new RelayCommand(CancelDeleteJob);
        RefreshLocalizedOptions();
    }

    public IAsyncRelayCommand RefreshJobsCommand { get; }

    public IAsyncRelayCommand RefreshStatesCommand { get; }

    public IAsyncRelayCommand AddJobCommand { get; }

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand RunSelectedJobCommand { get; }

    public IAsyncRelayCommand RunAllJobsCommand { get; }

    public IAsyncRelayCommand PauseSelectedJobCommand { get; }

    public IAsyncRelayCommand PauseAllJobsCommand { get; }

    public IAsyncRelayCommand StopSelectedJobCommand { get; }

    public IAsyncRelayCommand StopAllJobsCommand { get; }

    public IRelayCommand ResetJobFormCommand { get; }

    public IRelayCommand<int> NavigateToSectionCommand { get; }

    public IRelayCommand<BackupJob?> OpenDashboardJobCommand { get; }

    public IAsyncRelayCommand<BackupJob?> RunDashboardJobCommand { get; }

    public IAsyncRelayCommand<BackupJob?> StopDashboardJobCommand { get; }

    public IRelayCommand<BackupJob?> DeleteJobCommand { get; }

    public IRelayCommand PrepareCreateJobCommand { get; }

    public IRelayCommand<BackupJob?> EditJobCommand { get; }

    public IRelayCommand ToggleSettingsGuideCommand { get; }

    public IRelayCommand CloseSettingsGuideCommand { get; }

    public IRelayCommand CloseJobFormOverlayCommand { get; }

    public IAsyncRelayCommand ConfirmDeleteJobCommand { get; }

    public IRelayCommand CancelDeleteJobCommand { get; }

    public int TotalJobsCount => Jobs.Count;

    public int ActiveStatesCount => States.Count(state => string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase));

    public int FinishedStatesCount => States.Count(state => string.Equals(state.State, "Finished", StringComparison.OrdinalIgnoreCase));

    public int PausedStatesCount => States.Count(state => string.Equals(state.State, "Paused", StringComparison.OrdinalIgnoreCase));

    public int StoppedStatesCount => States.Count(state => string.Equals(state.State, "Stopped", StringComparison.OrdinalIgnoreCase));

    public int BlockedStatesCount => States.Count(state => string.Equals(state.State, "Blocked", StringComparison.OrdinalIgnoreCase));

    public int ErrorStatesCount => States.Count(state => string.Equals(state.State, "Error", StringComparison.OrdinalIgnoreCase));

    public string SelectedJobName => SelectedJob?.Name ?? Translate("NoJobSelectedValue");

    public string SelectedJobSource => SelectedJob?.SourceDirectory ?? Translate("NoDataPlaceholder");

    public string SelectedJobTarget => SelectedJob?.TargetDirectory ?? Translate("NoDataPlaceholder");

    public string SelectedJobTypeLabel => SelectedJob is null ? Translate("NoDataPlaceholder") : TranslateBackupType(SelectedJob.Type);

    public string CurrentLanguageLabel => FindLabel(LanguageOptions, SelectedLanguage);

    public string CurrentLogFormatLabel => FindLabel(LogFormatOptions, SelectedLogFormat);

    public string EncryptedExtensionsSummary => string.IsNullOrWhiteSpace(EncryptedExtensionsText)
        ? Translate("NoExtensionsConfigured")
        : EncryptedExtensionsText;

    public string PriorityExtensionsSummary => string.IsNullOrWhiteSpace(PriorityExtensionsText)
        ? Translate("NoExtensionsConfigured")
        : PriorityExtensionsText;

    public string LargeFileThresholdSummary => string.IsNullOrWhiteSpace(LargeFileThresholdKoText)
        ? "0"
        : LargeFileThresholdKoText;

    public string BusinessSoftwareSummary => string.IsNullOrWhiteSpace(BusinessSoftwareProcessesText)
        ? Translate("NoBusinessSoftwareConfigured")
        : BusinessSoftwareProcessesText;

    public string GlobalStatusLabel
    {
        get
        {
            if (IsBusy)
            {
                return Translate("StatusBusy");
            }

            if (ActiveStatesCount > 0)
            {
                return Translate("StatusActive");
            }

            if (PausedStatesCount > 0)
            {
                return Translate("StatusPaused");
            }

            if (StoppedStatesCount > 0)
            {
                return Translate("StatusStopped");
            }

            if (BlockedStatesCount > 0)
            {
                return Translate("StatusBlocked");
            }

            if (ErrorStatesCount > 0)
            {
                return Translate("StatusError");
            }

            if (FinishedStatesCount > 0)
            {
                return Translate("StatusFinished");
            }

            return Translate("StatusReady");
        }
    }

    public string BusinessSoftwareAlertText => string.IsNullOrWhiteSpace(BusinessSoftwareProcessesText)
        ? Translate("BusinessSoftwareAlertInactive")
        : string.Format(
            CultureInfo.InvariantCulture,
            Translate("BusinessSoftwareAlertActive"),
            BusinessSoftwareProcessesText);

    public bool IsBusinessSoftwareDetected
    {
        get
        {
            var settings = BuildSettingsFromViewModel();
            if (settings.GetNormalizedBusinessSoftwareProcesses().Count == 0)
            {
                return false;
            }

            return businessSoftwareDetector.Detect(settings).IsDetected;
        }
    }

    public string ExecutionBusinessSoftwareAlertText
    {
        get
        {
            var settings = BuildSettingsFromViewModel();
            if (settings.GetNormalizedBusinessSoftwareProcesses().Count == 0)
            {
                return Translate("BusinessSoftwareAlertInactive");
            }

            var detection = businessSoftwareDetector.Detect(settings);
            return detection.IsDetected
                ? string.Format(CultureInfo.InvariantCulture, Translate("ExecutionBusinessSoftwareDetected"), detection.ProcessName)
                : Translate("ExecutionBusinessSoftwareSafe");
        }
    }

    public string SelectedJobStateLabel => TranslateState(GetRelevantState()?.State);

    public double SelectedJobProgressValue => GetRelevantState()?.Progression ?? 0;

    public string SelectedJobProgressText => $"{SelectedJobProgressValue.ToString("0.##", CultureInfo.InvariantCulture)} %";

    public string SelectedJobCurrentSource => ValueOrPlaceholder(GetRelevantState()?.CurrentSourceFilePath);

    public string SelectedJobCurrentDestination => ValueOrPlaceholder(GetRelevantState()?.CurrentDestinationFilePath);

    public string SelectedJobRemainingFilesText => GetRelevantState() is null
        ? Translate("NoDataPlaceholder")
        : GetRelevantState()!.RemainingFiles.ToString(CultureInfo.InvariantCulture);

    public string SelectedJobTotalFilesText => GetRelevantState() is null
        ? Translate("NoDataPlaceholder")
        : GetRelevantState()!.TotalFilesToCopy.ToString(CultureInfo.InvariantCulture);

    public string SelectedJobRemainingSizeText => GetRelevantState() is null
        ? Translate("NoDataPlaceholder")
        : FormatSize(GetRelevantState()!.RemainingSize);

    public string SelectedJobTotalSizeText => GetRelevantState() is null
        ? Translate("NoDataPlaceholder")
        : FormatSize(GetRelevantState()!.TotalFilesSize);

    public string SelectedJobLastUpdateText => GetRelevantState() is null
        ? Translate("NoDataPlaceholder")
        : GetRelevantState()!.LastActionTimestamp.ToString("g", CultureInfo.CurrentCulture);

    public string SelectedJobStatusNote => SelectedJob is null
        ? Translate("SelectJobHint")
        : GetRelevantState()?.State switch
        {
            "Paused" => Translate("ExecutionPausedNote"),
            "Stopped" => Translate("ExecutionStoppedNote"),
            "Active" => Translate("ExecutionRunningNote"),
            _ => Translate("ExecutionControlReadyNote")
        };

    public string LatestBackupText => States.Count == 0
        ? Translate("NoDataPlaceholder")
        : States
            .OrderByDescending(state => state.LastActionTimestamp)
            .First()
            .LastActionTimestamp
            .ToString("g", CultureInfo.CurrentCulture);

    public string DashboardBusinessSoftwareRuntimeText
    {
        get
        {
            var settings = BuildSettingsFromViewModel();
            if (settings.GetNormalizedBusinessSoftwareProcesses().Count == 0)
            {
                return Translate("NoBusinessSoftwareConfigured");
            }

            var detection = businessSoftwareDetector.Detect(settings);
            return detection.IsDetected
                ? string.Format(CultureInfo.InvariantCulture, Translate("BusinessSoftwareDetectedValue"), detection.ProcessName)
                : Translate("DashboardBusinessSoftwareSafe");
        }
    }

    public string DashboardPrimaryGaugeTitle => SelectedJob is null
        ? Translate("SelectedJobMetric")
        : SelectedJobName;

    public string DashboardPrimaryGaugeValue => SelectedJobProgressText;

    public string DashboardPrimaryGaugeSubtitle => SelectedJobStateLabel;

    public string DashboardFooterCenterText => $"{Translate("LanguageLabel")}: {CurrentLanguageLabel}";

    public string DashboardFooterRightText => $"{Translate("LogFormatLabel")}: {CurrentLogFormatLabel}";

    public double DashboardQuickStatsPercent => TotalJobsCount == 0
        ? 0
        : Math.Round((double)FinishedStatesCount / TotalJobsCount * 100, 2);

    public string DashboardQuickStatsText => string.Format(
        CultureInfo.InvariantCulture,
        Translate("DashboardQuickStatsValue"),
        FinishedStatesCount,
        TotalJobsCount);

    public int FilteredJobsCount => JobListRows.Count;

    public string JobsSelectedCountText => string.Format(
        CultureInfo.InvariantCulture,
        Translate("JobsSelectedCountValue"),
        selectedJobs.Count);

    public string JobsServiceStatusText => string.Format(
        CultureInfo.InvariantCulture,
        Translate("JobsServiceStatusValue"),
        GlobalStatusLabel);

    public string JobsStorageUsageText => TotalJobsCount == 0
        ? Translate("NoDataPlaceholder")
        : $"{Math.Round((double)ActiveStatesCount / TotalJobsCount * 100, 0).ToString("0", CultureInfo.InvariantCulture)}%";

    public double JobsStorageUsagePercent => TotalJobsCount == 0
        ? 0
        : Math.Round((double)ActiveStatesCount / TotalJobsCount * 100, 2);

    public string JobFormTitle => IsEditingJob
        ? Translate("EditJobPageTitle")
        : Translate("CreatePageTitle");

    public string JobFormSubtitle => IsEditingJob
        ? Translate("EditJobPageSubtitle")
        : Translate("CreatePageSubtitle");

    public string SaveJobButtonText => IsEditingJob
        ? Translate("SaveJobChanges")
        : Translate("CreateJob");

    public string LogDirectoryPath => AppPaths.LogsDirectory;

    public string StateFilePath => AppPaths.StateFilePath;

    public string JobsConfigPath => AppPaths.JobsFilePath;

    public string SettingsConfigPath => AppPaths.SettingsFilePath;

    public bool CanStopSelectedJob => GetSelectedJobsOrFallback().Any(CanStopJob);

    public bool CanStopAllJobs => States.Any(state => IsStopEligibleState(state.State));

    public string DeleteConfirmationText => string.Format(
        CultureInfo.InvariantCulture,
        Translate("DeleteJobConfirmMessage"),
        PendingDeleteJobName);

    public string Translate(string key)
    {
        return Texts.TryGetValue(key, out var value) ? value : key;
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsIntoViewModelAsync();
        await RefreshJobsAsync();
        await RefreshStatesAsync();
        await RefreshLogPreviewAsync();
        StatusMessage = Translate("StatusReady");
        _ = MonitorRuntimeStateAsync(runtimeRefreshCancellationTokenSource.Token);
    }

    public void SetSourceDirectory(string path)
    {
        SourceDirectory = path;
    }

    public void SetTargetDirectory(string path)
    {
        TargetDirectory = path;
    }

    public void SetSelectedJobs(IEnumerable<BackupJob> jobs)
    {
        selectedJobs.Clear();
        selectedJobs.AddRange(jobs
            .Where(job => job is not null)
            .DistinctBy(job => job.Name, StringComparer.OrdinalIgnoreCase));

        var nextSelectedJob = selectedJobs.Count == 0
            ? null
            : SelectedJob is not null &&
              selectedJobs.Any(job => string.Equals(job.Name, SelectedJob.Name, StringComparison.OrdinalIgnoreCase))
                ? SelectedJob
                : selectedJobs[0];

        if (!ReferenceEquals(SelectedJob, nextSelectedJob))
        {
            SelectedJob = nextSelectedJob;
        }
        else
        {
            OnPropertyChanged(nameof(JobsSelectedCountText));
        }
    }

    partial void OnSelectedJobChanged(BackupJob? value)
    {
        NotifySelectionProperties();
        RebuildDashboardRows();
        OnPropertyChanged(nameof(JobsSelectedCountText));
    }

    partial void OnJobFilterTextChanged(string value)
    {
        RebuildJobListRows();
    }

    partial void OnIsEditingJobChanged(bool value)
    {
        OnPropertyChanged(nameof(JobFormTitle));
        OnPropertyChanged(nameof(JobFormSubtitle));
        OnPropertyChanged(nameof(SaveJobButtonText));
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLanguageLabel));
        OnPropertyChanged(nameof(DashboardFooterCenterText));
        OnPropertyChanged(nameof(JobFormTitle));
        OnPropertyChanged(nameof(JobFormSubtitle));
        OnPropertyChanged(nameof(SaveJobButtonText));
    }

    partial void OnSelectedLogFormatChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLogFormatLabel));
        OnPropertyChanged(nameof(DashboardFooterRightText));
        _ = RefreshLogPreviewAsync();
    }

    partial void OnEncryptedExtensionsTextChanged(string value)
    {
        OnPropertyChanged(nameof(EncryptedExtensionsSummary));
    }

    partial void OnPriorityExtensionsTextChanged(string value)
    {
        OnPropertyChanged(nameof(PriorityExtensionsSummary));
    }

    partial void OnLargeFileThresholdKoTextChanged(string value)
    {
        OnPropertyChanged(nameof(LargeFileThresholdSummary));
    }

    partial void OnBusinessSoftwareProcessesTextChanged(string value)
    {
        OnPropertyChanged(nameof(BusinessSoftwareSummary));
        OnPropertyChanged(nameof(BusinessSoftwareAlertText));
        OnPropertyChanged(nameof(DashboardBusinessSoftwareRuntimeText));
        OnPropertyChanged(nameof(IsBusinessSoftwareDetected));
        OnPropertyChanged(nameof(ExecutionBusinessSoftwareAlertText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(GlobalStatusLabel));
        OnPropertyChanged(nameof(DashboardQuickStatsPercent));
        OnPropertyChanged(nameof(DashboardQuickStatsText));
        OnPropertyChanged(nameof(IsBusinessSoftwareDetected));
        OnPropertyChanged(nameof(ExecutionBusinessSoftwareAlertText));
    }

    private async Task LoadSettingsIntoViewModelAsync()
    {
        var settings = await settingsRepository.LoadAsync();
        SelectedLanguage = settings.Language;
        SelectedLogFormat = settings.LogFormatName;
        EncryptedExtensionsText = string.Join(";", settings.EncryptedExtensions);
        PriorityExtensionsText = string.Join(";", settings.PriorityExtensions);
        BusinessSoftwareProcessesText = string.Join(";", settings.BusinessSoftwareProcesses);
        LargeFileThresholdKoText = settings.LargeFileThresholdKo.ToString(CultureInfo.InvariantCulture);
        CryptoSoftPath = string.IsNullOrWhiteSpace(settings.CryptoSoftPath)
            ? Path.Combine(AppPaths.BaseDirectory, "CryptoSoft")
            : settings.CryptoSoftPath;
        CryptoKey = settings.CryptoKey;
        await LoadTranslationsAsync(SelectedLanguage);
    }

    private async Task LoadTranslationsAsync(string language)
    {
        var resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", $"{language}.json");
        if (!File.Exists(resourcePath))
        {
            Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        await using var stream = File.OpenRead(resourcePath);
        Texts = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        RefreshLocalizedOptions();
        NotifyAllUiSummaries();
    }

    private async Task RefreshJobsAsync()
    {
        Jobs = new ObservableCollection<BackupJob>(await jobService.GetJobsAsync());
        SyncSelectedJobsWithCurrentJobs();

        if (Jobs.Count == 0)
        {
            SelectedJob = null;
        }
        else if (SelectedJob is null || !Jobs.Any(job => string.Equals(job.Name, SelectedJob.Name, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedJob = Jobs[0];
        }

        OnPropertyChanged(nameof(TotalJobsCount));
        OnPropertyChanged(nameof(DashboardQuickStatsPercent));
        OnPropertyChanged(nameof(DashboardQuickStatsText));
        OnPropertyChanged(nameof(JobsStorageUsagePercent));
        OnPropertyChanged(nameof(JobsStorageUsageText));
        NotifySelectionProperties();
        RebuildDashboardRows();
        RebuildJobListRows();
    }

    private async Task RefreshStatesAsync()
    {
        if (isRefreshingStates)
        {
            return;
        }

        isRefreshingStates = true;
        try
        {
            States = new ObservableCollection<BackupState>(await stateManager.GetStatesAsync());
            OnPropertyChanged(nameof(ActiveStatesCount));
            OnPropertyChanged(nameof(FinishedStatesCount));
            OnPropertyChanged(nameof(PausedStatesCount));
            OnPropertyChanged(nameof(StoppedStatesCount));
            OnPropertyChanged(nameof(BlockedStatesCount));
            OnPropertyChanged(nameof(ErrorStatesCount));
            OnPropertyChanged(nameof(GlobalStatusLabel));
            OnPropertyChanged(nameof(DashboardQuickStatsPercent));
            OnPropertyChanged(nameof(DashboardQuickStatsText));
            OnPropertyChanged(nameof(JobsServiceStatusText));
            OnPropertyChanged(nameof(JobsStorageUsagePercent));
            OnPropertyChanged(nameof(JobsStorageUsageText));
            NotifySelectionProperties();
            RebuildDashboardRows();
            RebuildJobListRows();
            await RefreshLogPreviewAsync();
        }
        finally
        {
            isRefreshingStates = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var settings = BuildValidatedSettingsFromViewModel();
            await settingsRepository.SaveAsync(settings);
            await LoadTranslationsAsync(settings.Language);
            await RefreshLogPreviewAsync();
            StatusMessage = Translate("SettingsSaved");
        });
    }

    private async Task AddJobAsync()
    {
        await RunBusyAsync(async () =>
        {
            var job = new BackupJob
            {
                Name = JobName.Trim(),
                SourceDirectory = SourceDirectory.Trim(),
                TargetDirectory = TargetDirectory.Trim(),
                Type = SelectedBackupType
            };

            if (IsEditingJob)
            {
                await jobService.UpdateJobAsync(EditingOriginalJobName, job);
                StatusMessage = Translate("JobUpdated");
            }
            else
            {
                await jobService.AddJobAsync(job);
                StatusMessage = Translate("JobCreated");
            }

            ResetJobForm();
            IsJobFormOverlayVisible = false;
            await RefreshJobsAsync();
        });
    }

    private async Task RunSelectedJobAsync()
    {
        var jobsToRun = GetSelectedJobsOrFallback();
        if (jobsToRun.Count == 0)
        {
            StatusMessage = Translate("SelectJobFirst");
            return;
        }

        await settingsRepository.SaveAsync(BuildValidatedSettingsFromViewModel());
        var stoppedOrInactiveJobIndexes = new List<int>();
        var resumedAny = false;

        foreach (var job in jobsToRun)
        {
            if (backupManager.IsJobRunning(job.Name))
            {
                resumedAny |= await backupManager.ResumeJobAsync(job.Name);
            }
            else
            {
                var jobIndex = Jobs.IndexOf(job);
                if (jobIndex >= 0)
                {
                    stoppedOrInactiveJobIndexes.Add(jobIndex + 1);
                }
            }
        }

        if (stoppedOrInactiveJobIndexes.Count > 0)
        {
            await backupManager.StartJobsAsync(stoppedOrInactiveJobIndexes);
        }

        StatusMessage = resumedAny && stoppedOrInactiveJobIndexes.Count == 0
            ? Translate("ExecutionResumed")
            : Translate("ExecutionStarted");
        await RefreshStatesAsync();
    }

    private async Task RunAllJobsAsync()
    {
        await settingsRepository.SaveAsync(BuildValidatedSettingsFromViewModel());

        var stoppedOrInactiveJobIndexes = Jobs
            .Select((job, index) => new { job, index })
            .Where(item => !backupManager.IsJobRunning(item.job.Name))
            .Select(item => item.index + 1)
            .ToList();

        await backupManager.ResumeAllJobsAsync();
        if (stoppedOrInactiveJobIndexes.Count > 0)
        {
            await backupManager.StartJobsAsync(stoppedOrInactiveJobIndexes);
        }

        StatusMessage = Translate("ExecutionStarted");
        await RefreshStatesAsync();
    }

    private async Task PauseSelectedJobAsync()
    {
        var jobsToPause = GetSelectedJobsOrFallback();
        if (jobsToPause.Count == 0)
        {
            StatusMessage = Translate("SelectJobFirst");
            return;
        }

        var pausedAny = false;
        foreach (var job in jobsToPause)
        {
            pausedAny |= await backupManager.PauseJobAsync(job.Name);
        }

        if (pausedAny)
        {
            StatusMessage = Translate("ExecutionPaused");
            await RefreshStatesAsync();
        }
    }

    private async Task PauseAllJobsAsync()
    {
        await backupManager.PauseAllJobsAsync();
        StatusMessage = Translate("ExecutionPaused");
        await RefreshStatesAsync();
    }

    private async Task StopSelectedJobAsync()
    {
        var jobsToStop = GetSelectedJobsOrFallback();
        if (jobsToStop.Count == 0)
        {
            StatusMessage = Translate("SelectJobFirst");
            return;
        }

        var stoppedAny = false;
        foreach (var job in jobsToStop)
        {
            stoppedAny |= await backupManager.StopJobAsync(job.Name);
        }

        if (stoppedAny)
        {
            StatusMessage = Translate("ExecutionStopped");
            await RefreshStatesAsync();
        }
    }

    private async Task StopAllJobsAsync()
    {
        await backupManager.StopAllJobsAsync();
        StatusMessage = Translate("ExecutionStopped");
        await RefreshStatesAsync();
    }

    private async Task RunDashboardJobAsync(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        SetSelectedJobs([job]);
        SelectedJob = job;
        await RunSelectedJobAsync();
    }

    private async Task StopDashboardJobAsync(BackupJob? job)
    {
        if (!CanStopJob(job))
        {
            return;
        }

        SetSelectedJobs([job!]);
        SelectedJob = job;
        await StopSelectedJobAsync();
    }

    private void RequestDeleteJob(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        PendingDeleteJob = job;
        PendingDeleteJobName = job.Name;
        IsDeleteConfirmationVisible = true;
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    private async Task ConfirmDeleteJobAsync()
    {
        var job = PendingDeleteJob;
        if (job is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (backupManager.IsJobRunning(job.Name))
            {
                await backupManager.StopJobAsync(job.Name);
            }

            await jobService.DeleteJobAsync(job.Name);
            await stateManager.RemoveStateAsync(job.Name);

            selectedJobs.RemoveAll(selected => string.Equals(selected.Name, job.Name, StringComparison.OrdinalIgnoreCase));
            if (SelectedJob is not null && string.Equals(SelectedJob.Name, job.Name, StringComparison.OrdinalIgnoreCase))
            {
                SelectedJob = null;
            }

            StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("JobDeleted"), job.Name);
            CancelDeleteJob();
            await RefreshJobsAsync();
            await RefreshStatesAsync();
        });
    }

    private void CancelDeleteJob()
    {
        PendingDeleteJob = null;
        PendingDeleteJobName = string.Empty;
        IsDeleteConfirmationVisible = false;
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception exception)
        {
            StatusMessage = TranslateUserFacingMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MonitorRuntimeStateAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(400));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshStatesAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshLogPreviewAsync()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);

            var extension = string.Equals(SelectedLogFormat, "xml", StringComparison.OrdinalIgnoreCase) ? ".xml" : ".json";
            var exactFile = Path.Combine(AppPaths.LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}{extension}");
            var fallbackFile = Directory
                .EnumerateFiles(AppPaths.LogsDirectory, $"*{extension}", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            var targetFile = File.Exists(exactFile) ? exactFile : fallbackFile;
            LogPreviewPath = targetFile ?? Path.Combine(AppPaths.LogsDirectory, $"yyyy-MM-dd{extension}");

            if (string.IsNullOrWhiteSpace(targetFile) || !File.Exists(targetFile))
            {
                LogPreviewText = Translate("LogsPreviewEmpty");
                LogPreviewInfo = Translate("LogsPreviewHint");
                return;
            }

            var content = await File.ReadAllTextAsync(targetFile);
            LogPreviewText = BuildPreview(content);
            LogPreviewInfo = string.Format(
                CultureInfo.InvariantCulture,
                Translate("LogsPreviewLoaded"),
                Path.GetFileName(targetFile));
        }
        catch (Exception exception)
        {
            LogPreviewText = exception.Message;
            LogPreviewInfo = Translate("LogsPreviewError");
        }
    }

    private AppSettings BuildSettingsFromViewModel()
    {
        return new AppSettings
        {
            Language = SelectedLanguage,
            LogFormatName = SelectedLogFormat,
            EncryptedExtensions = SplitList(EncryptedExtensionsText),
            PriorityExtensions = SplitList(PriorityExtensionsText),
            BusinessSoftwareProcesses = SplitList(BusinessSoftwareProcessesText),
            LargeFileThresholdKo = ParseLargeFileThresholdKo(LargeFileThresholdKoText),
            CryptoSoftPath = CryptoSoftPath.Trim(),
            CryptoKey = CryptoKey.Trim()
        };
    }

    private AppSettings BuildValidatedSettingsFromViewModel()
    {
        var settings = BuildSettingsFromViewModel();

        settings.LargeFileThresholdKo = ParseValidatedLargeFileThresholdKo(LargeFileThresholdKoText);
        if (string.IsNullOrWhiteSpace(settings.CryptoSoftPath))
        {
            throw new InvalidOperationException(Translate("CryptoSoftPathRequired"));
        }

        if (!File.Exists(settings.CryptoSoftPath) && !Directory.Exists(settings.CryptoSoftPath))
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                Translate("CryptoSoftPathNotFound"),
                settings.CryptoSoftPath));
        }

        if (string.IsNullOrWhiteSpace(settings.CryptoKey))
        {
            throw new InvalidOperationException(Translate("CryptoKeyRequired"));
        }

        return settings;
    }

    private static int ParseLargeFileThresholdKo(string rawValue)
    {
        return int.TryParse(rawValue?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var thresholdKo)
            ? Math.Max(0, thresholdKo)
            : 0;
    }

    private int ParseValidatedLargeFileThresholdKo(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return 0;
        }

        if (!int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var thresholdKo))
        {
            throw new InvalidOperationException(Translate("LargeFileThresholdInvalid"));
        }

        if (thresholdKo < 0)
        {
            throw new InvalidOperationException(Translate("LargeFileThresholdNegative"));
        }

        return thresholdKo;
    }

    private static List<string> SplitList(string rawValue)
    {
        return rawValue
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private void ResetJobForm()
    {
        IsEditingJob = false;
        EditingOriginalJobName = string.Empty;
        JobName = string.Empty;
        SourceDirectory = string.Empty;
        TargetDirectory = string.Empty;
        SelectedBackupType = BackupType.Complete;
        StatusMessage = Translate("JobFormReset");
    }

    private void PrepareCreateJob()
    {
        ResetJobForm();
        SelectedSectionIndex = 1;
        IsJobFormOverlayVisible = true;
    }

    private void EditJob(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        IsEditingJob = true;
        EditingOriginalJobName = job.Name;
        JobName = job.Name;
        SourceDirectory = job.SourceDirectory;
        TargetDirectory = job.TargetDirectory;
        SelectedBackupType = job.Type;
        SelectedSectionIndex = 1;
        IsJobFormOverlayVisible = true;
        StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("EditJobLoaded"), job.Name);
    }

    private void NavigateToSection(int index)
    {
        SelectedSectionIndex = index;
    }

    private void OpenDashboardJob(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        SetSelectedJobs([job]);
        SelectedJob = job;
        SelectedSectionIndex = 3;
        StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("DashboardJobOpened"), job.Name);
    }

    private void ToggleSettingsGuide()
    {
        IsSettingsGuideVisible = !IsSettingsGuideVisible;
    }

    private void CloseSettingsGuide()
    {
        IsSettingsGuideVisible = false;
    }

    private void CloseJobFormOverlay()
    {
        ResetJobForm();
        IsJobFormOverlayVisible = false;
        if (SelectedSectionIndex == 2)
        {
            SelectedSectionIndex = 1;
        }

        StatusMessage = string.Empty;
    }

    private BackupState? GetRelevantState()
    {
        if (SelectedJob is not null)
        {
            var matchingState = States.FirstOrDefault(
                state => string.Equals(state.Name, SelectedJob.Name, StringComparison.OrdinalIgnoreCase));
            if (matchingState is not null)
            {
                return matchingState;
            }
        }

        return States
            .OrderByDescending(state => state.LastActionTimestamp)
            .FirstOrDefault();
    }

    private void RebuildDashboardRows()
    {
        DashboardJobs = new ObservableCollection<DashboardJobRow>(
            Jobs.Select(job =>
            {
                var state = States.FirstOrDefault(existing => string.Equals(existing.Name, job.Name, StringComparison.OrdinalIgnoreCase));
                var status = TranslateState(state?.State ?? "Inactive");
                var completion = state is null
                    ? "--"
                    : $"{state.Progression.ToString("0.##", CultureInfo.InvariantCulture)}%";

                return new DashboardJobRow(job, job.Name, status, completion, CanStopJob(job));
            }));
    }

    private void RebuildJobListRows()
    {
        IEnumerable<BackupJob> filteredJobs = Jobs;

        if (!string.IsNullOrWhiteSpace(JobFilterText))
        {
            filteredJobs = filteredJobs.Where(job =>
                job.Name.Contains(JobFilterText, StringComparison.OrdinalIgnoreCase) ||
                job.SourceDirectory.Contains(JobFilterText, StringComparison.OrdinalIgnoreCase) ||
                job.TargetDirectory.Contains(JobFilterText, StringComparison.OrdinalIgnoreCase) ||
                job.Type.ToString().Contains(JobFilterText, StringComparison.OrdinalIgnoreCase));
        }

        JobListRows = new ObservableCollection<JobListRow>(
            filteredJobs.Select(job =>
            {
                var state = States.FirstOrDefault(existing => string.Equals(existing.Name, job.Name, StringComparison.OrdinalIgnoreCase));
                var statusKey = state?.State switch
                {
                    "Finished" => "JobsStatusSuccess",
                    "Active" => "JobsStatusActive",
                    "Paused" => "JobsStatusPaused",
                    "Stopped" => "JobsStatusStopped",
                    "Blocked" => "JobsStatusBlocked",
                    "Error" => "JobsStatusFailed",
                    _ => "JobsStatusPending"
                };

                var lastRun = state is null
                    ? Translate("JobsNeverRun")
                    : state.LastActionTimestamp.ToString("g", CultureInfo.CurrentCulture);

                return new JobListRow(
                    job,
                    job.Name,
                    job.SourceDirectory,
                    job.TargetDirectory,
                    TranslateBackupType(job.Type),
                    lastRun,
                    Translate(statusKey),
                    statusKey,
                    CanStopJob(job));
            }));

        SyncSelectedJobsWithCurrentJobs();
        OnPropertyChanged(nameof(FilteredJobsCount));
    }

    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(SelectedJobName));
        OnPropertyChanged(nameof(SelectedJobSource));
        OnPropertyChanged(nameof(SelectedJobTarget));
        OnPropertyChanged(nameof(SelectedJobTypeLabel));
        OnPropertyChanged(nameof(SelectedJobStateLabel));
        OnPropertyChanged(nameof(SelectedJobProgressValue));
        OnPropertyChanged(nameof(SelectedJobProgressText));
        OnPropertyChanged(nameof(SelectedJobCurrentSource));
        OnPropertyChanged(nameof(SelectedJobCurrentDestination));
        OnPropertyChanged(nameof(SelectedJobRemainingFilesText));
        OnPropertyChanged(nameof(SelectedJobTotalFilesText));
        OnPropertyChanged(nameof(SelectedJobRemainingSizeText));
        OnPropertyChanged(nameof(SelectedJobTotalSizeText));
        OnPropertyChanged(nameof(SelectedJobLastUpdateText));
        OnPropertyChanged(nameof(SelectedJobStatusNote));
        OnPropertyChanged(nameof(LatestBackupText));
        OnPropertyChanged(nameof(DashboardPrimaryGaugeTitle));
        OnPropertyChanged(nameof(DashboardPrimaryGaugeValue));
        OnPropertyChanged(nameof(DashboardPrimaryGaugeSubtitle));
        OnPropertyChanged(nameof(DashboardBusinessSoftwareRuntimeText));
        OnPropertyChanged(nameof(DashboardQuickStatsPercent));
        OnPropertyChanged(nameof(DashboardQuickStatsText));
        OnPropertyChanged(nameof(IsBusinessSoftwareDetected));
        OnPropertyChanged(nameof(ExecutionBusinessSoftwareAlertText));
        OnPropertyChanged(nameof(FilteredJobsCount));
        OnPropertyChanged(nameof(JobsSelectedCountText));
        OnPropertyChanged(nameof(JobsServiceStatusText));
        OnPropertyChanged(nameof(JobsStorageUsagePercent));
        OnPropertyChanged(nameof(JobsStorageUsageText));
        OnPropertyChanged(nameof(JobFormTitle));
        OnPropertyChanged(nameof(JobFormSubtitle));
        OnPropertyChanged(nameof(SaveJobButtonText));
        OnPropertyChanged(nameof(CanStopSelectedJob));
        OnPropertyChanged(nameof(CanStopAllJobs));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    private void NotifyAllUiSummaries()
    {
        OnPropertyChanged(nameof(CurrentLanguageLabel));
        OnPropertyChanged(nameof(CurrentLogFormatLabel));
        OnPropertyChanged(nameof(EncryptedExtensionsSummary));
        OnPropertyChanged(nameof(BusinessSoftwareSummary));
        OnPropertyChanged(nameof(BusinessSoftwareAlertText));
        OnPropertyChanged(nameof(GlobalStatusLabel));
        OnPropertyChanged(nameof(LatestBackupText));
        OnPropertyChanged(nameof(DashboardBusinessSoftwareRuntimeText));
        OnPropertyChanged(nameof(DashboardFooterCenterText));
        OnPropertyChanged(nameof(DashboardFooterRightText));
        OnPropertyChanged(nameof(DashboardQuickStatsPercent));
        OnPropertyChanged(nameof(DashboardQuickStatsText));
        OnPropertyChanged(nameof(IsBusinessSoftwareDetected));
        OnPropertyChanged(nameof(ExecutionBusinessSoftwareAlertText));
        OnPropertyChanged(nameof(FilteredJobsCount));
        OnPropertyChanged(nameof(JobsSelectedCountText));
        OnPropertyChanged(nameof(JobsServiceStatusText));
        OnPropertyChanged(nameof(JobsStorageUsagePercent));
        OnPropertyChanged(nameof(JobsStorageUsageText));
        OnPropertyChanged(nameof(JobFormTitle));
        OnPropertyChanged(nameof(JobFormSubtitle));
        OnPropertyChanged(nameof(SaveJobButtonText));
        RebuildDashboardRows();
        RebuildJobListRows();
        NotifySelectionProperties();
    }

    private IReadOnlyList<BackupJob> GetSelectedJobsOrFallback()
    {
        if (selectedJobs.Count > 0)
        {
            return selectedJobs
                .Where(selected => Jobs.Any(job => string.Equals(job.Name, selected.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return SelectedJob is null ? [] : [SelectedJob];
    }

    private void SyncSelectedJobsWithCurrentJobs()
    {
        if (selectedJobs.Count == 0)
        {
            return;
        }

        var currentSelections = selectedJobs
            .Select(selected => Jobs.FirstOrDefault(job => string.Equals(job.Name, selected.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(job => job is not null)
            .Cast<BackupJob>()
            .ToList();

        if (currentSelections.Count == selectedJobs.Count &&
            currentSelections.Select(job => job.Name)
                .SequenceEqual(selectedJobs.Select(job => job.Name), StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        selectedJobs.Clear();
        selectedJobs.AddRange(currentSelections);
        OnPropertyChanged(nameof(JobsSelectedCountText));
    }

    private static string BuildPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        using var reader = new StringReader(content);
        var builder = new StringBuilder();
        const int maxLines = 40;

        for (var index = 0; index < maxLines; index++)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                break;
            }

            builder.AppendLine(line);
        }

        if (reader.ReadLine() is not null)
        {
            builder.AppendLine("...");
        }

        return builder.ToString().TrimEnd();
    }

    private string ValueOrPlaceholder(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Translate("NoDataPlaceholder") : value;
    }

    private static string FindLabel(IEnumerable<SelectionOption> options, string value)
    {
        return options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))?.Label ?? value;
    }

    private void RefreshLocalizedOptions()
    {
        LanguageOptions =
        [
            new SelectionOption("fr", Translate("LanguageFrench")),
            new SelectionOption("en", Translate("LanguageEnglish"))
        ];

        LogFormatOptions =
        [
            new SelectionOption("json", Translate("LogFormatJson")),
            new SelectionOption("xml", Translate("LogFormatXml"))
        ];

        BackupTypeOptions =
        [
            new BackupTypeOption(BackupType.Complete, Translate("CompleteLabel")),
            new BackupTypeOption(BackupType.Differential, Translate("DifferentialLabel"))
        ];
    }

    private string TranslateBackupType(BackupType backupType)
    {
        return backupType switch
        {
            BackupType.Complete => Translate("CompleteLabel"),
            BackupType.Differential => Translate("DifferentialLabel"),
            _ => backupType.ToString()
        };
    }

    private string TranslateState(string? state)
    {
        var key = state switch
        {
            "Active" => "JobsStatusActive",
            "Finished" => "JobsStatusSuccess",
            "Paused" => "JobsStatusPaused",
            "Stopped" => "JobsStatusStopped",
            "Blocked" => "JobsStatusBlocked",
            "Error" => "JobsStatusFailed",
            "Inactive" => "DashboardStatusInactive",
            null or "" => "StateNotAvailable",
            _ => "StateNotAvailable"
        };

        return Translate(key);
    }

    private string FormatSize(long size)
    {
        if (size < 1024)
        {
            return $"{size} {Translate("SizeUnitB")}";
        }

        var units = new[]
        {
            Translate("SizeUnitKB"),
            Translate("SizeUnitMB"),
            Translate("SizeUnitGB"),
            Translate("SizeUnitTB")
        };
        var scaled = size;
        var unitIndex = -1;

        while (scaled >= 1024 && unitIndex < units.Length - 1)
        {
            scaled /= 1024;
            unitIndex++;
        }

        var precise = size / Math.Pow(1024, unitIndex + 1);
        return $"{precise.ToString("0.##", CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }

    private static ILoggerService CreateLogger(AppSettings settings)
    {
        return settings.LogFormat switch
        {
            LogFormat.Json => new JsonLoggerService(AppPaths.LogsDirectory),
            LogFormat.Xml => new XmlLoggerService(AppPaths.LogsDirectory),
            _ => throw new ArgumentOutOfRangeException(nameof(settings.LogFormat), "Unsupported log format.")
        };
    }

    private string TranslateUserFacingMessage(Exception exception)
    {
        return exception switch
        {
            ArgumentOutOfRangeException => Translate("BackupJobIndexOutOfRange"),
            DirectoryNotFoundException when exception.Message.StartsWith("Source path does not exist:", StringComparison.Ordinal) => Translate("SourceDirectoryDoesNotExist"),
            ArgumentException when exception.Message == "The backup name is required." => Translate("BackupNameRequired"),
            ArgumentException when exception.Message == "The source directory is required." => Translate("SourceDirectoryRequired"),
            ArgumentException when exception.Message == "The target directory is required." => Translate("TargetDirectoryRequired"),
            ArgumentException when exception.Message == "The backup type is invalid." => Translate("BackupTypeInvalid"),
            InvalidOperationException when exception.Message.StartsWith("A backup job named", StringComparison.Ordinal) => Translate("BackupNameAlreadyExists"),
            InvalidOperationException when exception.Message.StartsWith("The backup target directory cannot", StringComparison.Ordinal) => Translate("SourceTargetOverlap"),
            InvalidOperationException when exception.Message.StartsWith("The target directory could not be created:", StringComparison.Ordinal) => Translate("TargetDirectoryCreationFailed"),
            _ => exception.Message
        };
    }

    private bool CanStopJob(BackupJob? job)
    {
        if (job is null)
        {
            return false;
        }

        var state = States.FirstOrDefault(existing => string.Equals(existing.Name, job.Name, StringComparison.OrdinalIgnoreCase));
        return IsStopEligibleState(state?.State);
    }

    private static bool IsStopEligibleState(string? state)
    {
        return string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record SelectionOption(string Value, string Label);

public sealed record BackupTypeOption(BackupType Value, string Label);

public sealed record DashboardJobRow(BackupJob Job, string Name, string Status, string Completion, bool CanStop);

public sealed record JobListRow(
    BackupJob Job,
    string Name,
    string Source,
    string Destination,
    string Type,
    string LastRun,
    string Status,
    string StatusKey,
    bool CanStop);
