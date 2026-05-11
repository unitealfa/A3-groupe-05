namespace EasySave.Core.Services;

public interface IFileTransferService
{
    Task CopyAsync(string sourceFilePath, string destinationFilePath, bool overwrite, CancellationToken cancellationToken = default);
}
