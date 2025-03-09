using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using FileInfo = Drocsid.HenrikDennis2025.Core.Models.FileInfo;

namespace Drocsid.HenrikDennis2025.Api.Services;

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
                Filename = fileName,
                ContentType = contentType,
                Size = fileStream.Length,
                Path = storagePath
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
        
        public async Task<Attachment> UploadFileAsync(Stream fileStream, string fileName, string contentType, string id = null)
        {
            // Use provided id or create a new one
            var fileId = string.IsNullOrEmpty(id) ? Guid.NewGuid() : Guid.Parse(id);
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
                Filename = fileName,
                ContentType = contentType,
                Size = fileStream.Length,
                Path = storagePath,
                UploadedAt = DateTime.UtcNow
            };
        }

        // Add this method to LocalFileStorageService
        public async Task<FileInfo> GetFileInfoAsync(string id)
        {
            // This is a bit tricky since we don't have a database table of files
            // We'll need to check if the file exists in the file system
            
            var directoryPath = Path.Combine(_storagePath, id);
            if (!Directory.Exists(directoryPath))
            {
                return null;
            }
            
            // Get the first file in the directory
            var files = Directory.GetFiles(directoryPath);
            if (files.Length == 0)
            {
                return null;
            }
            
            var filePath = files[0];
            var fileInfo = new System.IO.FileInfo(filePath);
            var fileName = Path.GetFileName(filePath);
            var contentType = GetContentTypeFromExtension(fileInfo.Extension);
            
            return new FileInfo
            {
                Id = id,
                Filename = fileName,
                ContentType = contentType,
                Size = fileInfo.Length,
                Path = Path.Combine(id, fileName)
            };
        }

        // Helper method to get content type from file extension
        private string GetContentTypeFromExtension(string extension)
        {
            return extension.ToLower() switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }
    }