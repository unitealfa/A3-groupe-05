using EasySave.Core.Models;

namespace EasySave.Core.Strategies;

public interface IBackupStrategy
{
    Task ExecuteAsync(BackupJob job, BackupExecutionContext context, CancellationToken cancellationToken = default);
}
