using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

/// <summary>
    /// Handles file transfer operations between storage nodes
    /// </summary>
    [ApiController]
    [Route("api/filetransfer")]
    public class FileTransferController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileTransferController> _logger;
        private readonly string _storagePath;

        public FileTransferController(
            IConfiguration configuration,
            ILogger<FileTransferController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _storagePath = _configuration["FileStorage:LocalPath"] ?? 
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileStorage");
        }

        /// <summary>
        /// Downloads a file from this node
        /// </summary>
        [HttpGet("download/{fileId}")]
        public IActionResult DownloadFile(string fileId)
        {
            try
            {
                // In a real implementation, you would look up the file path from the fileId
                // in a local database. For this example, we assume a simple structure.
                var filePath = Path.Combine(_storagePath, fileId);
                
                if (!Directory.Exists(filePath))
                {
                    _logger.LogWarning("File directory not found for fileId {FileId}", fileId);
                    return NotFound();
                }

                // Find the actual file in the directory
                var files = Directory.GetFiles(filePath);
                if (files.Length == 0)
                {
                    _logger.LogWarning("No files found in directory for fileId {FileId}", fileId);
                    return NotFound();
                }

                var file = files[0]; // Take the first file
                var fileName = Path.GetFileName(file);
                var contentType = GetContentTypeFromFileName(fileName);

                _logger.LogInformation("Serving file {FileId}/{FileName} for download", fileId, fileName);
                return PhysicalFile(file, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileId}", fileId);
                return StatusCode(500, "Error downloading file");
            }
        }

        /// <summary>
        /// Uploads a file to this node
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string fileId, [FromForm] string contentType, [FromForm] string checksum)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file was uploaded");
                }

                // Ensure storage directory exists
                var fileDirectory = Path.Combine(_storagePath, fileId);
                if (!Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }

                // Save the file
                var filePath = Path.Combine(fileDirectory, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Verify the checksum
                string actualChecksum = await CalculateChecksumAsync(filePath);
                if (actualChecksum != checksum)
                {
                    _logger.LogWarning(
                        "Checksum mismatch for file {FileId}. Expected: {ExpectedChecksum}, Actual: {ActualChecksum}",
                        fileId, checksum, actualChecksum);
                    
                    // Delete the corrupted file
                    System.IO.File.Delete(filePath);
                    return BadRequest("File checksum verification failed");
                }

                _logger.LogInformation("File {FileId}/{FileName} uploaded successfully", fileId, file.FileName);
                return Ok(new { FileId = fileId, Path = filePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileId}", fileId);
                return StatusCode(500, "Error uploading file");
            }
        }

        /// <summary>
        /// Verifies a file exists on this node and returns its checksum
        /// </summary>
        [HttpGet("verify/{fileId}")]
        public async Task<IActionResult> VerifyFile(string fileId)
        {
            try
            {
                var fileDirectory = Path.Combine(_storagePath, fileId);
                if (!Directory.Exists(fileDirectory))
                {
                    return NotFound();
                }

                var files = Directory.GetFiles(fileDirectory);
                if (files.Length == 0)
                {
                    return NotFound();
                }

                // Calculate checksum of the first file
                string checksum = await CalculateChecksumAsync(files[0]);
                return Ok(checksum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying file {FileId}", fileId);
                return StatusCode(500, "Error verifying file");
            }
        }

        /// <summary>
        /// Deletes a file from this node
        /// </summary>
        [HttpDelete("{fileId}")]
        public IActionResult DeleteFile(string fileId)
        {
            try
            {
                var fileDirectory = Path.Combine(_storagePath, fileId);
                if (Directory.Exists(fileDirectory))
                {
                    Directory.Delete(fileDirectory, true);
                    _logger.LogInformation("File {FileId} deleted successfully", fileId);
                    return Ok();
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId}", fileId);
                return StatusCode(500, "Error deleting file");
            }
        }

        /// <summary>
        /// Calculates SHA256 checksum of a file
        /// </summary>
        private async Task<string> CalculateChecksumAsync(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = System.IO.File.OpenRead(filePath))
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Determines content type from file name
        /// </summary>
        private string GetContentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            return extension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                ".wav" => "audio/wav",
                _ => "application/octet-stream"
            };
        }
    }