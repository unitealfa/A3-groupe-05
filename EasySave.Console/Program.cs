using EasySave.Console;
using EasyLog;
using EasySave.Core.Configuration;
using EasySave.Core.Services;

try
{
    return await RunAsync(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine(BuildFatalMessage(exception));
    return 1;
}

async Task<int> RunAsync(string[] arguments)
{
    AppPaths.EnsureDirectories();

    var stateManager = new StateManager(AppPaths.StateFilePath);
    var repository = new BackupJobRepository(AppPaths.JobsFilePath);
    var settingsRepository = new AppSettingsRepository(AppPaths.SettingsFilePath);
    var jobService = new BackupJobService(repository);
    var languageSelector = new LanguageSelector(AppPaths.SettingsFilePath);
    await languageSelector.InitializeAsync();

    if (arguments.Length > 0)
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
        var parseResult = parser.Parse(arguments[0], jobs.Count, jobs.Count, languageSelector.Text);

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
}

ILoggerService CreateLogger(EasySave.Core.Models.AppSettings settings)
{
    return settings.LogFormat switch
    {
        LogFormat.Json => new JsonLoggerService(AppPaths.LogsDirectory),
        LogFormat.Xml => new XmlLoggerService(AppPaths.LogsDirectory),
        _ => throw new ArgumentOutOfRangeException(nameof(settings.LogFormat), "Unsupported log format.")
    };
}

static string BuildFatalMessage(Exception exception)
{
    return exception switch
    {
        InvalidOperationException when exception.Message.StartsWith("Backup jobs file could not be read:", StringComparison.Ordinal) =>
            "Impossible de lire le fichier des travaux de sauvegarde. Vérifiez qu'il n'est pas corrompu et que vous avez les droits d'accès.",
        InvalidOperationException when exception.Message.StartsWith("Backup jobs file could not be saved:", StringComparison.Ordinal) =>
            "Impossible d'enregistrer le fichier des travaux de sauvegarde. Vérifiez les droits d'accès au dossier de configuration.",
        InvalidOperationException when exception.Message.StartsWith("Settings file could not be read:", StringComparison.Ordinal) =>
            "Impossible de lire le fichier de paramètres. Vérifiez qu'il n'est pas corrompu et que vous avez les droits d'accès.",
        InvalidOperationException when exception.Message.StartsWith("Settings file could not be saved:", StringComparison.Ordinal) =>
            "Impossible d'enregistrer le fichier de paramètres. Vérifiez les droits d'accès au dossier de configuration.",
        InvalidOperationException when exception.Message.StartsWith("State file could not be read:", StringComparison.Ordinal) =>
            "Impossible de lire le fichier d'état. Vérifiez qu'il n'est pas corrompu et que vous avez les droits d'accès.",
        InvalidOperationException when exception.Message.StartsWith("State file could not be saved:", StringComparison.Ordinal) =>
            "Impossible d'enregistrer le fichier d'état. Vérifiez les droits d'accès au dossier d'état.",
        InvalidOperationException when exception.Message.StartsWith("Application directories could not be created:", StringComparison.Ordinal) =>
            "Impossible de créer les dossiers de l'application. Vérifiez les droits d'accès au dossier d'installation.",
        _ => $"Erreur inattendue : {exception.Message}"
    };
}
