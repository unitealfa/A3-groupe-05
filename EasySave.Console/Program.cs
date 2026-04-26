using EasySave.Console;
using EasySave.Core.Configuration;
using EasySave.Core.Services;

AppPaths.EnsureDirectories();

var stateManager = new StateManager(AppPaths.StateFilePath);
var repository = new BackupJobRepository(AppPaths.JobsFilePath);
var jobService = new BackupJobService(repository);
var backupManager = new BackupManager(jobService, stateManager, AppPaths.LogsDirectory);
var languageSelector = new LanguageSelector(AppPaths.SettingsFilePath);
await languageSelector.InitializeAsync();

if (args.Length > 0)
{
    var parser = new CliArgumentParser();
    var jobs = await jobService.GetJobsAsync();
    var parseResult = parser.Parse(args[0], jobs.Count, BackupJobService.MaxJobs, languageSelector.Text);

    if (!parseResult.IsSuccess)
    {
        Console.Error.WriteLine(parseResult.ErrorMessage);
        return 1;
    }

    await backupManager.ExecuteJobsAsync(parseResult.JobIndexes);
    return 0;
}

var menu = new ConsoleMenu(languageSelector, jobService, backupManager);
await menu.RunAsync();
return 0;
