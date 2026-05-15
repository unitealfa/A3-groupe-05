using EasySave.Core.Configuration;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class BackupJobService
{
    public const int MaxBackupJobNameLength = 100;

    private const string BackupFormEmptyMessage = "The backup form is empty.";
    private const string BackupNameRequiredMessage = "The backup name is required.";
    private const string BackupNameInvalidCharactersMessage = "The backup name contains invalid characters.";
    private const string BackupNameTooLongMessage = "The backup name is too long.";
    private const string SourceDirectoryRequiredMessage = "The source directory is required.";
    private const string TargetDirectoryRequiredMessage = "The target directory is required.";
    private const string BackupTypeInvalidMessage = "The backup type is invalid.";
    private const string SourcePathDoesNotExistMessage = "Source path does not exist: ";
    private const string TargetDirectoryDoesNotExistMessage = "Target directory does not exist: ";
    private const string SourceTargetSameDirectoryMessage = "The backup target directory cannot be the same as the source directory.";
    private const string TargetInsideSourceDirectoryMessage = "The backup target directory cannot be inside the source directory.";
    private const string TargetContainsSourceDirectoryMessage = "The backup target directory cannot contain the source directory.";
    private readonly BackupJobRepository repository;
    private static readonly char[] InvalidJobNameCharacters = Path.GetInvalidFileNameChars();

    public BackupJobService(BackupJobRepository repository)
    {
        this.repository = repository;
    }

    public Task<IReadOnlyList<BackupJob>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetAllAsync(cancellationToken);
    }

    public async Task AddJobAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        ValidateJob(job);

        var jobs = (await repository.GetAllAsync(cancellationToken)).ToList();
        EnsureJobNameIsUnique(jobs, job.Name);
        jobs.Add(job);
        await repository.SaveAllAsync(jobs, cancellationToken);
    }

    public async Task UpdateJobAsync(string originalName, BackupJob job, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            throw new ArgumentException("The original backup name is required.", nameof(originalName));
        }

        ValidateJob(job);

        var jobs = (await repository.GetAllAsync(cancellationToken)).ToList();
        var index = jobs.FindIndex(existing => string.Equals(existing.Name, originalName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException($"Backup job not found: {originalName}");
        }

        EnsureJobNameIsUnique(jobs, job.Name, originalName);
        jobs[index] = job;
        await repository.SaveAllAsync(jobs, cancellationToken);
    }

    public async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var jobs = (await repository.GetAllAsync(cancellationToken)).ToList();
        var removedCount = jobs.RemoveAll(existing => string.Equals(existing.Name, jobName, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            throw new InvalidOperationException($"Backup job not found: {jobName}");
        }

        await repository.SaveAllAsync(jobs, cancellationToken);
    }

    public static void ValidateJob(BackupJob job)
    {
        var sourcePaths = ValidateCommonJobFields(job);

        try
        {
            Directory.CreateDirectory(job.TargetDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"The target directory could not be created: {job.TargetDirectory}", exception);
        }

        EnsureSourceAndTargetDoNotOverlap(sourcePaths, job.TargetDirectory);
    }

    public static void ValidateJobForExecution(BackupJob job)
    {
        var sourcePaths = ValidateCommonJobFields(job);

        if (!Directory.Exists(job.TargetDirectory))
        {
            throw new DirectoryNotFoundException(TargetDirectoryDoesNotExistMessage + job.TargetDirectory);
        }

        EnsureSourceAndTargetDoNotOverlap(sourcePaths, job.TargetDirectory);
    }

    private static IReadOnlyList<string> ValidateCommonJobFields(BackupJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var trimmedName = job.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            if (string.IsNullOrWhiteSpace(job.SourceDirectory) && string.IsNullOrWhiteSpace(job.TargetDirectory))
            {
                throw new ArgumentException(BackupFormEmptyMessage, nameof(job));
            }

            throw new ArgumentException(BackupNameRequiredMessage, nameof(job));
        }

        if (!IsValidJobName(trimmedName))
        {
            throw new ArgumentException(BackupNameInvalidCharactersMessage, nameof(job));
        }

        if (trimmedName.Length > MaxBackupJobNameLength)
        {
            throw new ArgumentException(BackupNameTooLongMessage, nameof(job));
        }

        if (string.IsNullOrWhiteSpace(job.SourceDirectory))
        {
            throw new ArgumentException(SourceDirectoryRequiredMessage, nameof(job));
        }

        var sourcePaths = SourceSelectionParser.Parse(job.SourceDirectory);
        if (sourcePaths.Count == 0)
        {
            throw new ArgumentException(SourceDirectoryRequiredMessage, nameof(job));
        }

        var missingSourcePath = sourcePaths.FirstOrDefault(path => !SourceSelectionParser.IsExistingSource(path));
        if (!string.IsNullOrWhiteSpace(missingSourcePath))
        {
            throw new DirectoryNotFoundException(SourcePathDoesNotExistMessage + missingSourcePath);
        }

        if (string.IsNullOrWhiteSpace(job.TargetDirectory))
        {
            throw new ArgumentException(TargetDirectoryRequiredMessage, nameof(job));
        }

        if (!Enum.IsDefined(job.Type))
        {
            throw new ArgumentException(BackupTypeInvalidMessage, nameof(job));
        }

        return sourcePaths;
    }

    public static bool IsValidJobName(string? jobName)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            return false;
        }

        return jobName.Trim().All(character =>
            !char.IsControl(character) &&
            !InvalidJobNameCharacters.Contains(character));
    }

    private static void EnsureJobNameIsUnique(IEnumerable<BackupJob> jobs, string jobName, string? originalName = null)
    {
        var duplicateExists = jobs.Any(existing =>
            !string.Equals(existing.Name, originalName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Name, jobName, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            throw new InvalidOperationException($"A backup job named '{jobName}' already exists.");
        }
    }

    private static void EnsureSourceAndTargetDoNotOverlap(IEnumerable<string> sourcePaths, string targetDirectory)
    {
        var normalizedTargetDirectory = NormalizePath(targetDirectory);

        foreach (var sourcePath in sourcePaths)
        {
            var normalizedSourcePath = NormalizePath(sourcePath);

            if (File.Exists(sourcePath))
            {
                var sourceParentDirectory = NormalizePath(Path.GetDirectoryName(sourcePath)!);
                if (string.Equals(sourceParentDirectory, normalizedTargetDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(SourceTargetSameDirectoryMessage);
                }

                continue;
            }

            if (string.Equals(normalizedSourcePath, normalizedTargetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(SourceTargetSameDirectoryMessage);
            }

            if (IsSubdirectoryOf(normalizedTargetDirectory, normalizedSourcePath))
            {
                throw new InvalidOperationException(TargetInsideSourceDirectoryMessage);
            }

            if (IsSubdirectoryOf(normalizedSourcePath, normalizedTargetDirectory))
            {
                throw new InvalidOperationException(TargetContainsSourceDirectoryMessage);
            }
        }
    }

    private static bool IsSubdirectoryOf(string path, string potentialParent)
    {
        return path.StartsWith(potentialParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
