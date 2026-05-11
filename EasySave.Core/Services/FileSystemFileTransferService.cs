namespace EasySave.Core.Services;

public sealed class FileSystemFileTransferService : IFileTransferService
{
    public async Task CopyAsync(string sourceFilePath, string destinationFilePath, bool overwrite, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;
        await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var destinationStream = new FileStream(destinationFilePath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }
}
