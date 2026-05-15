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
    private const int SettingsSectionIndex = 6;

    private readonly AppSettingsRepository settingsRepository;
    private readonly BackupJobService jobService;
    private readonly BackupManager backupManager;
    private readonly StateManager stateManager;
    private readonly IBusinessSoftwareDetector businessSoftwareDetector;
    private readonly CancellationTokenSource runtimeRefreshCancellationTokenSource = new();
    private readonly List<BackupJob> selectedJobs = [];
    private Exception? startupException;
    private bool isRefreshingStates;
    private bool suppressJobFormDirtyTracking;
    private bool suppressSettingsDirtyTracking;
    private bool suppressSectionChangeGuard;
    private string appliedJobFormSnapshot = string.Empty;
    private string appliedSettingsSnapshot = string.Empty;
    private int? pendingSectionIndexAfterSettingsConfirmation;
    private int previousSectionIndex;

    [ObservableProperty]
    private Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private ObservableCollection<BackupJob> jobs = [];

    [ObservableProperty]
    private ObservableCollection<BackupState> states = [];

    [ObservableProperty]
    private ObservableCollection<StateListRow> stateRows = [];

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
    private BackupType? selectedBackupType;

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
    private string largeFileThresholdKoText = "1";

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
    private bool isJobFormLeaveConfirmationVisible;

    [ObservableProperty]
    private BackupJob? pendingDeleteJob;

    [ObservableProperty]
    private string pendingDeleteJobName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SelectionOption> languageOptions = [];

    [ObservableProperty]
    private string jobFormValidationMessage = string.Empty;

    [ObservableProperty]
    private bool isJobFormDirty;

    [ObservableProperty]
    private bool isSettingsDirty;

    [ObservableProperty]
    private bool isSettingsLeaveConfirmationVisible;

    [ObservableProperty]
    private bool isMultiRunConfirmationVisible;

    [ObservableProperty]
    private string settingsFeedbackMessage = string.Empty;

    [ObservableProperty]
    private bool isSettingsFeedbackSuccess;

    [ObservableProperty]
    private ObservableCollection<SelectionOption> logFormatOptions = [];

    [ObservableProperty]
    private ObservableCollection<BackupTypeOption> backupTypeOptions = [];

    public MainWindowViewModel()
    {
        try
        {
            AppPaths.EnsureDirectories();
        }
        catch (Exception exception)
        {
            startupException = exception;
        }

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
        ConfirmSaveJobFormAndLeaveCommand = new AsyncRelayCommand(ConfirmSaveJobFormAndLeaveAsync);
        ConfirmDiscardJobFormAndLeaveCommand = new RelayCommand(ConfirmDiscardJobFormAndLeave);
        CancelJobFormLeaveCommand = new RelayCommand(CancelJobFormLeave);
        ConfirmSaveSettingsAndLeaveCommand = new AsyncRelayCommand(ConfirmSaveSettingsAndLeaveAsync);
        ConfirmDiscardSettingsAndLeaveCommand = new AsyncRelayCommand(ConfirmDiscardSettingsAndLeaveAsync);
        CancelSettingsLeaveCommand = new RelayCommand(CancelSettingsLeave);
        PrepareMultiRunCommand = new RelayCommand(PrepareMultiRun);
        ConfirmMultiRunCommand = new AsyncRelayCommand(ConfirmMultiRunAsync);
        CancelMultiRunCommand = new RelayCommand(CancelMultiRun);
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

    public IAsyncRelayCommand ConfirmSaveJobFormAndLeaveCommand { get; }

    public IRelayCommand ConfirmDiscardJobFormAndLeaveCommand { get; }

    public IRelayCommand CancelJobFormLeaveCommand { get; }

    public IAsyncRelayCommand ConfirmSaveSettingsAndLeaveCommand { get; }

    public IAsyncRelayCommand ConfirmDiscardSettingsAndLeaveCommand { get; }

    public IRelayCommand CancelSettingsLeaveCommand { get; }

    public IRelayCommand PrepareMultiRunCommand { get; }

    public IAsyncRelayCommand ConfirmMultiRunCommand { get; }

    public IRelayCommand CancelMultiRunCommand { get; }

    public int TotalJobsCount => Jobs.Count;

    public int ActiveStatesCount => States.Count(state => string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase));

    public int FinishedStatesCount => States.Count(state => string.Equals(state.State, "Finished", StringComparison.OrdinalIgnoreCase));

    public int PausedStatesCount => States.Count(state => string.Equals(state.State, "Paused", StringComparison.OrdinalIgnoreCase));

    public int StoppedStatesCount => States.Count(state => string.Equals(state.State, "Stopped", StringComparison.OrdinalIgnoreCase));

    public int BlockedStatesCount => States.Count(state => string.Equals(state.State, "Blocked", StringComparison.OrdinalIgnoreCase));

    public int ErrorStatesCount => States.Count(state => string.Equals(state.State, "Error", StringComparison.OrdinalIgnoreCase));

    public string ActiveStatesBadgeText => string.Format(CultureInfo.CurrentCulture, Translate("StateBadgeActive"), ActiveStatesCount);

    public string FinishedStatesBadgeText => string.Format(CultureInfo.CurrentCulture, Translate("StateBadgeFinished"), FinishedStatesCount);

    public string ErrorStatesBadgeText => string.Format(CultureInfo.CurrentCulture, Translate("StateBadgeError"), ErrorStatesCount);

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

    public string EncryptionStatusText => string.IsNullOrWhiteSpace(CryptoSoftPath?.Trim())
        ? Translate("SettingsEncryptionDisabled")
        : Translate("SettingsEncryptionEnabled");

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
                return Translate("JobsStatusSuccess");
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

    public bool HasJobFormValidationError => !string.IsNullOrWhiteSpace(JobFormValidationMessage);

    public bool HasStatusMessage =>
        !string.IsNullOrWhiteSpace(StatusMessage) &&
        !string.Equals(StatusMessage, Translate("StatusReady"), StringComparison.Ordinal);

    public bool HasJobsPageStatusMessage =>
        HasStatusMessage &&
        !string.Equals(StatusMessage, Translate("SettingsChangesApplied"), StringComparison.Ordinal) &&
        !string.Equals(StatusMessage, Translate("SettingsNoChangesToApply"), StringComparison.Ordinal) &&
        !string.Equals(StatusMessage, Translate("SettingsChangesDiscarded"), StringComparison.Ordinal);

    public string LogDirectoryPath => AppPaths.LogsDirectory;

    public string StateFilePath => AppPaths.StateFilePath;

    public string JobsConfigPath => AppPaths.JobsFilePath;

    public string SettingsConfigPath => AppPaths.SettingsFilePath;

    public bool CanStopSelectedJob => GetSelectedJobsOrFallback().Any(CanStopJob);

    public bool CanStopAllJobs => Jobs.Any(CanStopJob);

    public bool CanPauseSelectedJob => GetSelectedJobsOrFallback().Any(IsRunningJob);

    public bool CanPauseAllJobs => Jobs.Any(IsRunningJob);

    public bool CanRunSelectedJob => !HasRunningJobs && GetSelectedJobsOrFallback().Count > 0;

    public bool CanRunAllJobs => !HasRunningJobs && Jobs.Count > 0;

    public bool HasRunningJobs => Jobs.Any(IsRunningJob);

    public string DeleteConfirmationText => string.Format(
        CultureInfo.InvariantCulture,
        Translate("DeleteJobConfirmMessage"),
        PendingDeleteJobName);

    public string JobFormLeaveConfirmationText => Translate("JobFormLeaveConfirmMessage");

    public string SettingsLeaveConfirmationText => Translate("SettingsLeaveConfirmMessage");

    public string MultiRunConfirmationText => string.Format(
        CultureInfo.InvariantCulture,
        Translate("RunSelectedJobsConfirmMessage"),
        string.Join(", ", selectedJobs.Select(job => job.Name)));

    public string Translate(string key)
    {
        return Texts.TryGetValue(key, out var value) ? value : key;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await LoadSettingsIntoViewModelAsync();
            await RefreshJobsAsync();
            await RefreshStatesAsync();
            await RefreshLogPreviewAsync();
            StatusMessage = startupException is null
                ? Translate("StatusReady")
                : TranslateUserFacingMessage(startupException);
        }
        catch (Exception exception)
        {
            await EnsureTranslationsForErrorAsync();
            StatusMessage = TranslateUserFacingMessage(exception);
        }

        _ = MonitorRuntimeStateAsync(runtimeRefreshCancellationTokenSource.Token);
    }

    public async Task ShutdownAsync()
    {
        runtimeRefreshCancellationTokenSource.Cancel();

        try
        {
            await backupManager.StopAllJobsAndWaitAsync();
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    public void SetSourceDirectory(string path)
    {
        SourceDirectory = path;
    }

    public void SetTargetDirectory(string path)
    {
        TargetDirectory = path;
    }

    public void ReportError(Exception exception)
    {
        StatusMessage = TranslateUserFacingMessage(exception);
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
        ValidateJobFormInput();
    }

    partial void OnJobNameChanged(string value)
    {
        ValidateJobFormInput();
        UpdateJobFormDirtyState();
    }

    partial void OnSourceDirectoryChanged(string value)
    {
        ValidateJobFormInput();
        UpdateJobFormDirtyState();
    }

    partial void OnTargetDirectoryChanged(string value)
    {
        ValidateJobFormInput();
        UpdateJobFormDirtyState();
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
        OnPropertyChanged(nameof(HasJobsPageStatusMessage));
    }

    partial void OnSelectedBackupTypeChanged(BackupType? value)
    {
        ValidateJobFormInput();
        UpdateJobFormDirtyState();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLanguageLabel));
        OnPropertyChanged(nameof(DashboardFooterCenterText));
        OnPropertyChanged(nameof(JobFormTitle));
        OnPropertyChanged(nameof(JobFormSubtitle));
        OnPropertyChanged(nameof(SaveJobButtonText));
        UpdateSettingsDirtyState();
    }

    partial void OnSelectedLogFormatChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentLogFormatLabel));
        OnPropertyChanged(nameof(DashboardFooterRightText));
        UpdateSettingsDirtyState();
        _ = RefreshLogPreviewAsync();
    }

    partial void OnEncryptedExtensionsTextChanged(string value)
    {
        OnPropertyChanged(nameof(EncryptedExtensionsSummary));
        UpdateSettingsDirtyState();
    }

    partial void OnPriorityExtensionsTextChanged(string value)
    {
        OnPropertyChanged(nameof(PriorityExtensionsSummary));
        UpdateSettingsDirtyState();
    }

    partial void OnLargeFileThresholdKoTextChanged(string value)
    {
        OnPropertyChanged(nameof(LargeFileThresholdSummary));
        UpdateSettingsDirtyState();
    }

    partial void OnJobFormValidationMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasJobFormValidationError));
    }

    partial void OnBusinessSoftwareProcessesTextChanged(string value)
    {
        OnPropertyChanged(nameof(BusinessSoftwareSummary));
        OnPropertyChanged(nameof(BusinessSoftwareAlertText));
        OnPropertyChanged(nameof(DashboardBusinessSoftwareRuntimeText));
        OnPropertyChanged(nameof(IsBusinessSoftwareDetected));
        OnPropertyChanged(nameof(ExecutionBusinessSoftwareAlertText));
        UpdateSettingsDirtyState();
    }

    partial void OnCryptoSoftPathChanged(string value)
    {
        OnPropertyChanged(nameof(EncryptionStatusText));
        UpdateSettingsDirtyState();
    }

    partial void OnCryptoKeyChanged(string value)
    {
        UpdateSettingsDirtyState();
    }

    partial void OnSelectedSectionIndexChanged(int value)
    {
        if (suppressSectionChangeGuard)
        {
            previousSectionIndex = value;
            return;
        }

        if (previousSectionIndex == SettingsSectionIndex &&
            value != SettingsSectionIndex &&
            IsSettingsDirty)
        {
            pendingSectionIndexAfterSettingsConfirmation = value;
            suppressSectionChangeGuard = true;
            SelectedSectionIndex = previousSectionIndex;
            suppressSectionChangeGuard = false;
            IsSettingsLeaveConfirmationVisible = true;
            return;
        }

        previousSectionIndex = value;
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
        suppressSettingsDirtyTracking = true;
        var settings = await settingsRepository.LoadAsync();
        var selectedLanguage = string.IsNullOrWhiteSpace(settings.Language) ? "en" : settings.Language;
        var selectedLogFormat = string.IsNullOrWhiteSpace(settings.LogFormatName) ? "json" : settings.LogFormatName;

        await LoadTranslationsAsync(selectedLanguage);
        RefreshLocalizedOptions();

        SelectedLanguage = selectedLanguage;
        SelectedLogFormat = selectedLogFormat;
        EncryptedExtensionsText = string.Join(";", settings.EncryptedExtensions);
        PriorityExtensionsText = string.Join(";", settings.PriorityExtensions);
        BusinessSoftwareProcessesText = string.Join(";", settings.BusinessSoftwareProcesses);
        LargeFileThresholdKoText = settings.LargeFileThresholdKo.ToString(CultureInfo.InvariantCulture);
        CryptoSoftPath = string.IsNullOrWhiteSpace(settings.CryptoSoftPath)
            ? Path.Combine(AppPaths.BaseDirectory, "CryptoSoft")
            : settings.CryptoSoftPath;
        CryptoKey = settings.CryptoKey;
        appliedSettingsSnapshot = CreateSettingsSnapshot(settings);
        suppressSettingsDirtyTracking = false;
        IsSettingsDirty = false;
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
        try
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
        }
        catch (Exception exception)
        {
            StatusMessage = TranslateUserFacingMessage(exception);
        }

        OnPropertyChanged(nameof(TotalJobsCount));
        OnPropertyChanged(nameof(DashboardQuickStatsPercent));
        OnPropertyChanged(nameof(DashboardQuickStatsText));
        OnPropertyChanged(nameof(JobsStorageUsagePercent));
        OnPropertyChanged(nameof(JobsStorageUsageText));
        OnPropertyChanged(nameof(CanRunSelectedJob));
        OnPropertyChanged(nameof(CanRunAllJobs));
        OnPropertyChanged(nameof(CanPauseSelectedJob));
        OnPropertyChanged(nameof(CanPauseAllJobs));
        OnPropertyChanged(nameof(CanStopSelectedJob));
        OnPropertyChanged(nameof(CanStopAllJobs));
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
            OnPropertyChanged(nameof(ActiveStatesBadgeText));
            OnPropertyChanged(nameof(FinishedStatesBadgeText));
            OnPropertyChanged(nameof(ErrorStatesBadgeText));
            OnPropertyChanged(nameof(GlobalStatusLabel));
            OnPropertyChanged(nameof(DashboardQuickStatsPercent));
            OnPropertyChanged(nameof(DashboardQuickStatsText));
            OnPropertyChanged(nameof(JobsServiceStatusText));
            OnPropertyChanged(nameof(JobsStorageUsagePercent));
            OnPropertyChanged(nameof(JobsStorageUsageText));
            OnPropertyChanged(nameof(DashboardBusinessSoftwareRuntimeText));
            OnPropertyChanged(nameof(IsBusinessSoftwareDetected));
            OnPropertyChanged(nameof(ExecutionBusinessSoftwareAlertText));
            OnPropertyChanged(nameof(CanRunSelectedJob));
            OnPropertyChanged(nameof(CanRunAllJobs));
            OnPropertyChanged(nameof(CanPauseSelectedJob));
            OnPropertyChanged(nameof(CanPauseAllJobs));
            OnPropertyChanged(nameof(CanStopSelectedJob));
            OnPropertyChanged(nameof(CanStopAllJobs));
            NotifySelectionProperties();
            RebuildDashboardRows();
            RebuildJobListRows();
            RebuildStateRows();
            await RefreshLogPreviewAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = TranslateUserFacingMessage(exception);
        }
        finally
        {
            isRefreshingStates = false;
        }
    }

    private async Task EnsureTranslationsForErrorAsync()
    {
        if (Texts.Count > 0)
        {
            return;
        }

        try
        {
            await LoadTranslationsAsync(SelectedLanguage);
        }
        catch
        {
            Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var settings = BuildValidatedSettingsFromViewModel();
            var nextSnapshot = CreateSettingsSnapshot(settings);
            if (string.Equals(nextSnapshot, appliedSettingsSnapshot, StringComparison.Ordinal))
            {
                StatusMessage = Translate("SettingsNoChangesToApply");
                SettingsFeedbackMessage = Translate("SettingsNoChangesToApply");
                IsSettingsFeedbackSuccess = false;
                return;
            }

            await settingsRepository.SaveAsync(settings);
            await LoadTranslationsAsync(settings.Language);
            await RefreshLogPreviewAsync();
            appliedSettingsSnapshot = nextSnapshot;
            IsSettingsDirty = false;
            var feedbackKey = string.IsNullOrWhiteSpace(settings.CryptoSoftPath)
                ? "SettingsEncryptionDisabledMessage"
                : "SettingsChangesApplied";
            StatusMessage = Translate(feedbackKey);
            SettingsFeedbackMessage = Translate(feedbackKey);
            IsSettingsFeedbackSuccess = true;
        });
    }

    private async Task AddJobAsync()
    {
        ValidateJobFormInput(forceRequiredValidation: true);
        if (HasJobFormValidationError)
        {
            StatusMessage = JobFormValidationMessage;
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (IsEditingJob && backupManager.IsJobRunning(EditingOriginalJobName))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    Translate("CannotEditRunningJob"),
                    EditingOriginalJobName));
            }

            var job = new BackupJob
            {
                Name = JobName.Trim(),
                SourceDirectory = SourceDirectory.Trim(),
                TargetDirectory = TargetDirectory.Trim(),
                Type = SelectedBackupType!.Value
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
        await RunBusyAsync(async () =>
        {
            if (HasRunningJobs)
            {
                StatusMessage = Translate("ExecutionAlreadyInProgress");
                return;
            }

            var jobsToRun = GetSelectedJobsOrFallback();
            if (jobsToRun.Count == 0)
            {
                StatusMessage = Translate("SelectJobFirst");
                return;
            }

            await ExecuteJobsAsync(jobsToRun);
            await RefreshStatesAsync();
        });
    }

    private async Task RunAllJobsAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (HasRunningJobs)
            {
                StatusMessage = Translate("ExecutionAlreadyInProgress");
                return;
            }

            if (Jobs.Count == 0)
            {
                StatusMessage = Translate("NoJobsToRun");
                return;
            }

            await ExecuteJobsAsync(Jobs.ToList());
            await RefreshStatesAsync();
        });
    }

    private async Task PauseSelectedJobAsync()
    {
        await RunBusyAsync(async () =>
        {
            var jobsToPause = GetSelectedJobsOrFallback();
            if (jobsToPause.Count == 0)
            {
                StatusMessage = Translate("SelectJobFirst");
                return;
            }

            if (!jobsToPause.Any(IsRunningJob))
            {
                StatusMessage = Translate("NoRunningJobToPause");
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
        });
    }

    private async Task PauseAllJobsAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!Jobs.Any(IsRunningJob))
            {
                StatusMessage = Translate("NoRunningJobToPause");
                return;
            }

            await backupManager.PauseAllJobsAsync();
            StatusMessage = Translate("ExecutionPaused");
            await RefreshStatesAsync();
        });
    }

    private async Task StopSelectedJobAsync()
    {
        await RunBusyAsync(async () =>
        {
            var jobsToStop = GetSelectedJobsOrFallback();
            if (jobsToStop.Count == 0)
            {
                StatusMessage = Translate("SelectJobFirst");
                return;
            }

            if (!jobsToStop.Any(IsRunningJob))
            {
                StatusMessage = Translate("NoRunningJobToStop");
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
        });
    }

    private async Task StopAllJobsAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!Jobs.Any(IsRunningJob))
            {
                StatusMessage = Translate("NoRunningJobToStop");
                return;
            }

            await backupManager.StopAllJobsAsync();
            StatusMessage = Translate("ExecutionStopped");
            await RefreshStatesAsync();
        });
    }

    private async Task RunDashboardJobAsync(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        SelectedJob = job;
        await RunBusyAsync(async () =>
        {
            if (HasRunningJobs)
            {
                StatusMessage = Translate("ExecutionAlreadyInProgress");
                return;
            }

            await ExecuteJobsAsync([job]);
            await RefreshStatesAsync();
        });
    }

    private async Task StopDashboardJobAsync(BackupJob? job)
    {
        if (!CanStopJob(job))
        {
            return;
        }

        SelectedJob = job;
        await RunBusyAsync(async () =>
        {
            await backupManager.StopJobAsync(job!.Name);
            StatusMessage = Translate("ExecutionStopped");
            await RefreshStatesAsync();
        });
    }

    private void RequestDeleteJob(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        if (HasRunningJobs)
        {
            StatusMessage = Translate("CannotDeleteWhileExecutionRunning");
            return;
        }

        if (IsRunningJob(job))
        {
            StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("CannotDeleteRunningJob"), job.Name);
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
            if (IsRunningJob(job))
            {
                StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("CannotDeleteRunningJob"), job.Name);
                CancelDeleteJob();
                return;
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
            LogPreviewText = TranslateUserFacingMessage(exception);
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
        ValidateExtensionList(settings.EncryptedExtensions, "InvalidEncryptedExtensions");
        ValidateExtensionList(settings.PriorityExtensions, "InvalidPriorityExtensions");
        ValidateBusinessSoftwareProcesses(settings.BusinessSoftwareProcesses);

        if (string.IsNullOrWhiteSpace(settings.CryptoSoftPath))
        {
            return settings;
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
            ? Math.Max(1, thresholdKo)
            : 1;
    }

    private int ParseValidatedLargeFileThresholdKo(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException(Translate("LargeFileThresholdRequired"));
        }

        if (!int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var thresholdKo))
        {
            throw new InvalidOperationException(Translate("LargeFileThresholdInvalid"));
        }

        if (thresholdKo < 0)
        {
            throw new InvalidOperationException(Translate("LargeFileThresholdNegative"));
        }

        if (thresholdKo == 0)
        {
            throw new InvalidOperationException(Translate("LargeFileThresholdMinimum"));
        }

        return thresholdKo;
    }

    private void ValidateExtensionList(IEnumerable<string> extensions, string translationKey)
    {
        foreach (var extension in extensions)
        {
            if (!IsValidExtensionToken(extension))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    Translate(translationKey),
                    extension));
            }
        }
    }

    private void ValidateBusinessSoftwareProcesses(IEnumerable<string> processes)
    {
        foreach (var process in processes)
        {
            if (!IsValidBusinessSoftwareProcess(process))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    Translate("InvalidBusinessSoftwareProcess"),
                    process));
            }
        }
    }

    private static bool IsValidExtensionToken(string value)
    {
        var extension = value.Trim();
        if (extension is "*" or "*.*" or ".*")
        {
            return true;
        }

        if (extension.Length == 0 || extension == "." || extension.Contains('*') || extension.Contains('?'))
        {
            return false;
        }

        if (extension.Contains(Path.DirectorySeparatorChar) || extension.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        var extensionName = extension.StartsWith('.') ? extension[1..] : extension;
        return extensionName.Length > 0 &&
               extensionName.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');
    }

    private static bool IsValidBusinessSoftwareProcess(string value)
    {
        var processName = value.Trim();
        if (processName.Length == 0 || processName.Contains('*') || processName.Contains('?'))
        {
            return false;
        }

        if (processName.Any(char.IsWhiteSpace) ||
            processName.Contains(Path.DirectorySeparatorChar) ||
            processName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        return processName.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');
    }

    private static List<string> SplitList(string rawValue)
    {
        return rawValue
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private void ResetJobForm()
    {
        suppressJobFormDirtyTracking = true;
        IsEditingJob = false;
        EditingOriginalJobName = string.Empty;
        JobName = string.Empty;
        SourceDirectory = string.Empty;
        TargetDirectory = string.Empty;
        SelectedBackupType = null;
        JobFormValidationMessage = string.Empty;
        IsJobFormDirty = false;
        appliedJobFormSnapshot = CreateJobFormSnapshot();
        suppressJobFormDirtyTracking = false;
        StatusMessage = Translate("JobFormReset");
    }

    private void PrepareMultiRun()
    {
        if (HasRunningJobs)
        {
            StatusMessage = Translate("ExecutionAlreadyInProgress");
            return;
        }

        if (selectedJobs.Count == 0)
        {
            StatusMessage = Translate("SelectJobsFirst");
            return;
        }

        IsMultiRunConfirmationVisible = true;
        OnPropertyChanged(nameof(MultiRunConfirmationText));
    }

    private async Task ConfirmMultiRunAsync()
    {
        var jobsToRun = GetSelectedJobsOrFallback();
        if (jobsToRun.Count == 0)
        {
            IsMultiRunConfirmationVisible = false;
            StatusMessage = Translate("SelectJobsFirst");
            return;
        }

        IsMultiRunConfirmationVisible = false;
        await RunBusyAsync(async () =>
        {
            await ExecuteJobsAsync(jobsToRun);
            await RefreshStatesAsync();
        });
    }

    private void CancelMultiRun()
    {
        IsMultiRunConfirmationVisible = false;
    }

    private void PrepareCreateJob()
    {
        ResetJobForm();
        SelectedSectionIndex = 1;
        IsJobFormOverlayVisible = true;
        appliedJobFormSnapshot = CreateJobFormSnapshot();
        IsJobFormDirty = false;
    }

    private void EditJob(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        if (HasRunningJobs)
        {
            StatusMessage = Translate("CannotEditWhileExecutionRunning");
            return;
        }

        if (IsRunningJob(job))
        {
            StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("CannotEditRunningJob"), job.Name);
            return;
        }

        suppressJobFormDirtyTracking = true;
        IsEditingJob = true;
        EditingOriginalJobName = job.Name;
        JobName = job.Name;
        SourceDirectory = job.SourceDirectory;
        TargetDirectory = job.TargetDirectory;
        SelectedBackupType = job.Type;
        suppressJobFormDirtyTracking = false;
        appliedJobFormSnapshot = CreateJobFormSnapshot();
        IsJobFormDirty = false;
        SelectedSectionIndex = 1;
        IsJobFormOverlayVisible = true;
        StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("EditJobLoaded"), job.Name);
    }

    private void NavigateToSection(int index)
    {
        if (SelectedSectionIndex == SettingsSectionIndex &&
            index != SettingsSectionIndex &&
            IsSettingsDirty)
        {
            pendingSectionIndexAfterSettingsConfirmation = index;
            IsSettingsLeaveConfirmationVisible = true;
            return;
        }

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
        SelectedSectionIndex = 1;
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

    private async Task ConfirmSaveSettingsAndLeaveAsync()
    {
        await SaveSettingsAsync();
        if (IsSettingsDirty)
        {
            return;
        }

        IsSettingsLeaveConfirmationVisible = false;
        if (pendingSectionIndexAfterSettingsConfirmation.HasValue)
        {
            suppressSectionChangeGuard = true;
            SelectedSectionIndex = pendingSectionIndexAfterSettingsConfirmation.Value;
            suppressSectionChangeGuard = false;
            previousSectionIndex = SelectedSectionIndex;
        }

        pendingSectionIndexAfterSettingsConfirmation = null;
    }

    private async Task ConfirmDiscardSettingsAndLeaveAsync()
    {
        await LoadSettingsIntoViewModelAsync();
        IsSettingsLeaveConfirmationVisible = false;
        if (pendingSectionIndexAfterSettingsConfirmation.HasValue)
        {
            suppressSectionChangeGuard = true;
            SelectedSectionIndex = pendingSectionIndexAfterSettingsConfirmation.Value;
            suppressSectionChangeGuard = false;
            previousSectionIndex = SelectedSectionIndex;
        }

        pendingSectionIndexAfterSettingsConfirmation = null;
        StatusMessage = Translate("SettingsChangesDiscarded");
        SettingsFeedbackMessage = string.Empty;
        IsSettingsFeedbackSuccess = false;
    }

    private void CancelSettingsLeave()
    {
        pendingSectionIndexAfterSettingsConfirmation = null;
        IsSettingsLeaveConfirmationVisible = false;
    }

    private async Task ExecuteJobsAsync(IReadOnlyList<BackupJob> jobsToRun)
    {
        await settingsRepository.SaveAsync(BuildValidatedSettingsFromViewModel());
        var duplicateTargetDirectory = FindDuplicateTargetDirectory(jobsToRun);
        if (!string.IsNullOrWhiteSpace(duplicateTargetDirectory))
        {
            StatusMessage = string.Format(
                CultureInfo.InvariantCulture,
                Translate("DuplicateTargetDirectorySelected"),
                duplicateTargetDirectory);
            return;
        }

        var startedAny = false;
        var resumedAny = false;
        string? firstFailureMessage = null;

        foreach (var job in jobsToRun)
        {
            if (backupManager.IsJobRunning(job.Name))
            {
                resumedAny |= await backupManager.ResumeJobAsync(job.Name);
                startedAny = true;
            }
            else
            {
                var jobIndex = Jobs.IndexOf(job);
                if (jobIndex >= 0)
                {
                    try
                    {
                        await backupManager.StartJobAsync(jobIndex + 1);
                        startedAny = true;
                    }
                    catch (Exception exception)
                    {
                        await backupManager.ReportStartFailureAsync(job, exception);
                        firstFailureMessage ??= TranslateUserFacingMessage(exception);
                    }
                }
            }
        }

        if (!startedAny)
        {
            StatusMessage = firstFailureMessage ?? Translate("NoJobsToRun");
            return;
        }

        StatusMessage = resumedAny && string.IsNullOrWhiteSpace(firstFailureMessage)
            ? Translate("ExecutionResumed")
            : Translate("ExecutionStarted");
    }

    private static string? FindDuplicateTargetDirectory(IEnumerable<BackupJob> jobs)
    {
        return jobs
            .Where(job => !string.IsNullOrWhiteSpace(job.TargetDirectory))
            .GroupBy(job => NormalizePath(job.TargetDirectory))
            .FirstOrDefault(group => group.Count() > 1)
            ?.First()
            .TargetDirectory;
    }

    private void CloseJobFormOverlay()
    {
        if (IsJobFormDirty)
        {
            IsJobFormLeaveConfirmationVisible = true;
            return;
        }

        DismissJobFormOverlay();
    }

    private async Task ConfirmSaveJobFormAndLeaveAsync()
    {
        IsJobFormLeaveConfirmationVisible = false;
        await AddJobAsync();
    }

    private void ConfirmDiscardJobFormAndLeave()
    {
        DismissJobFormOverlay();
    }

    private void CancelJobFormLeave()
    {
        IsJobFormLeaveConfirmationVisible = false;
    }

    private void DismissJobFormOverlay()
    {
        IsJobFormLeaveConfirmationVisible = false;
        ResetJobForm();
        IsJobFormOverlayVisible = false;
        if (SelectedSectionIndex == 2)
        {
            SelectedSectionIndex = 1;
        }

        StatusMessage = string.Empty;
    }

    private void UpdateJobFormDirtyState()
    {
        if (suppressJobFormDirtyTracking)
        {
            return;
        }

        IsJobFormDirty = !string.Equals(CreateJobFormSnapshot(), appliedJobFormSnapshot, StringComparison.Ordinal);
    }

    private string CreateJobFormSnapshot()
    {
        return string.Join(
            "\u001f",
            JobName.Trim(),
            SourceDirectory.Trim(),
            TargetDirectory.Trim(),
            SelectedBackupType?.ToString() ?? string.Empty);
    }

    private void ValidateJobFormInput(bool forceRequiredValidation = false)
    {
        var trimmedName = JobName.Trim();
        var trimmedSource = SourceDirectory.Trim();
        var trimmedTarget = TargetDirectory.Trim();

        if (forceRequiredValidation &&
            string.IsNullOrWhiteSpace(trimmedName) &&
            string.IsNullOrWhiteSpace(trimmedSource) &&
            string.IsNullOrWhiteSpace(trimmedTarget))
        {
            SetJobFormValidationMessage("JobFormAllFieldsRequired");
            return;
        }

        if (forceRequiredValidation || !string.IsNullOrWhiteSpace(trimmedName))
        {
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                SetJobFormValidationMessage("BackupNameRequired");
                return;
            }

            if (!BackupJobService.IsValidJobName(trimmedName))
            {
                SetJobFormValidationMessage("BackupNameInvalidCharacters");
                return;
            }

            if (trimmedName.Length > BackupJobService.MaxBackupJobNameLength)
            {
                SetJobFormValidationMessage("BackupNameTooLong", BackupJobService.MaxBackupJobNameLength);
                return;
            }

            var duplicateExists = Jobs.Any(existing =>
                !string.Equals(existing.Name, EditingOriginalJobName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
            if (duplicateExists)
            {
                SetJobFormValidationMessage("BackupNameAlreadyExists");
                return;
            }
        }

        if (forceRequiredValidation || !string.IsNullOrWhiteSpace(trimmedSource))
        {
            if (string.IsNullOrWhiteSpace(trimmedSource))
            {
                SetJobFormValidationMessage("SourceDirectoryRequired");
                return;
            }
        }

        if (forceRequiredValidation || !string.IsNullOrWhiteSpace(trimmedTarget))
        {
            if (string.IsNullOrWhiteSpace(trimmedTarget))
            {
                SetJobFormValidationMessage("TargetDirectoryRequired");
                return;
            }
        }

        if (forceRequiredValidation && SelectedBackupType is null)
        {
            SetJobFormValidationMessage("BackupTypeRequired");
            return;
        }

        if (!string.IsNullOrWhiteSpace(trimmedSource) && !string.IsNullOrWhiteSpace(trimmedTarget))
        {
            var overlapErrorKey = GetSourceTargetOverlapValidationKey(trimmedSource, trimmedTarget);
            if (!string.IsNullOrWhiteSpace(overlapErrorKey))
            {
                SetJobFormValidationMessage(overlapErrorKey);
                return;
            }
        }

        JobFormValidationMessage = string.Empty;
    }

    private void SetJobFormValidationMessage(string translationKey, params object[] formatArguments)
    {
        var template = Translate(translationKey);
        JobFormValidationMessage = formatArguments.Length == 0
            ? template
            : string.Format(CultureInfo.InvariantCulture, template, formatArguments);
    }

    private static string? GetSourceTargetOverlapValidationKey(string rawSource, string rawTarget)
    {
        try
        {
            var normalizedTargetDirectory = NormalizePath(rawTarget);
            var sourcePaths = SourceSelectionParser.Parse(rawSource);

            foreach (var sourcePath in sourcePaths)
            {
                var normalizedSourcePath = NormalizePath(sourcePath);

                if (File.Exists(sourcePath))
                {
                    var sourceParentDirectory = Path.GetDirectoryName(sourcePath);
                    if (!string.IsNullOrWhiteSpace(sourceParentDirectory) &&
                        string.Equals(NormalizePath(sourceParentDirectory), normalizedTargetDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        return "SourceTargetSameDirectory";
                    }

                    continue;
                }

                if (string.Equals(normalizedSourcePath, normalizedTargetDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return "SourceTargetSameDirectory";
                }

                if (IsSubdirectoryOf(normalizedTargetDirectory, normalizedSourcePath))
                {
                    return "TargetInsideSourceDirectory";
                }

                if (IsSubdirectoryOf(normalizedSourcePath, normalizedTargetDirectory))
                {
                    return "TargetContainsSourceDirectory";
                }
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or IOException)
        {
            return null;
        }

        return null;
    }

    private static bool IsSubdirectoryOf(string path, string potentialParent)
    {
        return path.StartsWith(potentialParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void UpdateSettingsDirtyState()
    {
        if (suppressSettingsDirtyTracking)
        {
            return;
        }

        IsSettingsDirty = !string.Equals(CreateSettingsSnapshot(BuildSettingsFromViewModel()), appliedSettingsSnapshot, StringComparison.Ordinal);
        if (IsSettingsDirty)
        {
            SettingsFeedbackMessage = string.Empty;
            IsSettingsFeedbackSuccess = false;
        }
    }

    private static string CreateSettingsSnapshot(AppSettings settings)
    {
        return JsonSerializer.Serialize(new
        {
            Language = settings.Language,
            LogFormatName = settings.LogFormatName,
            EncryptedExtensions = settings.EncryptedExtensions,
            PriorityExtensions = settings.PriorityExtensions,
            BusinessSoftwareProcesses = settings.BusinessSoftwareProcesses,
            LargeFileThresholdKo = settings.LargeFileThresholdKo,
            CryptoSoftPath = settings.CryptoSoftPath,
            CryptoKey = settings.CryptoKey
        }, JsonOptions);
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
                    CanStopJob(job),
                    selectedJobs.Any(selected => string.Equals(selected.Name, job.Name, StringComparison.OrdinalIgnoreCase)));
            }));

        SyncSelectedJobsWithCurrentJobs();
        OnPropertyChanged(nameof(FilteredJobsCount));
    }

    private void RebuildStateRows()
    {
        StateRows = new ObservableCollection<StateListRow>(
            States.Select(state =>
            {
                var job = Jobs.FirstOrDefault(existing => string.Equals(existing.Name, state.Name, StringComparison.OrdinalIgnoreCase));
                var currentSource = !string.IsNullOrWhiteSpace(state.CurrentSourceFilePath)
                    ? state.CurrentSourceFilePath
                    : job?.SourceDirectory ?? Translate("NoDataPlaceholder");
                var currentDestination = !string.IsNullOrWhiteSpace(state.CurrentDestinationFilePath)
                    ? state.CurrentDestinationFilePath
                    : job?.TargetDirectory ?? Translate("NoDataPlaceholder");

                return new StateListRow(
                    state.Name,
                    TranslateState(state.State),
                    state.Progression,
                    state.RemainingFiles,
                    state.TotalFilesSize,
                    state.RemainingSize,
                    currentSource,
                    currentDestination);
            }));
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
        OnPropertyChanged(nameof(HasJobFormValidationError));
        OnPropertyChanged(nameof(CanRunSelectedJob));
        OnPropertyChanged(nameof(CanRunAllJobs));
        OnPropertyChanged(nameof(CanPauseSelectedJob));
        OnPropertyChanged(nameof(CanPauseAllJobs));
        OnPropertyChanged(nameof(CanStopSelectedJob));
        OnPropertyChanged(nameof(CanStopAllJobs));
        OnPropertyChanged(nameof(DeleteConfirmationText));
        OnPropertyChanged(nameof(SettingsLeaveConfirmationText));
        OnPropertyChanged(nameof(MultiRunConfirmationText));
    }

    private void NotifyAllUiSummaries()
    {
        OnPropertyChanged(nameof(ActiveStatesBadgeText));
        OnPropertyChanged(nameof(FinishedStatesBadgeText));
        OnPropertyChanged(nameof(ErrorStatesBadgeText));
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
        OnPropertyChanged(nameof(HasJobFormValidationError));
        OnPropertyChanged(nameof(CanRunSelectedJob));
        OnPropertyChanged(nameof(CanRunAllJobs));
        OnPropertyChanged(nameof(CanPauseSelectedJob));
        OnPropertyChanged(nameof(CanPauseAllJobs));
        OnPropertyChanged(nameof(CanStopSelectedJob));
        OnPropertyChanged(nameof(CanStopAllJobs));
        OnPropertyChanged(nameof(SettingsLeaveConfirmationText));
        OnPropertyChanged(nameof(MultiRunConfirmationText));
        RebuildDashboardRows();
        RebuildJobListRows();
        RebuildStateRows();
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
        OnPropertyChanged(nameof(CanRunSelectedJob));
        OnPropertyChanged(nameof(CanPauseSelectedJob));
        OnPropertyChanged(nameof(CanStopSelectedJob));
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
            new SelectionOption("fr", "Français"),
            new SelectionOption("en", "English"),
            new SelectionOption("ja", "日本語")
        ];

        LogFormatOptions =
        [
            new SelectionOption("json", Translate("LogFormatJson")),
            new SelectionOption("xml", Translate("LogFormatXml"))
        ];

        BackupTypeOptions =
        [
            new BackupTypeOption(null, Translate("SelectBackupTypeOption")),
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
            DirectoryNotFoundException when exception.Message.StartsWith("Source path does not exist:", StringComparison.Ordinal) => FormatMissingPathMessage("SourcePathDoesNotExistDetailed", exception.Message, "Source path does not exist:"),
            DirectoryNotFoundException when exception.Message.StartsWith("Target directory does not exist:", StringComparison.Ordinal) => FormatMissingPathMessage("TargetDirectoryDoesNotExistDetailed", exception.Message, "Target directory does not exist:"),
            ArgumentException when exception.Message == "The backup form is empty." => Translate("JobFormAllFieldsRequired"),
            ArgumentException when exception.Message == "The backup name is required." => Translate("BackupNameRequired"),
            ArgumentException when exception.Message == "The backup name contains invalid characters." => Translate("BackupNameInvalidCharacters"),
            ArgumentException when exception.Message == "The backup name is too long." => string.Format(CultureInfo.InvariantCulture, Translate("BackupNameTooLong"), BackupJobService.MaxBackupJobNameLength),
            ArgumentException when exception.Message == "The source directory is required." => Translate("SourceDirectoryRequired"),
            ArgumentException when exception.Message == "The target directory is required." => Translate("TargetDirectoryRequired"),
            ArgumentException when exception.Message == "The backup type is invalid." => Translate("BackupTypeInvalid"),
            InvalidOperationException when exception.Message.StartsWith("Backup job not found:", StringComparison.Ordinal) => Translate("BackupJobNotFound"),
            InvalidOperationException when exception.Message.StartsWith("A backup job named", StringComparison.Ordinal) => Translate("BackupNameAlreadyExists"),
            InvalidOperationException when exception.Message == "The backup target directory cannot be the same as the source directory." => Translate("SourceTargetSameDirectory"),
            InvalidOperationException when exception.Message == "The backup target directory cannot be inside the source directory." => Translate("TargetInsideSourceDirectory"),
            InvalidOperationException when exception.Message == "The backup target directory cannot contain the source directory." => Translate("TargetContainsSourceDirectory"),
            InvalidOperationException when exception.Message.StartsWith("The target directory could not be created:", StringComparison.Ordinal) => Translate("TargetDirectoryCreationFailed"),
            InvalidOperationException when exception.Message.StartsWith("Backup jobs file could not be read:", StringComparison.Ordinal) => Translate("JobsFileReadFailed"),
            InvalidOperationException when exception.Message.StartsWith("Backup jobs file could not be saved:", StringComparison.Ordinal) => Translate("JobsFileSaveFailed"),
            InvalidOperationException when exception.Message.StartsWith("Settings file could not be read:", StringComparison.Ordinal) => Translate("SettingsFileReadFailed"),
            InvalidOperationException when exception.Message.StartsWith("Settings file could not be saved:", StringComparison.Ordinal) => Translate("SettingsFileSaveFailed"),
            InvalidOperationException when exception.Message.StartsWith("State file could not be read:", StringComparison.Ordinal) => Translate("StateFileReadFailed"),
            InvalidOperationException when exception.Message.StartsWith("State file could not be saved:", StringComparison.Ordinal) => Translate("StateFileSaveFailed"),
            InvalidOperationException when exception.Message.StartsWith("Application directories could not be created:", StringComparison.Ordinal) => Translate("ApplicationDirectoriesCreateFailed"),
            _ => FormatUnexpectedError(exception)
        };
    }

    private string FormatMissingPathMessage(string translationKey, string rawMessage, string prefix)
    {
        var missingPath = rawMessage[prefix.Length..].Trim();
        var template = Translate(translationKey);
        return string.Equals(template, translationKey, StringComparison.Ordinal)
            ? missingPath
            : string.Format(CultureInfo.InvariantCulture, template, missingPath);
    }

    private string FormatUnexpectedError(Exception exception)
    {
        var template = Translate("UnexpectedError");
        return string.Equals(template, "UnexpectedError", StringComparison.Ordinal)
            ? exception.Message
            : string.Format(CultureInfo.InvariantCulture, template, exception.Message);
    }

    private bool CanStopJob(BackupJob? job)
    {
        return IsRunningJob(job);
    }

    private bool IsRunningJob(BackupJob? job)
    {
        if (job is null)
        {
            return false;
        }

        return backupManager.IsJobRunning(job.Name);
    }
}

public sealed record SelectionOption(string Value, string Label);

public sealed record BackupTypeOption(BackupType? Value, string Label);

public sealed record StateListRow(
    string Name,
    string State,
    double Progression,
    int RemainingFiles,
    long TotalFilesSize,
    long RemainingSize,
    string CurrentSourceFilePath,
    string CurrentDestinationFilePath);

public sealed record DashboardJobRow(BackupJob Job, string Name, string Status, string Completion, bool CanStop);

public sealed class JobListRow
{
    public JobListRow(
        BackupJob job,
        string name,
        string source,
        string destination,
        string type,
        string lastRun,
        string status,
        string statusKey,
        bool canStop,
        bool isMarked)
    {
        Job = job;
        Name = name;
        Source = source;
        Destination = destination;
        Type = type;
        LastRun = lastRun;
        Status = status;
        StatusKey = statusKey;
        CanStop = canStop;
        IsMarked = isMarked;
    }

    public BackupJob Job { get; }

    public string Name { get; }

    public string Source { get; }

    public string Destination { get; }

    public string Type { get; }

    public string LastRun { get; }

    public string Status { get; }

    public string StatusKey { get; }

    public bool CanStop { get; }

    public bool IsMarked { get; set; }
}
