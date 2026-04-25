namespace EasySave.Core.Models;

public sealed class BackupState
{
    public string Name { get; set; } = string.Empty;

    public DateTime LastActionTimestamp { get; set; }

    public string State { get; set; } = "Inactive";

    public int TotalFilesToCopy { get; set; }

    public long TotalFilesSize { get; set; }

    public double Progression { get; set; }

    public int RemainingFiles { get; set; }

    public long RemainingSize { get; set; }

    public string CurrentSourceFilePath { get; set; } = string.Empty;

    public string CurrentDestinationFilePath { get; set; } = string.Empty;
}
