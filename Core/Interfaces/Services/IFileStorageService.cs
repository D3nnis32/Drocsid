using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

public interface IFileStorageService
{
    Task<Attachment> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream> DownloadFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);
}