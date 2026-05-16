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

            await PersistEntriesAsync(logFilePath, entries, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static async Task PersistEntriesAsync(string logFilePath, List<LogEntry> entries, CancellationToken cancellationToken)
    {
        var tempFilePath = $"{logFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
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

            try
            {
                File.Move(tempFilePath, logFilePath, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await WriteDirectlyAsync(logFilePath, entries, cancellationToken);
            }
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
        }
    }

    private static async Task WriteDirectlyAsync(string logFilePath, List<LogEntry> entries, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            logFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
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

    private static void TryDeleteTempFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
        }
    }
}
