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
        IFileEncryptionService fileEncryptionService,
        PriorityFileCoordinator priorityFileCoordinator,
        LargeFileTransferCoordinator largeFileTransferCoordinator,
        IFileTransferService fileTransferService,
        ExecutionPauseController pauseController)
    {
        StateManager = stateManager;
        Logger = logger;
        Settings = settings;
        BusinessSoftwareDetector = businessSoftwareDetector;
        FileEncryptionService = fileEncryptionService;
        PriorityFileCoordinator = priorityFileCoordinator;
        LargeFileTransferCoordinator = largeFileTransferCoordinator;
        FileTransferService = fileTransferService;
        PauseController = pauseController;
    }

    public StateManager StateManager { get; }

    public ILoggerService Logger { get; }

    public AppSettings Settings { get; }

    public IBusinessSoftwareDetector BusinessSoftwareDetector { get; }

    public IFileEncryptionService FileEncryptionService { get; }

    public PriorityFileCoordinator PriorityFileCoordinator { get; }

    public LargeFileTransferCoordinator LargeFileTransferCoordinator { get; }

    public IFileTransferService FileTransferService { get; }

    public ExecutionPauseController PauseController { get; }

    public bool IsBlockedByBusinessSoftware { get; set; }
}
