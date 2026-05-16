namespace EasySave.Core.Services;

public sealed class PriorityFileCoordinator
{
    private readonly object syncLock = new();
    private TaskCompletionSource priorityDrainCompletionSource = CreateCompletedSource();
    private TaskCompletionSource registrationCompletionSource = CreateCompletedSource();
    private int pendingPriorityFiles;
    private int pendingRegistrationJobs;
    private int pendingPriorityJobs;
    private TaskCompletionSource priorityJobsCompletionSource = CreateCompletedSource();

    public void BeginRegistrationWindow(int jobCount)
    {
        if (jobCount <= 0)
        {
            return;
        }

        lock (syncLock)
        {
            pendingRegistrationJobs += jobCount;
            if (pendingRegistrationJobs > 0)
            {
                registrationCompletionSource = CreatePendingSource();
            }
        }
    }

    public void CompleteRegistrationForJob()
    {
        lock (syncLock)
        {
            if (pendingRegistrationJobs <= 0)
            {
                return;
            }

            pendingRegistrationJobs--;
            if (pendingRegistrationJobs == 0)
            {
                registrationCompletionSource.TrySetResult();
            }
        }
    }

    public void CancelRegistrationWindow()
    {
        lock (syncLock)
        {
            pendingRegistrationJobs = 0;
            registrationCompletionSource.TrySetResult();
        }
    }

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

    public void RegisterPriorityJob()
    {
        lock (syncLock)
        {
            if (pendingPriorityJobs == 0)
            {
                priorityJobsCompletionSource = CreatePendingSource();
            }

            pendingPriorityJobs++;
        }
    }

    public void CompletePriorityJob()
    {
        lock (syncLock)
        {
            if (pendingPriorityJobs <= 0)
            {
                return;
            }

            pendingPriorityJobs--;
            if (pendingPriorityJobs == 0)
            {
                priorityJobsCompletionSource.TrySetResult();
            }
        }
    }

    public async Task WaitUntilNonPriorityTransfersAllowedAsync(CancellationToken cancellationToken)
    {
        Task priorityWaitTask;
        Task registrationWaitTask;
        Task priorityJobsWaitTask;

        lock (syncLock)
        {
            if (pendingPriorityFiles == 0 && pendingRegistrationJobs == 0 && pendingPriorityJobs == 0)
            {
                return;
            }

            priorityWaitTask = priorityDrainCompletionSource.Task;
            registrationWaitTask = registrationCompletionSource.Task;
            priorityJobsWaitTask = priorityJobsCompletionSource.Task;
        }

        await Task.WhenAll(priorityWaitTask, registrationWaitTask, priorityJobsWaitTask).WaitAsync(cancellationToken);
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
