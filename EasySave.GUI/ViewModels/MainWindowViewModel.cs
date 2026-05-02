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
    private string encryptedExtensionsText = ".txt;.zip";

    [ObservableProperty]
    private string businessSoftwareProcessesText = "calc";

    [ObservableProperty]
    private string cryptoSoftPath = Path.Combine(Directory.GetCurrentDirectory(), "CryptoSoft");

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
            new CryptoSoftEncryptionService());

        LanguageOptions =
        [
            new SelectionOption("fr", "Francais"),
            new SelectionOption("en", "English")
        ];

        LogFormatOptions =
        [
            new SelectionOption("json", "JSON"),
            new SelectionOption("xml", "XML")
        ];

        BackupTypeOptions =
        [
            new BackupTypeOption(BackupType.Complete, "Complete"),
            new BackupTypeOption(BackupType.Differential, "Differential")
        ];

        RefreshJobsCommand = new AsyncRelayCommand(RefreshJobsAsync);
        RefreshStatesCommand = new AsyncRelayCommand(RefreshStatesAsync);
        AddJobCommand = new AsyncRelayCommand(AddJobAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        RunSelectedJobCommand = new AsyncRelayCommand(RunSelectedJobAsync);
        RunAllJobsCommand = new AsyncRelayCommand(RunAllJobsAsync);
        ResetJobFormCommand = new RelayCommand(ResetJobForm);
        NavigateToSectionCommand = new RelayCommand<int>(NavigateToSection);
        OpenDashboardJobCommand = new RelayCommand<BackupJob?>(OpenDashboardJob);
        RunDashboardJobCommand = new AsyncRelayCommand<BackupJob?>(RunDashboardJobAsync);
        PrepareCreateJobCommand = new RelayCommand(PrepareCreateJob);
        EditJobCommand = new RelayCommand<BackupJob?>(EditJob);
    }

    public IReadOnlyList<SelectionOption> LanguageOptions { get; }

    public IReadOnlyList<SelectionOption> LogFormatOptions { get; }

    public IReadOnlyList<BackupTypeOption> BackupTypeOptions { get; }

    public IAsyncRelayCommand RefreshJobsCommand { get; }

    public IAsyncRelayCommand RefreshStatesCommand { get; }

    public IAsyncRelayCommand AddJobCommand { get; }

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand RunSelectedJobCommand { get; }

    public IAsyncRelayCommand RunAllJobsCommand { get; }

    public IRelayCommand ResetJobFormCommand { get; }

    public IRelayCommand<int> NavigateToSectionCommand { get; }

    public IRelayCommand<BackupJob?> OpenDashboardJobCommand { get; }

    public IAsyncRelayCommand<BackupJob?> RunDashboardJobCommand { get; }

    public IRelayCommand PrepareCreateJobCommand { get; }

    public IRelayCommand<BackupJob?> EditJobCommand { get; }

    public int TotalJobsCount => Jobs.Count;

    public int ActiveStatesCount => States.Count(state => string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase));

    public int FinishedStatesCount => States.Count(state => string.Equals(state.State, "Finished", StringComparison.OrdinalIgnoreCase));

    public int BlockedStatesCount => States.Count(state => string.Equals(state.State, "Blocked", StringComparison.OrdinalIgnoreCase));

    public int ErrorStatesCount => States.Count(state => string.Equals(state.State, "Error", StringComparison.OrdinalIgnoreCase));

    public string SelectedJobName => SelectedJob?.Name ?? Translate("NoJobSelectedValue");

    public string SelectedJobSource => SelectedJob?.SourceDirectory ?? Translate("NoDataPlaceholder");

    public string SelectedJobTarget => SelectedJob?.TargetDirectory ?? Translate("NoDataPlaceholder");

    public string SelectedJobTypeLabel => SelectedJob?.Type.ToString() ?? Translate("NoDataPlaceholder");

    public string CurrentLanguageLabel => FindLabel(LanguageOptions, SelectedLanguage);

    public string CurrentLogFormatLabel => FindLabel(LogFormatOptions, SelectedLogFormat);

    public string EncryptedExtensionsSummary => string.IsNullOrWhiteSpace(EncryptedExtensionsText)
        ? Translate("NoExtensionsConfigured")
        : EncryptedExtensionsText;

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

    public string SelectedJobStateLabel => GetRelevantState()?.State ?? Translate("StateNotAvailable");

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
        : Translate("NoPauseMessage");

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
        SelectedJob is null ? 0 : 1);

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
    }

    public void SetSourceDirectory(string path)
    {
        SourceDirectory = path;
    }

    public void SetTargetDirectory(string path)
    {
        TargetDirectory = path;
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
        BusinessSoftwareProcessesText = string.Join(";", settings.BusinessSoftwareProcesses);
        CryptoSoftPath = string.IsNullOrWhiteSpace(settings.CryptoSoftPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "CryptoSoft")
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

        NotifyAllUiSummaries();
    }

    private async Task RefreshJobsAsync()
    {
        Jobs = new ObservableCollection<BackupJob>(await jobService.GetJobsAsync());

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
        States = new ObservableCollection<BackupState>(await stateManager.GetStatesAsync());
        OnPropertyChanged(nameof(ActiveStatesCount));
        OnPropertyChanged(nameof(FinishedStatesCount));
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

    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var settings = BuildSettingsFromViewModel();
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
            await RefreshJobsAsync();
        });
    }

    private async Task RunSelectedJobAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (SelectedJob is null)
            {
                StatusMessage = Translate("SelectJobFirst");
                return;
            }

            await settingsRepository.SaveAsync(BuildSettingsFromViewModel());
            var jobIndex = Jobs.IndexOf(SelectedJob) + 1;
            await backupManager.ExecuteJobAsync(jobIndex);
            await RefreshStatesAsync();
            StatusMessage = Translate("BackupFinished");
        });
    }

    private async Task RunAllJobsAsync()
    {
        await RunBusyAsync(async () =>
        {
            await settingsRepository.SaveAsync(BuildSettingsFromViewModel());
            await backupManager.ExecuteAllJobsAsync();
            await RefreshStatesAsync();
            StatusMessage = Translate("BackupFinished");
        });
    }

    private async Task RunDashboardJobAsync(BackupJob? job)
    {
        if (job is null)
        {
            return;
        }

        SelectedJob = job;
        await RunSelectedJobAsync();
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
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
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
            BusinessSoftwareProcesses = SplitList(BusinessSoftwareProcessesText),
            CryptoSoftPath = CryptoSoftPath.Trim(),
            CryptoKey = CryptoKey.Trim()
        };
    }

    private static List<string> SplitList(string rawValue)
    {
        return rawValue
            .Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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
        SelectedSectionIndex = 2;
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
        SelectedSectionIndex = 2;
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

        SelectedJob = job;
        SelectedSectionIndex = 3;
        StatusMessage = string.Format(CultureInfo.InvariantCulture, Translate("DashboardJobOpened"), job.Name);
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
                var status = state?.State ?? Translate("DashboardStatusInactive");
                var completion = state is null
                    ? "--"
                    : $"{state.Progression.ToString("0.##", CultureInfo.InvariantCulture)}%";

                return new DashboardJobRow(job, job.Name, status, completion);
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
                    job.Type.ToString(),
                    lastRun,
                    Translate(statusKey),
                    statusKey);
            }));

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

    private string FormatSize(long size)
    {
        if (size < 1024)
        {
            return $"{size} B";
        }

        var units = new[] { "KB", "MB", "GB", "TB" };
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
}

public sealed record SelectionOption(string Value, string Label);

public sealed record BackupTypeOption(BackupType Value, string Label);

public sealed record DashboardJobRow(BackupJob Job, string Name, string Status, string Completion);

public sealed record JobListRow(
    BackupJob Job,
    string Name,
    string Source,
    string Destination,
    string Type,
    string LastRun,
    string Status,
    string StatusKey);
