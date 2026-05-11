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
        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
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

            await using var stream = File.Create(stateFilePath);
            await JsonSerializer.SerializeAsync(stream, states, JsonOptions, cancellationToken);
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

            await using var stream = File.Create(stateFilePath);
            await JsonSerializer.SerializeAsync(stream, states, JsonOptions, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task<List<BackupState>> ReadStatesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(stateFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(stateFilePath);
        return await JsonSerializer.DeserializeAsync<List<BackupState>>(stream, JsonOptions, cancellationToken) ?? [];
    }
}
