using System.Text.Json;

namespace EasyLog;

public sealed class JsonLoggerService : ILoggerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly string logDirectory;

    public JsonLoggerService(string? logDirectory = null)
    {
        this.logDirectory = logDirectory ?? GetDefaultLogDirectory();
        Directory.CreateDirectory(this.logDirectory);
    }

    public async Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var logFilePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");
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
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProSoft",
            "EasySave",
            "logs");
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
