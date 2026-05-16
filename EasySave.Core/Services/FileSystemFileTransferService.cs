namespace EasySave.Core.Services;

public sealed class FileSystemFileTransferService : IFileTransferService
{
    private const int MaxCopyAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    public async Task CopyAsync(
        string sourceFilePath,
        string destinationFilePath,
        bool overwrite,
        Func<long, Task>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Source file not found during copy: " + sourceFilePath, sourceFilePath);
        }

        if (Directory.Exists(destinationFilePath))
        {
            throw new IOException("Destination path points to a directory: " + destinationFilePath);
        }

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= MaxCopyAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (overwrite)
                {
                    RemoveReadonlyAttributeIfNeeded(destinationFilePath);
                }

                await CopyCoreAsync(sourceFilePath, destinationFilePath, overwrite, progressCallback, cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastFailure = exception;
                if (attempt == MaxCopyAttempts)
                {
                    break;
                }

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw CreateDetailedCopyException(sourceFilePath, destinationFilePath, lastFailure);
    }

    private static async Task CopyCoreAsync(
        string sourceFilePath,
        string destinationFilePath,
        bool overwrite,
        Func<long, Task>? progressCallback,
        CancellationToken cancellationToken)
    {
        var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;
        await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
        await using var destinationStream = new FileStream(destinationFilePath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = new byte[81920];
        long totalTransferredBytes = 0;

        while (true)
        {
            var bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalTransferredBytes += bytesRead;

            if (progressCallback is not null)
            {
                await progressCallback(totalTransferredBytes);
            }
        }
    }

    private static void RemoveReadonlyAttributeIfNeeded(string destinationFilePath)
    {
        if (!File.Exists(destinationFilePath))
        {
            return;
        }

        var attributes = File.GetAttributes(destinationFilePath);
        if (!attributes.HasFlag(FileAttributes.ReadOnly))
        {
            return;
        }

        File.SetAttributes(destinationFilePath, attributes & ~FileAttributes.ReadOnly);
    }

    private static Exception CreateDetailedCopyException(string sourceFilePath, string destinationFilePath, Exception? failure)
    {
        if (failure is UnauthorizedAccessException)
        {
            return new UnauthorizedAccessException(
                $"Access denied while copying '{sourceFilePath}' to '{destinationFilePath}'. The source or destination file may be locked, read-only, or protected.",
                failure);
        }

        if (failure is IOException)
        {
            return new IOException(
                $"Copy failed from '{sourceFilePath}' to '{destinationFilePath}'. {failure.Message}",
                failure);
        }

        return new IOException(
            $"Copy failed from '{sourceFilePath}' to '{destinationFilePath}'.",
            failure);
    }
}
