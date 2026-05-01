using System.Globalization;
using System.Xml.Linq;

namespace EasyLog;

public sealed class XmlLoggerService : ILoggerService
{
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly string logDirectory;

    public XmlLoggerService(string? logDirectory = null)
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
            var logFilePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");
            var entries = await ReadEntriesAsync(logFilePath, cancellationToken);
            entries.Add(entry);

            var document = new XDocument(
                new XElement("LogEntries", entries.Select(CreateLogEntryElement)));

            await using var stream = File.Create(logFilePath);
            await document.SaveAsync(stream, SaveOptions.None, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
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
            new XElement("Status", entry.Status),
            new XElement("ErrorMessage", entry.ErrorMessage ?? string.Empty));
    }

    private static async Task<List<LogEntry>> ReadEntriesAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
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
                Status = element.Element("Status")?.Value ?? string.Empty,
                ErrorMessage = element.Element("ErrorMessage")?.Value
            })
            .ToList() ?? [];
    }

    private static string GetDefaultLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProSoft",
            "EasySave",
            "logs");
    }
}
