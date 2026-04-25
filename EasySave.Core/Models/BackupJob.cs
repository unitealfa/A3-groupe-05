namespace EasySave.Core.Models;

public sealed class BackupJob
{
    public string Name { get; set; } = string.Empty;

    public string SourceDirectory { get; set; } = string.Empty;

    public string TargetDirectory { get; set; } = string.Empty;

    public BackupType Type { get; set; }
}
