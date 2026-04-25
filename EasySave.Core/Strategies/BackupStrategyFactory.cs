using EasySave.Core.Models;

namespace EasySave.Core.Strategies;

public static class BackupStrategyFactory
{
    public static IBackupStrategy Create(BackupType backupType)
    {
        return backupType switch
        {
            BackupType.Complete => new CompleteBackupStrategy(),
            BackupType.Differential => new DifferentialBackupStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(backupType), "Unsupported backup type.")
        };
    }
}
