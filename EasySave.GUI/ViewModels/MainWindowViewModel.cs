using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private ObservableCollection<BackupJob> jobs = [];

    [ObservableProperty]
    private ObservableCollection<BackupState> states = [];

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

    public MainWindowViewModel()
    {
        AppPaths.EnsureDirectories();

        settingsRepository = new AppSettingsRepository(AppPaths.SettingsFilePath);
        var repository = new BackupJobRepository(AppPaths.JobsFilePath);
        jobService = new BackupJobService(repository);
        stateManager = new StateManager(AppPaths.StateFilePath);
        backupManager = new BackupManager(
            jobService,
            stateManager,
            CreateLogger,
            settingsRepository,
            new ProcessBusinessSoftwareDetector(),
            new CryptoSoftEncryptionService());

        LanguageOptions =
        [
            new SelectionOption("fr", "Français"),
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

    public string Translate(string key)
    {
        return Texts.TryGetValue(key, out var value) ? value : key;
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsIntoViewModelAsync();
        await RefreshJobsAsync();
        await RefreshStatesAsync();
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
    }

    private async Task RefreshJobsAsync()
    {
        Jobs = new ObservableCollection<BackupJob>(await jobService.GetJobsAsync());
    }

    private async Task RefreshStatesAsync()
    {
        States = new ObservableCollection<BackupState>(await stateManager.GetStatesAsync());
    }

    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var settings = BuildSettingsFromViewModel();
            await settingsRepository.SaveAsync(settings);
            await LoadTranslationsAsync(settings.Language);
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

            await jobService.AddJobAsync(job);
            JobName = string.Empty;
            SourceDirectory = string.Empty;
            TargetDirectory = string.Empty;
            SelectedBackupType = BackupType.Complete;
            await RefreshJobsAsync();
            StatusMessage = Translate("JobCreated");
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
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
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
