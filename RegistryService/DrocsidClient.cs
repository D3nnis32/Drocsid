using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.Extensions.Options;
using FileInfo = Drocsid.HenrikDennis2025.Core.Models.FileInfo;

namespace Drocsid.HenrikDennis2025.RegistryService;

public class DrocsidClient : IDrocsidClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly DrocsidClientOptions _options;
        private readonly ILogger<DrocsidClient> _logger;
        private readonly SemaphoreSlim _semaphore;
        
        // Cache of active nodes
        private List<NodeInfo> _activeNodes = new List<NodeInfo>();
        private readonly SemaphoreSlim _nodeListLock = new SemaphoreSlim(1, 1);
        private DateTime _lastNodeRefresh = DateTime.MinValue;

        public DrocsidClient(
            IHttpClientFactory httpClientFactory,
            IOptions<DrocsidClientOptions> options,
            ILogger<DrocsidClient> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize the semaphore for parallel transfers
            _semaphore = new SemaphoreSlim(_options.MaxParallelTransfers, _options.MaxParallelTransfers);
        }

        public async Task<string> UploadFileAsync(string filePath, IEnumerable<string> tags = null, int replicationFactor = 3)
        {
            // First, get a list of active nodes
            await RefreshNodeListIfNeededAsync();
            
            if (_activeNodes.Count == 0)
            {
                throw new InvalidOperationException("No active storage nodes available");
            }
            
            // Select nodes for upload (prioritizing preferred region if specified)
            var selectedNodes = SelectNodesForOperation(replicationFactor);
            
            if (selectedNodes.Count < 1)
            {
                throw new InvalidOperationException("Could not find suitable nodes for upload");
            }
            
            // Create a unique file ID
            string fileId = Guid.NewGuid().ToString();
            
            // Calculate file metadata
            var fileInfo = new FileInfo
            {
                Id = fileId,
                Filename = Path.GetFileName(filePath),
                Size = new System.IO.FileInfo(filePath).Length,
                ContentType = GetMimeTypeFromFileName(filePath),
                Tags = tags != null ? new List<string>(tags) : new List<string>()
            };
            
            try
            {
                // Register the file with the registry service
                await RegisterFileWithRegistryAsync(fileInfo, selectedNodes);
                
                // Upload the file to the selected nodes
                await UploadFileToNodesAsync(filePath, fileInfo, selectedNodes);
                
                return fileId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {Filename}", fileInfo.Filename);
                
                // Try to clean up the partially uploaded file
                try
                {
                    await DeleteFileAsync(fileId);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Error cleaning up partially uploaded file {FileId}", fileId);
                }
                
                throw;
            }
        }

        public async Task<bool> DownloadFileAsync(string fileId, string destinationPath)
        {
            try
            {
                // Get file information
                var fileInfo = await GetFileInfoAsync(fileId);
                if (fileInfo == null)
                {
                    _logger.LogWarning("File not found: {FileId}", fileId);
                    return false;
                }
                
                // Get nodes that have this file
                var nodes = await GetNodesForFileAsync(fileId);
                if (nodes.Count == 0)
                {
                    _logger.LogWarning("No nodes have file: {FileId}", fileId);
                    return false;
                }
                
                // Try to download from the best node
                var selectedNode = SelectBestNodeForDownload(nodes);
                
                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Download the file
                await _semaphore.WaitAsync();
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var url = $"{selectedNode.Endpoint.TrimEnd('/')}/api/files/{fileId}/content";
                    
                    using var response = await client.GetStreamAsync(url);
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                    
                    await response.CopyToAsync(fileStream);
                    
                    _logger.LogInformation("Downloaded file {FileId} to {DestinationPath}", fileId, destinationPath);
                    return true;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileId}", fileId);
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            try
            {
                // Get nodes that have this file
                var nodes = await GetNodesForFileAsync(fileId);
                
                // Delete from each node
                var deleteTasks = new List<Task<bool>>();
                foreach (var node in nodes)
                {
                    deleteTasks.Add(DeleteFileFromNodeAsync(fileId, node));
                }
                
                // Wait for all delete operations to complete
                var results = await Task.WhenAll(deleteTasks);
                
                // Delete from registry
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.RegistryUrl.TrimEnd('/')}/api/registry/files/{fileId}";
                var response = await client.DeleteAsync(url);
                
                bool success = response.IsSuccessStatusCode && results.All(r => r);
                
                if (success)
                {
                    _logger.LogInformation("Successfully deleted file {FileId}", fileId);
                }
                else
                {
                    _logger.LogWarning("Partial failure deleting file {FileId}", fileId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId}", fileId);
                return false;
            }
        }

        public async Task<IEnumerable<FileInfo>> ListFilesAsync(string filenamePattern = null, IEnumerable<string> tags = null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.RegistryUrl.TrimEnd('/')}/api/registry/files";
                
                // Add query parameters if needed
                if (!string.IsNullOrEmpty(filenamePattern))
                {
                    url += $"?filename={Uri.EscapeDataString(filenamePattern)}";
                }
                
                if (tags != null && tags.Any())
                {
                    var tagsParam = string.Join(",", tags);
                    url += url.Contains("?") ? $"&tags={Uri.EscapeDataString(tagsParam)}" : $"?tags={Uri.EscapeDataString(tagsParam)}";
                }
                
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to list files. Status code: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<FileInfo>();
                }
                
                var files = await response.Content.ReadFromJsonAsync<List<FileInfo>>();
                return files ?? Enumerable.Empty<FileInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                return Enumerable.Empty<FileInfo>();
            }
        }

        public async Task<FileInfo> GetFileInfoAsync(string fileId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.RegistryUrl.TrimEnd('/')}/api/registry/files/{fileId}";
                
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("File not found: {FileId}", fileId);
                        return null;
                    }
                    
                    _logger.LogWarning("Failed to get file info. Status code: {StatusCode}", response.StatusCode);
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<FileInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file info for {FileId}", fileId);
                return null;
            }
        }

        // Private helper methods
        
        private async Task RefreshNodeListIfNeededAsync()
        {
            // Check if we need to refresh the node list
            var now = DateTime.UtcNow;
            if (_lastNodeRefresh.Add(_options.NodeRefreshInterval) > now && _activeNodes.Count > 0)
            {
                return;
            }
            
            await _nodeListLock.WaitAsync();
            try
            {
                // Double-check after acquiring the lock
                if (_lastNodeRefresh.Add(_options.NodeRefreshInterval) > now && _activeNodes.Count > 0)
                {
                    return;
                }
                
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.RegistryUrl.TrimEnd('/')}/api/nodes";
                
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get node list. Status code: {StatusCode}", response.StatusCode);
                    return;
                }
                
                var nodes = await response.Content.ReadFromJsonAsync<List<NodeInfo>>();
                if (nodes != null)
                {
                    _activeNodes = nodes;
                    _lastNodeRefresh = now;
                    _logger.LogInformation("Refreshed node list. {NodeCount} active nodes", _activeNodes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing node list");
            }
            finally
            {
                _nodeListLock.Release();
            }
        }
        
        private List<NodeInfo> SelectNodesForOperation(int count)
        {
            var result = new List<NodeInfo>();
            
            // Only consider healthy nodes
            var healthyNodes = _activeNodes.Where(n => n.IsHealthy).ToList();
            
            // First, try to get nodes in the preferred region
            if (!string.IsNullOrEmpty(_options.PreferredRegion))
            {
                var regionNodes = healthyNodes
                    .Where(n => n.Region == _options.PreferredRegion)
                    .OrderByDescending(n => n.AvailableSpace)
                    .Take(count)
                    .ToList();
                
                result.AddRange(regionNodes);
            }
            
            // If we still need more nodes, get nodes from any region
            if (result.Count < count)
            {
                var otherNodes = healthyNodes
                    .Where(n => !result.Contains(n))
                    .OrderByDescending(n => n.AvailableSpace)
                    .Take(count - result.Count)
                    .ToList();
                
                result.AddRange(otherNodes);
            }
            
            return result;
        }
        
        private NodeInfo SelectBestNodeForDownload(List<NodeInfo> nodes)
        {
            // Filter to only healthy nodes
            var healthyNodes = nodes.Where(n => n.IsHealthy).ToList();
            if (!healthyNodes.Any())
            {
                // If no healthy nodes, return any node (will likely fail, but at least we try)
                return nodes.FirstOrDefault();
            }
            
            // For download, prioritize nodes in the preferred region
            if (!string.IsNullOrEmpty(_options.PreferredRegion))
            {
                var regionNode = healthyNodes.FirstOrDefault(n => n.Region == _options.PreferredRegion);
                if (regionNode != null)
                {
                    return regionNode;
                }
            }
            
            // Otherwise, use a node with low load
            return healthyNodes
                .OrderBy(n => n.CurrentLoad)
                .FirstOrDefault() ?? healthyNodes.First();
        }
        
        private async Task RegisterFileWithRegistryAsync(FileInfo fileInfo, List<NodeInfo> selectedNodes)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{_options.RegistryUrl.TrimEnd('/')}/api/registry/files/register";
            
            // Create the file registration payload
            var fileRegistration = new
            {
                FileId = fileInfo.Id,
                FileName = fileInfo.Filename,
                fileInfo.Size,
                fileInfo.ContentType,
                NodeIds = selectedNodes.Select(n => n.Id).ToList(),
                Checksum = fileInfo.Checksum ?? "",
                Metadata = new Dictionary<string, string>()
            };
            
            var response = await client.PostAsJsonAsync(url, fileRegistration);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to register file with registry. Status code: {response.StatusCode}");
            }
            
            _logger.LogInformation("Registered file {FileId} in registry", fileInfo.Id);
        }
        
        private async Task UploadFileToNodesAsync(string filePath, FileInfo fileInfo, List<NodeInfo> nodes)
        {
            var uploadTasks = new List<Task>();
            
            foreach (var node in nodes)
            {
                uploadTasks.Add(UploadFileToNodeAsync(filePath, fileInfo, node));
            }
            
            await Task.WhenAll(uploadTasks);
        }
        
        private async Task UploadFileToNodeAsync(string filePath, FileInfo fileInfo, NodeInfo node)
        {
            await _semaphore.WaitAsync();
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{node.Endpoint.TrimEnd('/')}/api/files";
                
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var content = new MultipartFormDataContent
                {
                    { new StreamContent(fileStream), "file", fileInfo.Filename },
                    { new StringContent(fileInfo.Id), "id" }
                };
                
                var response = await client.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to upload file to node {node.Id}. Status code: {response.StatusCode}");
                }
                
                _logger.LogInformation("Uploaded file {FileId} to node {NodeId}", fileInfo.Id, node.Id);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        private async Task<List<NodeInfo>> GetNodesForFileAsync(string fileId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.RegistryUrl.TrimEnd('/')}/api/registry/files/{fileId}/locations";
                
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get nodes for file {FileId}. Status code: {StatusCode}", fileId, response.StatusCode);
                    return new List<NodeInfo>();
                }
                
                var result = await response.Content.ReadFromJsonAsync<FileLocationResponse>();
                if (result == null || result.Locations == null)
                {
                    return new List<NodeInfo>();
                }
                
                // Convert location info to NodeInfo
                return result.Locations.Select(loc => new NodeInfo
                {
                    Id = loc.NodeId,
                    Endpoint = loc.Endpoint,
                    Region = loc.Region,
                    IsHealthy = true, // We only get healthy nodes from the API
                    AvailableSpace = 0, // Not provided in location response
                    CurrentLoad = 0     // Not provided in location response
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nodes for file {FileId}", fileId);
                return new List<NodeInfo>();
            }
        }
        
        private async Task<bool> DeleteFileFromNodeAsync(string fileId, NodeInfo node)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{node.Endpoint.TrimEnd('/')}/api/files/{fileId}";
                
                var response = await client.DeleteAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to delete file {FileId} from node {NodeId}. Status code: {StatusCode}",
                        fileId, node.Id, response.StatusCode);
                    return false;
                }
                
                _logger.LogInformation("Deleted file {FileId} from node {NodeId}", fileId, node.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId} from node {NodeId}", fileId, node.Id);
                return false;
            }
        }
        
        private string GetMimeTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".doc" or ".docx" => "application/msword",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }
    }