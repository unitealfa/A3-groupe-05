using EasySave.Core.Configuration;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class BackupJobService
{
    private readonly BackupJobRepository repository;

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
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.Name))
        {
            throw new ArgumentException("The backup name is required.", nameof(job));
        }

        if (string.IsNullOrWhiteSpace(job.SourceDirectory))
        {
            throw new ArgumentException("The source directory is required.", nameof(job));
        }

        var sourcePaths = SourceSelectionParser.Parse(job.SourceDirectory);
        if (sourcePaths.Count == 0)
        {
            throw new ArgumentException("The source directory is required.", nameof(job));
        }

        var missingSourcePath = sourcePaths.FirstOrDefault(path => !SourceSelectionParser.IsExistingSource(path));
        if (!string.IsNullOrWhiteSpace(missingSourcePath))
        {
            throw new DirectoryNotFoundException($"Source path does not exist: {missingSourcePath}");
        }

        if (string.IsNullOrWhiteSpace(job.TargetDirectory))
        {
            throw new ArgumentException("The target directory is required.", nameof(job));
        }

        try
        {
            Directory.CreateDirectory(job.TargetDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"The target directory could not be created: {job.TargetDirectory}", exception);
        }

        EnsureSourceAndTargetDoNotOverlap(sourcePaths, job.TargetDirectory);

        if (!Enum.IsDefined(job.Type))
        {
            throw new ArgumentException("The backup type is invalid.", nameof(job));
        }
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
                    throw new InvalidOperationException("The backup target directory cannot be the same as the source directory.");
                }

                continue;
            }

            if (PathsOverlap(normalizedSourcePath, normalizedTargetDirectory))
            {
                throw new InvalidOperationException("The backup target directory cannot be the same as, inside, or contain the source directory.");
            }
        }
    }

    private static bool PathsOverlap(string firstPath, string secondPath)
    {
        return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase) ||
               IsSubdirectoryOf(firstPath, secondPath) ||
               IsSubdirectoryOf(secondPath, firstPath);
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
