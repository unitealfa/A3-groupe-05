using System.Text.Json;
using EasySave.Core.Models;

namespace EasySave.Core.Configuration;

public sealed class BackupJobRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string jobsFilePath;

    public BackupJobRepository(string jobsFilePath)
    {
        this.jobsFilePath = jobsFilePath;
    }

    public async Task<IReadOnlyList<BackupJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(jobsFilePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(jobsFilePath);
            var jobs = await JsonSerializer.DeserializeAsync<List<BackupJob>>(stream, JsonOptions, cancellationToken);
            return jobs ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"Backup jobs file could not be read: {jobsFilePath}", exception);
        }
    }

    public async Task SaveAllAsync(IEnumerable<BackupJob> jobs, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(jobsFilePath)!);

            await using var stream = File.Create(jobsFilePath);
            await JsonSerializer.SerializeAsync(stream, jobs, JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"Backup jobs file could not be saved: {jobsFilePath}", exception);
        }
    }
}
