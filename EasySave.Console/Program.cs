using EasySave.Console;
using EasyLog;
using EasySave.Core.Configuration;
using EasySave.Core.Services;

AppPaths.EnsureDirectories();

var stateManager = new StateManager(AppPaths.StateFilePath);
var repository = new BackupJobRepository(AppPaths.JobsFilePath);
var settingsRepository = new AppSettingsRepository(AppPaths.SettingsFilePath);
var jobService = new BackupJobService(repository);
var languageSelector = new LanguageSelector(AppPaths.SettingsFilePath);
await languageSelector.InitializeAsync();

if (args.Length > 0)
{
    var cliBackupManager = new BackupManager(
        jobService,
        stateManager,
        CreateLogger,
        settingsRepository,
        new ProcessBusinessSoftwareDetector(),
        new CryptoSoftEncryptionService(),
        new FileSystemFileTransferService());
    var parser = new CliArgumentParser();
    var jobs = await jobService.GetJobsAsync();
    var parseResult = parser.Parse(args[0], jobs.Count, jobs.Count, languageSelector.Text);

    if (!parseResult.IsSuccess)
    {
        Console.Error.WriteLine(parseResult.ErrorMessage);
        return 1;
    }

    await cliBackupManager.ExecuteJobsAsync(parseResult.JobIndexes);
    return 0;
}

await languageSelector.SelectLanguageAsync();
await languageSelector.SelectLogFormatAsync();

var backupManager = new BackupManager(
    jobService,
    stateManager,
    CreateLogger,
    settingsRepository,
    new ProcessBusinessSoftwareDetector(),
    new CryptoSoftEncryptionService(),
    new FileSystemFileTransferService());
var menu = new ConsoleMenu(languageSelector, jobService, backupManager);
await menu.RunAsync();
return 0;

ILoggerService CreateLogger(EasySave.Core.Models.AppSettings settings)
{
    return settings.LogFormat switch
    {
        LogFormat.Json => new JsonLoggerService(AppPaths.LogsDirectory),
        LogFormat.Xml => new XmlLoggerService(AppPaths.LogsDirectory),
        _ => throw new ArgumentOutOfRangeException(nameof(settings.LogFormat), "Unsupported log format.")
    };
}
