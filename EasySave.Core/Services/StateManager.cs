using System.Text.Json;
using EasySave.Core.Models;

namespace EasySave.Core.Services;

public sealed class StateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly string stateFilePath;

    public StateManager(string stateFilePath)
    {
        this.stateFilePath = stateFilePath;
    }

    public async Task UpdateAsync(BackupState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var states = await ReadStatesAsync(cancellationToken);
            var index = states.FindIndex(existing => string.Equals(existing.Name, state.Name, StringComparison.OrdinalIgnoreCase));
            state.LastActionTimestamp = DateTime.Now;

            if (index >= 0)
            {
                states[index] = state;
            }
            else
            {
                states.Add(state);
            }

            await WriteStatesAsync(states, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<BackupState>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadStatesAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task SetStateValueAsync(string backupName, string stateValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateValue);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var states = await ReadStatesAsync(cancellationToken);
            var state = states.FirstOrDefault(existing => string.Equals(existing.Name, backupName, StringComparison.OrdinalIgnoreCase));
            if (state is null)
            {
                return;
            }

            state.State = stateValue;
            state.LastActionTimestamp = DateTime.Now;

            await WriteStatesAsync(states, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task RemoveStateAsync(string backupName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var states = await ReadStatesAsync(cancellationToken);
            var removedCount = states.RemoveAll(existing => string.Equals(existing.Name, backupName, StringComparison.OrdinalIgnoreCase));
            if (removedCount == 0)
            {
                return;
            }

            await WriteStatesAsync(states, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task<List<BackupState>> ReadStatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(stateFilePath))
            {
                return [];
            }

            await using var stream = new FileStream(
                stateFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<List<BackupState>>(stream, JsonOptions, cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"State file could not be read: {stateFilePath}", exception);
        }
    }

    private async Task WriteStatesAsync(IReadOnlyList<BackupState> states, CancellationToken cancellationToken)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(stateFilePath)!;
            var tempFilePath = Path.Combine(directoryPath, $"{Path.GetFileName(stateFilePath)}.{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(directoryPath);

            await using (var stream = new FileStream(
                             tempFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, states, JsonOptions, cancellationToken);
            }

            File.Move(tempFilePath, stateFilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"State file could not be saved: {stateFilePath}", exception);
        }
    }
}
