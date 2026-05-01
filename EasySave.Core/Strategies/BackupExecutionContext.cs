using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Services;

namespace EasySave.Core.Strategies;

public sealed class BackupExecutionContext
{
    public BackupExecutionContext(
        StateManager stateManager,
        ILoggerService logger,
        AppSettings settings,
        IBusinessSoftwareDetector businessSoftwareDetector,
        IFileEncryptionService fileEncryptionService)
    {
        StateManager = stateManager;
        Logger = logger;
        Settings = settings;
        BusinessSoftwareDetector = businessSoftwareDetector;
        FileEncryptionService = fileEncryptionService;
    }

    public StateManager StateManager { get; }

    public ILoggerService Logger { get; }

    public AppSettings Settings { get; }

    public IBusinessSoftwareDetector BusinessSoftwareDetector { get; }

    public IFileEncryptionService FileEncryptionService { get; }

    public bool IsBlockedByBusinessSoftware { get; set; }
}
