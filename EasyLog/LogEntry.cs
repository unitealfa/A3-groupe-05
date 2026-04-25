namespace EasyLog;

public sealed class LogEntry
{
    public DateTime Timestamp { get; set; }

    public string BackupName { get; set; } = string.Empty;

    public string SourceFilePath { get; set; } = string.Empty;

    public string DestinationFilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public long TransferTimeMs { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}
