using System.Globalization;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace EasyLog;

public sealed class XmlLoggerService : ILoggerService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WriteLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string logDirectory;

    public XmlLoggerService(string? logDirectory = null)
    {
        this.logDirectory = logDirectory ?? GetDefaultLogDirectory();
        Directory.CreateDirectory(this.logDirectory);
    }

    public async Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");
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
        var document = new XDocument(
            new XElement("LogEntries", entries.Select(CreateLogEntryElement)));

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
                await document.SaveAsync(stream, SaveOptions.None, cancellationToken);
            }

            try
            {
                File.Move(tempFilePath, logFilePath, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await WriteDirectlyAsync(logFilePath, document, cancellationToken);
            }
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
        }
    }

    private static async Task WriteDirectlyAsync(string logFilePath, XDocument document, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            logFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        await document.SaveAsync(stream, SaveOptions.None, cancellationToken);
    }

    private static XElement CreateLogEntryElement(LogEntry entry)
    {
        return new XElement(
            "LogEntry",
            new XElement("Timestamp", entry.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
            new XElement("BackupName", entry.BackupName),
            new XElement("SourceFilePath", entry.SourceFilePath),
            new XElement("DestinationFilePath", entry.DestinationFilePath),
            new XElement("FileSize", entry.FileSize),
            new XElement("TransferTimeMs", entry.TransferTimeMs),
            new XElement("EncryptionTimeMs", entry.EncryptionTimeMs),
            new XElement("Status", entry.Status),
            new XElement("ErrorMessage", entry.ErrorMessage ?? string.Empty));
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
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

        return document.Root?
            .Elements("LogEntry")
            .Select(element => new LogEntry
            {
                Timestamp = DateTime.Parse(
                    element.Element("Timestamp")?.Value ?? DateTime.MinValue.ToString("O", CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                BackupName = element.Element("BackupName")?.Value ?? string.Empty,
                SourceFilePath = element.Element("SourceFilePath")?.Value ?? string.Empty,
                DestinationFilePath = element.Element("DestinationFilePath")?.Value ?? string.Empty,
                FileSize = long.TryParse(element.Element("FileSize")?.Value, out var fileSize) ? fileSize : 0,
                TransferTimeMs = long.TryParse(element.Element("TransferTimeMs")?.Value, out var transferTimeMs) ? transferTimeMs : 0,
                EncryptionTimeMs = long.TryParse(element.Element("EncryptionTimeMs")?.Value, out var encryptionTimeMs) ? encryptionTimeMs : 0,
                Status = element.Element("Status")?.Value ?? string.Empty,
                ErrorMessage = element.Element("ErrorMessage")?.Value
            })
            .ToList() ?? [];
    }

    private static string GetDefaultLogDirectory()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "logs");
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
