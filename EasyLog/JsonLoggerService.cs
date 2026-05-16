using System.Text.Json;
using System.Collections.Concurrent;

namespace EasyLog;

public sealed class JsonLoggerService : ILoggerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WriteLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string logDirectory;

    public JsonLoggerService(string? logDirectory = null)
    {
        this.logDirectory = logDirectory ?? GetDefaultLogDirectory();
        Directory.CreateDirectory(this.logDirectory);
    }

    public async Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");
        var writeLock = WriteLocks.GetOrAdd(logFilePath, _ => new SemaphoreSlim(1, 1));

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadEntriesAsync(logFilePath, cancellationToken);
            entries.Add(entry);

            var tempFilePath = $"{logFilePath}.{Guid.NewGuid():N}.tmp";
            await using (var stream = new FileStream(
                             tempFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
            }

            File.Move(tempFilePath, logFilePath, overwrite: true);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static string GetDefaultLogDirectory()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "logs");
    }

    private static async Task<List<LogEntry>> ReadEntriesAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        return await JsonSerializer.DeserializeAsync<List<LogEntry>>(stream, JsonOptions, cancellationToken) ?? [];
    }
}
