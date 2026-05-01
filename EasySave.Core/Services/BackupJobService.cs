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
        jobs.Add(job);
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

        if (!Directory.Exists(job.SourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {job.SourceDirectory}");
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

        if (!Enum.IsDefined(job.Type))
        {
            throw new ArgumentException("The backup type is invalid.", nameof(job));
        }
    }
}
