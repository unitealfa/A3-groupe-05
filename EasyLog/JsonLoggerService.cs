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

        var logFilePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");
        var writeLock = WriteLocks.GetOrAdd(logFilePath, _ => new SemaphoreSlim(1, 1));

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadEntriesAsync(logFilePath, cancellationToken);
            entries.Add(entry);

            await using var stream = File.Create(logFilePath);
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
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

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<LogEntry>>(stream, JsonOptions, cancellationToken) ?? [];
    }
}
