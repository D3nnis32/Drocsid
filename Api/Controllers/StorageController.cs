using System.Security.Cryptography;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

/// <summary>
    /// Handles storage operations for node-to-node file transfers
    /// </summary>
    [ApiController]
    [Route("api/storage")]
    [Authorize(Policy = "NodeAuthorization")]
    public class StorageController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StorageController> _logger;
        private readonly string _storagePath;
        private readonly IFileRegistry _fileRegistry;
        private readonly Dictionary<string, UploadInfo> _pendingUploads = new();

        public StorageController(
            IConfiguration configuration,
            IFileRegistry fileRegistry,
            ILogger<StorageController> logger)
        {
            _configuration = configuration;
            _fileRegistry = fileRegistry;
            _logger = logger;
            _storagePath = _configuration["FileStorage:LocalPath"] ?? 
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileStorage");
                
            // Ensure base storage directory exists
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        /// <summary>
        /// Prepares for a file download by generating a temporary download URL
        /// </summary>
        [HttpGet("prepare-download/{fileId}")]
        public async Task<IActionResult> PrepareDownload(string fileId)
        {
            try
            {
                // Check if the file exists in the registry
                var file = await _fileRegistry.GetFileAsync(fileId);
                if (file == null)
                {
                    return NotFound("File not found in registry");
                }
                
                // Check if this node has the file
                var nodeId = GetCurrentNodeId();
                if (!file.NodeLocations.Contains(nodeId))
                {
                    return BadRequest("This node does not have the requested file");
                }
                
                // Determine the local file path
                var filePath = GetLocalFilePath(fileId);
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("File {FileId} is registered but not found on disk at {FilePath}", 
                        fileId, filePath);
                    return NotFound("File registered but not found on disk");
                }
                
                // Generate a temporary download token (in a real system, this would be more secure)
                var downloadToken = Guid.NewGuid().ToString();
                
                // Store token in temp storage with short expiration
                var downloadUrl = $"{Request.Scheme}://{Request.Host}/api/storage/download/{fileId}?token={downloadToken}";
                
                // In a real system, you would store this token with an expiration in a distributed cache
                // For this example, we'll use the URL directly
                
                return Ok(new 
                {
                    DownloadUrl = downloadUrl,
                    ExpiresInSeconds = 300 // 5 minutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing download for file {FileId}", fileId);
                return StatusCode(500, "Error preparing file download");
            }
        }

        /// <summary>
        /// Downloads a file (used with the temporary URL)
        /// </summary>
        [HttpGet("download/{fileId}")]
        [AllowAnonymous] // This endpoint uses the token parameter for auth instead
        public async Task<IActionResult> DownloadFile(string fileId, [FromQuery] string token)
        {
            try
            {
                // In a real system, validate the token from your distributed cache
                // For this example, we'll skip detailed validation
                
                // Check if the file exists
                var filePath = GetLocalFilePath(fileId);
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound();
                }
                
                // Get file info for the response
                var fileInfo = new FileInfo(filePath);
                var contentType = GetContentTypeFromFileName(fileInfo.Name);
                
                // Update access time in registry
                await _fileRegistry.UpdateFileAccessTimeAsync(fileId);
                
                _logger.LogInformation("Serving file {FileId} for download", fileId);
                return PhysicalFile(filePath, contentType, fileInfo.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileId}", fileId);
                return StatusCode(500, "Error downloading file");
            }
        }

        /// <summary>
        /// Prepares for a file upload by generating a temporary upload URL and ID
        /// </summary>
        [HttpPost("prepare-upload")]
        public IActionResult PrepareUpload([FromBody] UploadMetadata metadata)
        {
            try
            {
                if (metadata == null || string.IsNullOrEmpty(metadata.FileId))
                {
                    return BadRequest("Invalid upload metadata");
                }
                
                // Generate a temporary upload ID
                var uploadId = Guid.NewGuid().ToString();
                
                // Create temporary folder for the upload
                var tempUploadPath = Path.Combine(_storagePath, "temp", uploadId);
                Directory.CreateDirectory(tempUploadPath);
                
                // Store upload info for confirmation later
                _pendingUploads[uploadId] = new UploadInfo
                {
                    FileId = metadata.FileId,
                    Filename = metadata.Filename,
                    ContentType = metadata.ContentType,
                    ExpectedSize = metadata.Size,
                    ExpectedChecksum = metadata.Checksum,
                    Metadata = metadata.Metadata,
                    TempPath = tempUploadPath,
                    UploadTime = DateTime.UtcNow
                };
                
                // Generate the upload URL
                var uploadUrl = $"{Request.Scheme}://{Request.Host}/api/storage/upload/{uploadId}";
                
                return Ok(new
                {
                    UploadUrl = uploadUrl,
                    UploadId = uploadId,
                    ExpiresInSeconds = 3600 // 1 hour
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing upload");
                return StatusCode(500, "Error preparing file upload");
            }
        }

        /// <summary>
        /// Handles the actual file upload
        /// </summary>
        [HttpPut("upload/{uploadId}")]
        [AllowAnonymous] // This endpoint is authorized by uploadId
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadFile(string uploadId, [FromForm] IFormFile file)
        {
            try
            {
                // Check if the upload ID is valid
                if (!_pendingUploads.TryGetValue(uploadId, out var uploadInfo))
                {
                    return BadRequest("Invalid or expired upload ID");
                }
                
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file was uploaded");
                }
                
                // Check if file size matches expected size
                if (uploadInfo.ExpectedSize > 0 && file.Length != uploadInfo.ExpectedSize)
                {
                    _logger.LogWarning("Uploaded file size {ActualSize} doesn't match expected size {ExpectedSize}",
                        file.Length, uploadInfo.ExpectedSize);
                }
                
                // Save the file to the temporary location
                var tempFilePath = Path.Combine(uploadInfo.TempPath, Path.GetFileName(uploadInfo.Filename));
                
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                
                // Store the actual path for confirmation
                uploadInfo.ActualFilePath = tempFilePath;
                
                return Ok(new
                {
                    UploadId = uploadId,
                    Status = "Pending",
                    Message = "File uploaded successfully, awaiting confirmation"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file for upload ID {UploadId}", uploadId);
                return StatusCode(500, "Error uploading file");
            }
        }

        /// <summary>
        /// Confirms a completed upload and moves it to permanent storage
        /// </summary>
        [HttpPost("confirm-upload/{uploadId}")]
        public async Task<IActionResult> ConfirmUpload(string uploadId)
        {
            try
            {
                // Check if the upload ID is valid
                if (!_pendingUploads.TryGetValue(uploadId, out var uploadInfo))
                {
                    return BadRequest("Invalid or expired upload ID");
                }
                
                if (string.IsNullOrEmpty(uploadInfo.ActualFilePath) || !System.IO.File.Exists(uploadInfo.ActualFilePath))
                {
                    return BadRequest("No file was uploaded or file is missing");
                }
                
                // Verify checksum if provided
                if (!string.IsNullOrEmpty(uploadInfo.ExpectedChecksum))
                {
                    var actualChecksum = await CalculateChecksumAsync(uploadInfo.ActualFilePath);
                    if (actualChecksum != uploadInfo.ExpectedChecksum)
                    {
                        _logger.LogWarning("Checksum mismatch for upload {UploadId}. Expected: {ExpectedChecksum}, Actual: {ActualChecksum}",
                            uploadId, uploadInfo.ExpectedChecksum, actualChecksum);
                        
                        return BadRequest("File checksum verification failed");
                    }
                }
                
                // Create the file's permanent storage directory
                var finalDir = Path.Combine(_storagePath, uploadInfo.FileId);
                if (!Directory.Exists(finalDir))
                {
                    Directory.CreateDirectory(finalDir);
                }
                
                // Move the file to permanent storage
                var finalPath = Path.Combine(finalDir, uploadInfo.Filename);
                
                if (System.IO.File.Exists(finalPath))
                {
                    System.IO.File.Delete(finalPath); // Replace if exists
                }
                
                System.IO.File.Move(uploadInfo.ActualFilePath, finalPath);
                
                // Clean up the temporary directory
                if (Directory.Exists(uploadInfo.TempPath))
                {
                    Directory.Delete(uploadInfo.TempPath, true);
                }
                
                // Update the file registry to indicate this node has the file
                var nodeId = GetCurrentNodeId();
                await _fileRegistry.AddFileLocationAsync(uploadInfo.FileId, nodeId);
                
                // Remove the pending upload
                _pendingUploads.Remove(uploadId);
                
                _logger.LogInformation("Upload confirmed for file {FileId}, saved to {FinalPath}", 
                    uploadInfo.FileId, finalPath);
                
                return Ok(new
                {
                    FileId = uploadInfo.FileId,
                    Status = "Completed",
                    Message = "File successfully stored"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming upload for upload ID {UploadId}", uploadId);
                return StatusCode(500, "Error confirming upload");
            }
        }

        /// <summary>
        /// Gets a file's information including verification data
        /// </summary>
        [HttpGet("verify/{fileId}")]
        public async Task<IActionResult> VerifyFile(string fileId)
        {
            try
            {
                // Check if the file exists in the registry
                var file = await _fileRegistry.GetFileAsync(fileId);
                if (file == null)
                {
                    return NotFound("File not found in registry");
                }
                
                // Check if this node has the file
                var nodeId = GetCurrentNodeId();
                if (!file.NodeLocations.Contains(nodeId))
                {
                    return BadRequest("This node does not have the requested file");
                }
                
                // Check if the file exists on disk
                var filePath = GetLocalFilePath(fileId);
                if (!System.IO.File.Exists(filePath))
                {
                    // Update registry to remove this node from locations
                    await _fileRegistry.RemoveFileLocationAsync(fileId, nodeId);
                    
                    return NotFound("File registered but not found on disk");
                }
                
                // Calculate current checksum
                var checksum = await CalculateChecksumAsync(filePath);
                
                return Ok(new
                {
                    FileId = fileId,
                    Checksum = checksum,
                    Size = new FileInfo(filePath).Length,
                    StoragePath = filePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying file {FileId}", fileId);
                return StatusCode(500, "Error verifying file");
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
        /// Gets the local file path for a file ID
        /// </summary>
        private string GetLocalFilePath(string fileId)
        {
            var fileDir = Path.Combine(_storagePath, fileId);
            
            if (!Directory.Exists(fileDir))
            {
                return null;
            }
            
            var files = Directory.GetFiles(fileDir);
            return files.FirstOrDefault();
        }

        /// <summary>
        /// Gets the current node ID from configuration
        /// </summary>
        private string GetCurrentNodeId()
        {
            return _configuration["NodeId"] ?? "unknown";
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

        /// <summary>
        /// Class to track information about pending uploads
        /// </summary>
        private class UploadInfo
        {
            public string FileId { get; set; }
            public string Filename { get; set; }
            public string ContentType { get; set; }
            public long ExpectedSize { get; set; }
            public string ExpectedChecksum { get; set; }
            public Dictionary<string, string> Metadata { get; set; }
            public string TempPath { get; set; }
            public string ActualFilePath { get; set; }
            public DateTime UploadTime { get; set; }
        }
        
        /// <summary>
        /// Model for upload metadata
        /// </summary>
        public class UploadMetadata
        {
            public string FileId { get; set; }
            public string Filename { get; set; }
            public string ContentType { get; set; }
            public long Size { get; set; }
            public string Checksum { get; set; }
            public Dictionary<string, string> Metadata { get; set; }
        }
    }