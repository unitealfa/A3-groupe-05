using EasySave.Core.Models;

namespace EasySave.Core.Strategies;

public sealed class DifferentialBackupStrategy : IBackupStrategy
{
    public Task ExecuteAsync(BackupJob job, BackupExecutionContext context, CancellationToken cancellationToken = default)
    {
        return BackupStrategyRunner.ExecuteAsync(job, context, ShouldCopy, cancellationToken);
    }

    private static bool ShouldCopy(FileInfo sourceFile, FileInfo destinationFile)
    {
        if (!destinationFile.Exists)
        {
            return true;
        }

        return sourceFile.LastWriteTimeUtc > destinationFile.LastWriteTimeUtc ||
            sourceFile.Length != destinationFile.Length;
    }
}
