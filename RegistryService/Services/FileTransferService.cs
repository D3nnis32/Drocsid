using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Placeholder implementation for the IFileTransferService
/// </summary>
public class FileTransferService : IFileTransferService
{
    private readonly ILogger<FileTransferService> _logger;

    public FileTransferService(ILogger<FileTransferService> logger)
    {
        _logger = logger;
    }

    public Task<Attachment> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        _logger.LogWarning("UploadFileAsync was called on Registry service's placeholder implementation");
        return Task.FromResult(new Attachment
        {
            Id = Guid.NewGuid(),
            Filename = fileName,
            ContentType = contentType,
            Size = fileStream.Length,
            Path = $"placeholder/{Guid.NewGuid()}/{fileName}"
        });
    }

    public Task<Stream> DownloadFileAsync(string storagePath)
    {
        _logger.LogWarning("DownloadFileAsync was called on Registry service's placeholder implementation");
        return Task.FromResult(Stream.Null as Stream);
    }

    public Task DeleteFileAsync(string storagePath)
    {
        _logger.LogWarning("DeleteFileAsync was called on Registry service's placeholder implementation");
        return Task.CompletedTask;
    }

    public Task<bool> TransferFileAsync(string fileId, string sourceNodeId, string targetNodeId)
    {
        _logger.LogWarning("TransferFileAsync was called on Registry service's placeholder implementation");
        return (Task<bool>)Task.CompletedTask;
    }
}