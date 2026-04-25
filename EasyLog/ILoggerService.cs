namespace EasyLog;

public interface ILoggerService
{
    Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
