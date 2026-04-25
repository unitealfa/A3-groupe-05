using EasyLog;
using EasySave.Core.Services;

namespace EasySave.Core.Strategies;

public sealed class BackupExecutionContext
{
    public BackupExecutionContext(StateManager stateManager, ILoggerService logger)
    {
        StateManager = stateManager;
        Logger = logger;
    }

    public StateManager StateManager { get; }

    public ILoggerService Logger { get; }
}
