using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.Server.Services;

public class FileTransferService : IFileTransferService
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly IFileRegistry _fileRegistry;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FileTransferService> _logger;
        private readonly FileTransferOptions _options;

        public FileTransferService(
            INodeRegistry nodeRegistry,
            IFileRegistry fileRegistry,
            IHttpClientFactory httpClientFactory,
            IOptions<FileTransferOptions> options,
            ILogger<FileTransferService> logger)
        {
            _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
            _fileRegistry = fileRegistry ?? throw new ArgumentNullException(nameof(fileRegistry));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Transfers a file from a source node to a target node, with fallback to alternative sources if needed
        /// </summary>
        public async Task<bool> TransferFileAsync(string fileId, string sourceNodeId, string targetNodeId)
        {
            try
            {
                _logger.LogInformation("Starting file transfer for file {FileId} from node {SourceNodeId} to node {TargetNodeId}", 
                    fileId, sourceNodeId, targetNodeId);

                // Get file details
                var file = await _fileRegistry.GetFileAsync(fileId);
                if (file == null)
                {
                    _logger.LogError("File {FileId} not found in registry", fileId);
                    return false;
                }

                // Verify target node exists and is healthy
                var targetNode = await _nodeRegistry.GetNodeAsync(targetNodeId);
                if (targetNode == null || !targetNode.Status.IsHealthy)
                {
                    _logger.LogError("Target node {TargetNodeId} is unavailable or unhealthy", targetNodeId);
                    return false;
                }

                // Check if target node already has the file
                if (file.NodeLocations.Contains(targetNodeId))
                {
                    _logger.LogInformation("File {FileId} already exists on target node {TargetNodeId}", fileId, targetNodeId);
                    return true;
                }

                // Try to use specified source node
                var sourceNode = await _nodeRegistry.GetNodeAsync(sourceNodeId);
                if (sourceNode == null || !sourceNode.Status.IsHealthy)
                {
                    _logger.LogWarning("Primary source node {SourceNodeId} is unavailable, looking for alternative source", 
                        sourceNodeId);
                    
                    // Find an alternative source
                    sourceNode = await FindAlternativeSourceNodeAsync(file, sourceNodeId);
                    if (sourceNode == null)
                    {
                        _logger.LogError("No healthy source nodes available for file {FileId}", fileId);
                        return false;
                    }
                    
                    _logger.LogInformation("Selected alternative source node {NodeId} for file {FileId}", 
                        sourceNode.Id, fileId);
                }

                // Check source node has enough free network capacity
                if (sourceNode.Status.ActiveTransfers >= _options.MaxConcurrentTransfersPerNode)
                {
                    _logger.LogWarning("Source node {NodeId} has reached maximum concurrent transfers limit", 
                        sourceNode.Id);
                    
                    // Find another alternative if available
                    var alternativeSource = await FindAlternativeSourceNodeAsync(file, sourceNode.Id);
                    if (alternativeSource != null)
                    {
                        sourceNode = alternativeSource;
                        _logger.LogInformation("Selected less busy source node {NodeId} for file {FileId}", 
                            sourceNode.Id, fileId);
                    }
                    // Otherwise proceed with busy node
                }

                // Perform the actual file transfer
                bool success = await ExecuteFileTransferAsync(file, sourceNode, targetNode);
                
                if (success)
                {
                    // Update file registry with new location
                    await _fileRegistry.AddFileLocationAsync(fileId, targetNodeId);
                    
                    // Update storage statistics
                    await UpdateNodeStorageStatsAsync(targetNode.Id, file.Size);
                    
                    _logger.LogInformation("File {FileId} ({FileName}, {Size} bytes) successfully transferred to node {TargetNodeId}", 
                        fileId, file.Filename, file.Size, targetNodeId);
                    
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to transfer file {FileId} to node {TargetNodeId}", 
                        fileId, targetNodeId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file transfer for file {FileId}", fileId);
                return false;
            }
        }

        /// <summary>
        /// Finds an alternative healthy and available source node for a file
        /// </summary>
        private async Task<StorageNode> FindAlternativeSourceNodeAsync(StoredFile file, string excludeNodeId)
        {
            try
            {
                // Get all nodes that have this file, excluding the specified node
                var potentialSourceNodeIds = file.NodeLocations
                    .Where(nodeId => nodeId != excludeNodeId)
                    .ToList();

                if (!potentialSourceNodeIds.Any())
                {
                    _logger.LogWarning("No alternative nodes have file {FileId}", file.Id);
                    return null;
                }

                // Get node details for all potential source nodes
                var availableSourceNodes = new List<StorageNode>();
                foreach (var nodeId in potentialSourceNodeIds)
                {
                    var node = await _nodeRegistry.GetNodeAsync(nodeId);
                    if (node != null && node.Status.IsHealthy)
                    {
                        availableSourceNodes.Add(node);
                    }
                }

                if (!availableSourceNodes.Any())
                {
                    _logger.LogWarning("No healthy alternative source nodes for file {FileId}", file.Id);
                    return null;
                }

                // Select the node with the fewest active transfers and highest network capacity
                return availableSourceNodes
                    .OrderBy(n => n.Status.ActiveTransfers)
                    .ThenByDescending(n => n.Status.NetworkCapacity)
                    .First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding alternative source node for file {FileId}", file.Id);
                return null;
            }
        }

        /// <summary>
        /// Executes the actual file transfer between nodes
        /// </summary>
        private async Task<bool> ExecuteFileTransferAsync(StoredFile file, StorageNode sourceNode, StorageNode targetNode)
        {
            var httpClient = _httpClientFactory.CreateClient("NodeTransfer");
            httpClient.Timeout = _options.TransferTimeout;

            try
            {
                _logger.LogInformation("Beginning transfer of file {FileId} ({Size} bytes) from {SourceNode} to {TargetNode}", 
                    file.Id, file.Size, sourceNode.Hostname, targetNode.Hostname);

                // Increment active transfers count on both nodes
                await UpdateNodeTransferStatsAsync(sourceNode.Id, true);
                await UpdateNodeTransferStatsAsync(targetNode.Id, true);
                
                try
                {
                    // Step 1: Get file download URL from source node
                    var downloadUrlRequest = new HttpRequestMessage(HttpMethod.Get, 
                        $"{sourceNode.Endpoint}/api/storage/prepare-download/{file.Id}");
                    var authToken = GenerateAuthToken(sourceNode.ApiKey);
                    downloadUrlRequest.Headers.Add("Authorization", $"Bearer {authToken}");
                    
                    var downloadUrlResponse = await httpClient.SendAsync(downloadUrlRequest);
                    downloadUrlResponse.EnsureSuccessStatusCode();
                    
                    var downloadUrlData = await downloadUrlResponse.Content.ReadFromJsonAsync<DownloadUrlResponse>();
                    
                    // Step 2: Get upload URL from target node
                    var uploadUrlRequest = new HttpRequestMessage(HttpMethod.Post, 
                        $"{targetNode.Endpoint}/api/storage/prepare-upload");
                    authToken = GenerateAuthToken(targetNode.ApiKey);
                    uploadUrlRequest.Headers.Add("Authorization", $"Bearer {authToken}");
                    
                    var uploadMetadata = new UploadMetadata
                    {
                        FileId = file.Id,
                        Filename = file.Filename,
                        ContentType = file.ContentType,
                        Size = file.Size,
                        Checksum = file.Checksum,
                        Metadata = file.Metadata
                    };
                    
                    uploadUrlRequest.Content = new StringContent(
                        JsonSerializer.Serialize(uploadMetadata),
                        Encoding.UTF8,
                        "application/json");
                    
                    var uploadUrlResponse = await httpClient.SendAsync(uploadUrlRequest);
                    uploadUrlResponse.EnsureSuccessStatusCode();
                    
                    var uploadUrlData = await uploadUrlResponse.Content.ReadFromJsonAsync<UploadUrlResponse>();
                    
                    // Step 3: Download the file from source node
                    var fileDownloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrlData.DownloadUrl);
                    using var downloadResponse = await httpClient.SendAsync(fileDownloadRequest, HttpCompletionOption.ResponseHeadersRead);
                    downloadResponse.EnsureSuccessStatusCode();
                    
                    using var fileStream = await downloadResponse.Content.ReadAsStreamAsync();
                    
                    // Step 4: Upload the file to target node
                    var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrlData.UploadUrl);
                    uploadRequest.Content = new StreamContent(fileStream);
                    
                    if (!string.IsNullOrEmpty(file.ContentType))
                    {
                        uploadRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    }
                    
                    var uploadResponse = await httpClient.SendAsync(uploadRequest);
                    uploadResponse.EnsureSuccessStatusCode();
                    
                    // Step 5: Confirm upload on target node
                    var confirmRequest = new HttpRequestMessage(HttpMethod.Post, 
                        $"{targetNode.Endpoint}/api/storage/confirm-upload/{uploadUrlData.UploadId}");
                    confirmRequest.Headers.Add("Authorization", $"Bearer {authToken}");
                    
                    var confirmResponse = await httpClient.SendAsync(confirmRequest);
                    confirmResponse.EnsureSuccessStatusCode();
                    
                    return true;
                }
                finally
                {
                    // Decrement active transfers count regardless of outcome
                    await UpdateNodeTransferStatsAsync(sourceNode.Id, false);
                    await UpdateNodeTransferStatsAsync(targetNode.Id, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file transfer execution for file {FileId}", file.Id);
                return false;
            }
        }

        /// <summary>
        /// Updates the node's active transfer count
        /// </summary>
        private async Task UpdateNodeTransferStatsAsync(string nodeId, bool isStarting)
        {
            try
            {
                var node = await _nodeRegistry.GetNodeAsync(nodeId);
                if (node != null)
                {
                    // Update active transfers count
                    if (isStarting)
                    {
                        node.Status.ActiveTransfers++;
                    }
                    else
                    {
                        node.Status.ActiveTransfers = Math.Max(0, node.Status.ActiveTransfers - 1);
                    }
                    
                    // Update the node in the registry
                    await _nodeRegistry.UpdateNodeAsync(node);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transfer stats for node {NodeId}", nodeId);
            }
        }

        /// <summary>
        /// Updates the node's storage statistics after a successful transfer
        /// </summary>
        private async Task UpdateNodeStorageStatsAsync(string nodeId, long fileSize)
        {
            try
            {
                var node = await _nodeRegistry.GetNodeAsync(nodeId);
                if (node != null)
                {
                    // Update available space
                    node.Status.AvailableSpace -= fileSize;
                    node.Status.UsedSpace += fileSize;
                    
                    // Update the node in the registry
                    await _nodeRegistry.UpdateNodeAsync(node);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating storage stats for node {NodeId}", nodeId);
            }
        }

        /// <summary>
        /// Generates a simple auth token for node-to-node communication
        /// In a production system, you would use a more secure method
        /// </summary>
        private string GenerateAuthToken(string apiKey)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var data = $"{apiKey}:{timestamp}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }

        // Simple model classes for API responses
        private class DownloadUrlResponse
        {
            public string DownloadUrl { get; set; }
            public int ExpiresInSeconds { get; set; }
        }

        private class UploadUrlResponse
        {
            public string UploadUrl { get; set; }
            public string UploadId { get; set; }
            public int ExpiresInSeconds { get; set; }
        }

        private class UploadMetadata
        {
            public string FileId { get; set; }
            public string Filename { get; set; }
            public string ContentType { get; set; }
            public long Size { get; set; }
            public string Checksum { get; set; }
            public Dictionary<string, string> Metadata { get; set; }
        }
    }