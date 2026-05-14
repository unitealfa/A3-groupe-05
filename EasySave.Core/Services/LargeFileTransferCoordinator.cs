namespace EasySave.Core.Services;

public sealed class LargeFileTransferCoordinator
{
    private readonly SemaphoreSlim largeFileTransferLock = new(1, 1);

    public async Task<LargeFileTransferLease> AcquireAsync(long fileSizeBytes, Models.AppSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.IsLargeFile(fileSizeBytes))
        {
            return LargeFileTransferLease.NoOp;
        }

        await largeFileTransferLock.WaitAsync(cancellationToken);
        return new LargeFileTransferLease(largeFileTransferLock);
    }
}

public sealed class LargeFileTransferLease : IAsyncDisposable
{
    private readonly SemaphoreSlim? semaphoreSlim;

    internal static readonly LargeFileTransferLease NoOp = new(null);

    internal LargeFileTransferLease(SemaphoreSlim? semaphoreSlim)
    {
        this.semaphoreSlim = semaphoreSlim;
    }

    public ValueTask DisposeAsync()
    {
        semaphoreSlim?.Release();
        return ValueTask.CompletedTask;
    }
}
