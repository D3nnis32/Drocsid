using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Server.Services;

public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _storagePath;

        public LocalFileStorageService(IConfiguration configuration)
        {
            _storagePath = configuration["FileStorage:LocalPath"] ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileStorage");
            
            // Ensure the storage directory exists
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task<Attachment> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            // Create a unique file name to avoid collisions
            var fileId = Guid.NewGuid();
            var fileExtension = Path.GetExtension(fileName);
            var storagePath = Path.Combine(fileId.ToString("N"), fileName);
            var fullPath = Path.Combine(_storagePath, fileId.ToString("N"));
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            
            fullPath = Path.Combine(fullPath, fileName);
            
            // Save the file
            using (var fileStreamWriter = new FileStream(fullPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(fileStreamWriter);
            }
            
            // Create and return the attachment record
            return new Attachment
            {
                Id = fileId,
                FileName = fileName,
                ContentType = contentType,
                FileSize = fileStream.Length,
                StoragePath = storagePath
            };
        }

        public async Task<Stream> DownloadFileAsync(string storagePath)
        {
            var fullPath = Path.Combine(_storagePath, storagePath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("File not found", fullPath);
            }
            
            // Return file as stream
            return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        }

        public async Task DeleteFileAsync(string storagePath)
        {
            var fullPath = Path.Combine(_storagePath, storagePath);
            
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            
            // Optionally delete parent directory if empty
            var directory = Path.GetDirectoryName(fullPath);
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }