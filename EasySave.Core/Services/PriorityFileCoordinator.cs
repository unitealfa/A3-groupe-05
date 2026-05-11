namespace EasySave.Core.Services;

public sealed class PriorityFileCoordinator
{
    private readonly object syncLock = new();
    private TaskCompletionSource priorityDrainCompletionSource = CreateCompletedSource();
    private int pendingPriorityFiles;

    public void RegisterPriorityFiles(int priorityFileCount)
    {
        if (priorityFileCount <= 0)
        {
            return;
        }

        lock (syncLock)
        {
            if (pendingPriorityFiles == 0)
            {
                priorityDrainCompletionSource = CreatePendingSource();
            }

            pendingPriorityFiles += priorityFileCount;
        }
    }

    public async Task WaitUntilNonPriorityTransfersAllowedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;

        lock (syncLock)
        {
            if (pendingPriorityFiles == 0)
            {
                return;
            }

            waitTask = priorityDrainCompletionSource.Task;
        }

        await waitTask.WaitAsync(cancellationToken);
    }

    public void CompletePriorityFile()
    {
        ReleasePriorityFiles(1);
    }

    public void ReleaseUnprocessedPriorityFiles(int count)
    {
        ReleasePriorityFiles(count);
    }

    private void ReleasePriorityFiles(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (syncLock)
        {
            pendingPriorityFiles = Math.Max(0, pendingPriorityFiles - count);
            if (pendingPriorityFiles == 0)
            {
                priorityDrainCompletionSource.TrySetResult();
            }
        }
    }

    private static TaskCompletionSource CreatePendingSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static TaskCompletionSource CreateCompletedSource()
    {
        var completionSource = CreatePendingSource();
        completionSource.TrySetResult();
        return completionSource;
    }
}
