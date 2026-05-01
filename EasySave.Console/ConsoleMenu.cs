using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Console;

public sealed class ConsoleMenu
{
    private readonly LanguageSelector languageSelector;
    private readonly BackupJobService jobService;
    private readonly BackupManager backupManager;

    public ConsoleMenu(LanguageSelector languageSelector, BackupJobService jobService, BackupManager backupManager)
    {
        this.languageSelector = languageSelector;
        this.jobService = jobService;
        this.backupManager = backupManager;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var shouldContinue = true;
        while (shouldContinue)
        {
            PrintMenu();
            var choice = System.Console.ReadLine();

            try
            {
                shouldContinue = await HandleChoiceAsync(choice, cancellationToken);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
            {
                System.Console.WriteLine(TranslateExceptionMessage(exception));
            }
        }
    }

    private void PrintMenu()
    {
        System.Console.WriteLine();
        System.Console.WriteLine(languageSelector.Text("AppTitle"));
        System.Console.WriteLine($"1 - {languageSelector.Text("CreateJob")}");
        System.Console.WriteLine($"2 - {languageSelector.Text("ListJobs")}");
        System.Console.WriteLine($"3 - {languageSelector.Text("RunJob")}");
        System.Console.WriteLine($"4 - {languageSelector.Text("RunAllJobs")}");
        System.Console.WriteLine($"5 - {languageSelector.Text("Quit")}");
        System.Console.Write("> ");
    }

    private async Task<bool> HandleChoiceAsync(string? choice, CancellationToken cancellationToken)
    {
        switch (choice)
        {
            case "1":
                await CreateJobAsync(cancellationToken);
                return true;
            case "2":
                await ListJobsAsync(cancellationToken);
                return true;
            case "3":
                await RunJobAsync(cancellationToken);
                return true;
            case "4":
                await backupManager.ExecuteAllJobsAsync(cancellationToken);
                System.Console.WriteLine(languageSelector.Text("BackupFinished"));
                return true;
            case "5":
                return false;
            default:
                System.Console.WriteLine(languageSelector.Text("InvalidChoice"));
                return true;
        }
    }

    private async Task CreateJobAsync(CancellationToken cancellationToken)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        if (jobs.Count >= BackupJobService.MaxJobs)
        {
            System.Console.WriteLine(languageSelector.Text("MaxJobsReached"));
            return;
        }

        var job = new BackupJob
        {
            Name = AskRequired("JobName"),
            SourceDirectory = AskRequired("SourceDirectory"),
            TargetDirectory = AskRequired("TargetDirectory"),
            Type = AskBackupType()
        };

        await jobService.AddJobAsync(job, cancellationToken);
        System.Console.WriteLine(languageSelector.Text("JobCreated"));
    }

    private async Task ListJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = await jobService.GetJobsAsync(cancellationToken);
        if (jobs.Count == 0)
        {
            System.Console.WriteLine(languageSelector.Text("NoJobs"));
            return;
        }

        for (var index = 0; index < jobs.Count; index++)
        {
            var job = jobs[index];
            System.Console.WriteLine($"{index + 1}. {job.Name} | {job.Type} | {job.SourceDirectory} -> {job.TargetDirectory}");
        }
    }

    private async Task RunJobAsync(CancellationToken cancellationToken)
    {
        await ListJobsAsync(cancellationToken);
        System.Console.Write($"{languageSelector.Text("JobIndex")} ");
        if (!int.TryParse(System.Console.ReadLine(), out var jobIndex))
        {
            System.Console.WriteLine(languageSelector.Text("InvalidChoice"));
            return;
        }

        await backupManager.ExecuteJobAsync(jobIndex, cancellationToken);
        System.Console.WriteLine(languageSelector.Text("BackupFinished"));
    }

    private string AskRequired(string key)
    {
        while (true)
        {
            System.Console.Write($"{languageSelector.Text(key)} ");
            var value = System.Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            System.Console.WriteLine(languageSelector.Text("RequiredValue"));
        }
    }

    private BackupType AskBackupType()
    {
        while (true)
        {
            System.Console.Write($"{languageSelector.Text("BackupType")} ");
            var value = System.Console.ReadLine();
            if (value == "1")
            {
                return BackupType.Complete;
            }

            if (value == "2")
            {
                return BackupType.Differential;
            }

            System.Console.WriteLine(languageSelector.Text("InvalidChoice"));
        }
    }

    private string TranslateExceptionMessage(Exception exception)
    {
        return exception switch
        {
            ArgumentOutOfRangeException => languageSelector.Text("BackupJobIndexOutOfRange"),
            DirectoryNotFoundException when exception.Message.StartsWith("Source directory does not exist:", StringComparison.Ordinal) => languageSelector.Text("SourceDirectoryDoesNotExist"),
            ArgumentException when exception.Message == "The backup name is required." => languageSelector.Text("BackupNameRequired"),
            ArgumentException when exception.Message == "The source directory is required." => languageSelector.Text("SourceDirectoryRequired"),
            ArgumentException when exception.Message == "The target directory is required." => languageSelector.Text("TargetDirectoryRequired"),
            ArgumentException when exception.Message == "The backup type is invalid." => languageSelector.Text("BackupTypeInvalid"),
            InvalidOperationException when exception.Message == "The maximum number of backup jobs is five." => languageSelector.Text("MaxJobsReached"),
            InvalidOperationException when exception.Message.StartsWith("The target directory could not be created:", StringComparison.Ordinal) => languageSelector.Text("TargetDirectoryCreationFailed"),
            _ => exception.Message
        };
    }
}
