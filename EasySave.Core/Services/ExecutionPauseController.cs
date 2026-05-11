namespace EasySave.Core.Services;

public sealed class ExecutionPauseController
{
    private readonly object syncLock = new();
    private TaskCompletionSource resumeCompletionSource = CreateCompletedSource();
    private bool isPaused;

    public bool IsPaused
    {
        get
        {
            lock (syncLock)
            {
                return isPaused;
            }
        }
    }

    public void Pause()
    {
        lock (syncLock)
        {
            if (isPaused)
            {
                return;
            }

            isPaused = true;
            resumeCompletionSource = CreatePendingSource();
        }
    }

    public void Resume()
    {
        lock (syncLock)
        {
            if (!isPaused)
            {
                return;
            }

            isPaused = false;
            resumeCompletionSource.TrySetResult();
        }
    }

    public async Task<bool> WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;

        lock (syncLock)
        {
            if (!isPaused)
            {
                return false;
            }

            waitTask = resumeCompletionSource.Task;
        }

        await waitTask.WaitAsync(cancellationToken);
        return true;
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
