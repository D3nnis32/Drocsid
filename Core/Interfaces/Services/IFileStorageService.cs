using Drocsid.HenrikDennis2025.Core.Models;
using FileInfo = Drocsid.HenrikDennis2025.Core.Models.FileInfo;

namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

public interface IFileStorageService
{
    Task<Attachment> UploadFileAsync(Stream stream, string fileName, string contentType, string id = null);
    Task<Stream> DownloadFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);
    Task<FileInfo> GetFileInfoAsync(string id);
}