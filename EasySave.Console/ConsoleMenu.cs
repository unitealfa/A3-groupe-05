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
            SourceDirectory = AskDirectory("SourceDirectory", requireExisting: true),
            TargetDirectory = AskDirectory("TargetDirectory", requireExisting: false),
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

    private string AskDirectory(string key, bool requireExisting)
    {
        var options = BuildDirectoryOptions(requireExisting);

        while (true)
        {
            System.Console.WriteLine(languageSelector.Text(key));

            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                System.Console.WriteLine($"{index + 1} - {languageSelector.Text(option.LabelKey)}: {option.Path}");
            }

            var customPathChoice = options.Count + 1;
            System.Console.WriteLine($"{customPathChoice} - {languageSelector.Text("CustomPathOption")}");
            System.Console.Write($"{languageSelector.Text("DirectoryChoicePrompt")} ");
            var choice = System.Console.ReadLine();

            if (int.TryParse(choice, out var selectedIndex))
            {
                if (selectedIndex == customPathChoice)
                {
                    return AskRequired(key);
                }

                if (selectedIndex >= 1 && selectedIndex <= options.Count)
                {
                    return BrowseDirectory(options[selectedIndex - 1].Path, key, requireExisting);
                }
            }

            System.Console.WriteLine(languageSelector.Text("InvalidChoice"));
        }
    }

    private string BrowseDirectory(string startPath, string key, bool requireExisting)
    {
        var currentPath = startPath;

        while (true)
        {
            System.Console.WriteLine();
            System.Console.WriteLine($"{languageSelector.Text(key)} {currentPath}");
            System.Console.WriteLine($"1 - {languageSelector.Text("SelectCurrentDirectoryOption")}");

            var subDirectories = GetSubDirectories(currentPath);
            for (var index = 0; index < subDirectories.Count; index++)
            {
                System.Console.WriteLine($"{index + 2} - {languageSelector.Text("OpenDirectoryOption")}: {Path.GetFileName(subDirectories[index])}");
            }

            var goUpChoice = subDirectories.Count + 2;
            System.Console.WriteLine($"{goUpChoice} - {languageSelector.Text("GoUpDirectoryOption")}");

            var customPathChoice = subDirectories.Count + 3;
            System.Console.WriteLine($"{customPathChoice} - {languageSelector.Text("CustomPathOption")}");

            System.Console.Write($"{languageSelector.Text("DirectoryChoicePrompt")} ");
            var choice = System.Console.ReadLine();

            if (!int.TryParse(choice, out var selectedIndex))
            {
                System.Console.WriteLine(languageSelector.Text("InvalidChoice"));
                continue;
            }

            if (selectedIndex == 1)
            {
                if (!requireExisting || Directory.Exists(currentPath))
                {
                    return currentPath;
                }

                System.Console.WriteLine(languageSelector.Text("SourceDirectoryDoesNotExist"));
                continue;
            }

            if (selectedIndex >= 2 && selectedIndex < goUpChoice)
            {
                currentPath = subDirectories[selectedIndex - 2];
                continue;
            }

            if (selectedIndex == goUpChoice)
            {
                var parent = Directory.GetParent(currentPath)?.FullName;
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    currentPath = parent;
                }

                continue;
            }

            if (selectedIndex == customPathChoice)
            {
                return AskRequired(key);
            }

            System.Console.WriteLine(languageSelector.Text("InvalidChoice"));
        }
    }

    private static List<DirectoryOption> BuildDirectoryOptions(bool requireExisting)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var currentDirectory = Directory.GetCurrentDirectory();
        var parentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory;

        var options = new[]
        {
            new DirectoryOption("HomeDirectory", homeDirectory),
            new DirectoryOption("DesktopDirectory", GetDefaultChildDirectory(homeDirectory, Environment.SpecialFolder.DesktopDirectory, "Desktop")),
            new DirectoryOption("DocumentsDirectory", GetDefaultChildDirectory(homeDirectory, Environment.SpecialFolder.MyDocuments, "Documents")),
            new DirectoryOption("DownloadsDirectory", Path.Combine(homeDirectory, "Downloads")),
            new DirectoryOption("CurrentProjectDirectory", currentDirectory)
        };

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filteredOptions = new List<DirectoryOption>();

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Path))
            {
                continue;
            }

            if (requireExisting && !Directory.Exists(option.Path))
            {
                continue;
            }

            if (seenPaths.Add(option.Path))
            {
                filteredOptions.Add(option);
            }
        }

        if (!requireExisting && seenPaths.Add(parentDirectory))
        {
            filteredOptions.Add(new DirectoryOption("ParentDirectory", parentDirectory));
        }

        return filteredOptions;
    }

    private static string GetDefaultChildDirectory(string homeDirectory, Environment.SpecialFolder folder, string fallbackName)
    {
        var path = Environment.GetFolderPath(folder);
        return string.IsNullOrWhiteSpace(path) ? Path.Combine(homeDirectory, fallbackName) : path;
    }

    private static List<string> GetSubDirectories(string path)
    {
        try
        {
            return Directory
                .GetDirectories(path)
                .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return [];
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

    private sealed record DirectoryOption(string LabelKey, string Path);
}
