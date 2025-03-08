using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : BaseController
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeRegistrationOptions _nodeOptions;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileStorageService fileStorageService,
        IHttpClientFactory httpClientFactory,
        IOptions<NodeRegistrationOptions> nodeOptions,
        ILogger<FilesController> logger)
        : base(logger)
    {
        _fileStorageService = fileStorageService;
        _httpClientFactory = httpClientFactory;
        _nodeOptions = nodeOptions.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Attachment>> UploadFile(IFormFile file, [FromForm] string fileId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file was uploaded");
        }

        try
        {
            // If fileId is specified, use it (for replication scenarios)
            // Otherwise generate a new ID
            var id = string.IsNullOrEmpty(fileId) ? Guid.NewGuid().ToString() : fileId;
            
            using (var stream = file.OpenReadStream())
            {
                var attachment = await _fileStorageService.UploadFileAsync(
                    stream, 
                    file.FileName, 
                    file.ContentType,
                    id);
                
                // Register the file with the registry service
                var registrationSuccess = await RegisterFileWithRegistryAsync(
                    id, 
                    file.FileName, 
                    file.ContentType, 
                    file.Length);
                
                if (!registrationSuccess)
                {
                    _logger.LogWarning("File uploaded but not registered with registry: {FileName}", file.FileName);
                }
                
                return Ok(attachment);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            return StatusCode(500, $"Error uploading file: {ex.Message}");
        }
    }

    [HttpGet("{id}/content")]
    public async Task<IActionResult> DownloadFile(string id)
    {
        try
        {
            // Check if the file exists in our local storage
            var fileInfo = await _fileStorageService.GetFileInfoAsync(id);
            
            if (fileInfo == null)
            {
                // If file is not found locally, try to find it through the registry
                var registryFileInfo = await GetFileInfoFromRegistryAsync(id);
                
                if (registryFileInfo == null)
                {
                    return NotFound($"File with ID {id} not found");
                }
                
                // File exists in the system but not on this node
                // Redirect to the node that has it or replicate it
                // For now, return a redirect to the registry's file location endpoint
                return RedirectToAction("RedirectToFileNode", new { fileId = id });
            }
            
            // Get the file stream from local storage
            var stream = await _fileStorageService.DownloadFileAsync(fileInfo.Path);
            
            return File(stream, fileInfo.ContentType, fileInfo.Filename);
        }
        catch (System.IO.FileNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file: {FileId}", id);
            return StatusCode(500, $"Error retrieving file: {ex.Message}");
        }
    }

    [HttpGet("redirect/{fileId}")]
    public async Task<IActionResult> RedirectToFileNode(string fileId)
    {
        try
        {
            // Call registry to find the best node for this file
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.GetAsync($"{registryUrl}/api/gateway/files/{fileId}/location");
            
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, $"Registry error: {response.ReasonPhrase}");
            }
            
            var locationInfo = await response.Content.ReadFromJsonAsync<FileLocationInfo>();
            
            if (locationInfo == null)
            {
                return StatusCode(500, "Invalid response from registry");
            }
            
            // Redirect to the appropriate node
            var redirectUrl = $"{locationInfo.NodeEndpoint.TrimEnd('/')}/api/files/{fileId}/content";
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redirecting to file node: {FileId}", fileId);
            return StatusCode(500, $"Error locating file: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(string id)
    {
        try
        {
            // Check if the file exists in our local storage
            var fileInfo = await _fileStorageService.GetFileInfoAsync(id);
            
            if (fileInfo == null)
            {
                return NotFound();
            }

            // Delete file from local storage
            await _fileStorageService.DeleteFileAsync(fileInfo.Path);
            
            // Notify registry about file deletion
            await NotifyRegistryOfFileDeletionAsync(id);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileId}", id);
            return StatusCode(500, $"Error deleting file: {ex.Message}");
        }
    }

    [HttpGet("{id}/info")]
    public async Task<ActionResult<FileMetadata>> GetFileInfo(string id)
    {
        try
        {
            // Check if the file exists in our local storage
            var fileInfo = await _fileStorageService.GetFileInfoAsync(id);
            
            if (fileInfo == null)
            {
                // If file is not found locally, try to find it through the registry
                var registryFileInfo = await GetFileInfoFromRegistryAsync(id);
                
                if (registryFileInfo == null)
                {
                    return NotFound($"File with ID {id} not found");
                }
                
                return Ok(new FileMetadata
                {
                    Id = registryFileInfo.FileId,
                    Filename = registryFileInfo.FileName,
                    ContentType = registryFileInfo.ContentType,
                    Size = registryFileInfo.Size,
                    IsLocal = false,
                    Path = null // Don't expose internal path
                });
            }
            
            return Ok(new FileMetadata
            {
                Id = fileInfo.Id,
                Filename = fileInfo.Filename,
                ContentType = fileInfo.ContentType,
                Size = fileInfo.Size,
                IsLocal = true,
                Path = null // Don't expose internal path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file info: {FileId}", id);
            return StatusCode(500, $"Error retrieving file info: {ex.Message}");
        }
    }

    /// <summary>
    /// Register the file with the registry service
    /// </summary>
    private async Task<bool> RegisterFileWithRegistryAsync(
        string fileId, 
        string fileName, 
        string contentType, 
        long size)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var fileRegistration = new FileRegistrationRequest
            {
                FileId = fileId,
                FileName = fileName,
                ContentType = contentType,
                Size = size,
                NodeIds = new List<string> { _nodeOptions.NodeId },
                Checksum = "", // Implement checksum if needed
                Metadata = new Dictionary<string, string>()
            };
            
            var response = await client.PostAsJsonAsync(
                $"{registryUrl}/api/registry/files/register", 
                fileRegistration);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("File registered with registry: {FileName}, ID: {FileId}", fileName, fileId);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to register file with registry. Status: {Status}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering file with registry: {FileName}, ID: {FileId}", fileName, fileId);
            return false;
        }
    }

    /// <summary>
    /// Get file information from the registry
    /// </summary>
    private async Task<FileStorage> GetFileInfoFromRegistryAsync(string fileId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.GetAsync($"{registryUrl}/api/registry/files/{fileId}");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<FileStorage>();
            }
            else
            {
                _logger.LogWarning("File not found in registry: {FileId}. Status: {Status}", 
                    fileId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info from registry: {FileId}", fileId);
            return null;
        }
    }

    /// <summary>
    /// Notify registry that a file has been deleted from this node
    /// </summary>
    private async Task<bool> NotifyRegistryOfFileDeletionAsync(string fileId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.DeleteAsync(
                $"{registryUrl}/api/registry/files/{fileId}/locations/{_nodeOptions.NodeId}");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Registry notified of file deletion: {FileId}", fileId);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to notify registry of file deletion. Status: {Status}", 
                    response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying registry of file deletion: {FileId}", fileId);
            return false;
        }
    }
}