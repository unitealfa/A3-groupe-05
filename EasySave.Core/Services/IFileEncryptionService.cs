using EasySave.Core.Models;

namespace EasySave.Core.Services;

public interface IFileEncryptionService
{
    Task<long> EncryptAsync(string filePath, AppSettings settings, CancellationToken cancellationToken = default);
}
