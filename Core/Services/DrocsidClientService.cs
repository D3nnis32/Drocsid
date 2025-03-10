using System.Net.Http.Headers;
using System.Net.Http.Json;
using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using FileInfo = Drocsid.HenrikDennis2025.Core.Models.FileInfo;

namespace Drocsid.HenrikDennis2025.Core.Services
{
    /// <summary>
    /// Client implementation for interacting with the Drocsid distributed system
    /// Handles communication with both storage nodes and registry service
    /// </summary>
    public class DrocsidClientService : IDrocsidClient
    {
        private readonly HttpClient _httpClient;
        private string _registryUrl = "http://localhost:5261"; // Default registry URL
        private string _currentNodeUrl = string.Empty;
        private int _maxRetries = 3;

        public event EventHandler<string> NodeRedirected;
        public event EventHandler<string> ConnectionError;

        public DrocsidClientService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public void Configure(string registryUrl, string nodeUrl)
        {
            _registryUrl = registryUrl.TrimEnd('/');
            _currentNodeUrl = nodeUrl.TrimEnd('/');
            Console.WriteLine($"DrocsidClient configured with registry: {_registryUrl}, node: {_currentNodeUrl}");
        }

        /// <summary>
        /// Uploads a file to the distributed storage system
        /// </summary>
        public async Task<string> UploadFileAsync(string filePath, IEnumerable<string> tags = null, int replicationFactor = 3)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    using var content = new MultipartFormDataContent();
                    
                    // Add the file content
                    var fileContent = new StreamContent(fileStream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeTypeFromFileName(filePath));
                    content.Add(fileContent, "file", Path.GetFileName(filePath));
                    
                    // Upload to the current node
                    var response = await _httpClient.PostAsync($"{_currentNodeUrl}/api/files", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var attachment = await response.Content.ReadFromJsonAsync<Attachment>();
                        Console.WriteLine($"File uploaded successfully. ID: {attachment.Id}");
                        return attachment.Id.ToString();
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // Token might have expired, try to request node reassignment
                        if (await RequestNodeReassignmentAsync("AUTH_FAILURE"))
                        {
                            // Retry with new node/token
                            continue;
                        }
                    }
                    else if (IsNodeFailureStatusCode(response.StatusCode))
                    {
                        // Node might be failing, try to get reassigned
                        if (await RequestNodeReassignmentAsync("NODE_FAILURE"))
                        {
                            // Retry with new node
                            continue;
                        }
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error uploading file: {response.StatusCode}, {errorContent}");
                    throw new Exception($"Failed to upload file: {response.StatusCode}");
                }
                catch (HttpRequestException ex)
                {
                    // Network-level issues
                    Console.WriteLine($"Network error: {ex.Message}");
                    
                    // Try to get reassigned to a new node
                    if (await RequestNodeReassignmentAsync("NODE_FAILURE") && attempt < _maxRetries - 1)
                    {
                        // Continue to retry with the new node
                        continue;
                    }
                    
                    throw new Exception("Failed to connect to storage node", ex);
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    // For other exceptions, retry if we have attempts left
                    Console.WriteLine($"Error during upload (attempt {attempt+1}): {ex.Message}");
                    await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                    continue;
                }
            }
            
            throw new Exception("Failed to upload file after multiple attempts");
        }

        /// <summary>
        /// Downloads a file from the distributed storage system
        /// </summary>
        public async Task<bool> DownloadFileAsync(string fileId, string destinationPath)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                    
                    // First, try to get the file directly from current node
                    var response = await _httpClient.GetAsync($"{_currentNodeUrl}/api/files/{fileId}/content");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            // File not found on current node, try to find it via registry
                            var fileLocationResponse = await _httpClient.GetAsync($"{_registryUrl}/api/gateway/files/{fileId}/location");
                            
                            if (fileLocationResponse.IsSuccessStatusCode)
                            {
                                var locationInfo = await fileLocationResponse.Content.ReadFromJsonAsync<FileLocationInfo>();
                                
                                // Redirect to the node that has the file
                                response = await _httpClient.GetAsync($"{locationInfo.NodeEndpoint}/api/files/{fileId}/content");
                                
                                if (!response.IsSuccessStatusCode)
                                {
                                    throw new Exception($"Failed to download file from redirected node: {response.StatusCode}");
                                }
                            }
                            else
                            {
                                // File not found anywhere in the system
                                throw new Exception("File not found in the system");
                            }
                        }
                        else if (IsNodeFailureStatusCode(response.StatusCode))
                        {
                            // Node might be failing, try to get reassigned
                            if (await RequestNodeReassignmentAsync("NODE_FAILURE"))
                            {
                                // Retry with new node
                                continue;
                            }
                        }
                    }
                    
                    // Ensure destination directory exists
                    var directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Save the file
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                    
                    return true;
                }
                catch (HttpRequestException ex)
                {
                    // Network-level issues
                    Console.WriteLine($"Network error: {ex.Message}");
                    
                    // Try to get reassigned to a new node
                    if (await RequestNodeReassignmentAsync("NODE_FAILURE") && attempt < _maxRetries - 1)
                    {
                        // Continue to retry with the new node
                        continue;
                    }
                    
                    throw new Exception("Failed to connect to storage node", ex);
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    // For other exceptions, retry if we have attempts left
                    Console.WriteLine($"Error during download (attempt {attempt+1}): {ex.Message}");
                    await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                    continue;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Deletes a file from the distributed storage system
        /// </summary>
        public async Task<bool> DeleteFileAsync(string fileId)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                    
                    // Delete from current node
                    var response = await _httpClient.DeleteAsync($"{_currentNodeUrl}/api/files/{fileId}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else if (IsNodeFailureStatusCode(response.StatusCode))
                    {
                        // Node might be failing, try to get reassigned
                        if (await RequestNodeReassignmentAsync("NODE_FAILURE"))
                        {
                            // Retry with new node
                            continue;
                        }
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error deleting file: {response.StatusCode}, {errorContent}");
                    return false;
                }
                catch (HttpRequestException ex)
                {
                    // Network-level issues
                    Console.WriteLine($"Network error: {ex.Message}");
                    
                    // Try to get reassigned to a new node
                    if (await RequestNodeReassignmentAsync("NODE_FAILURE") && attempt < _maxRetries - 1)
                    {
                        // Continue to retry with the new node
                        continue;
                    }
                    
                    throw new Exception("Failed to connect to storage node", ex);
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    // For other exceptions, retry if we have attempts left
                    Console.WriteLine($"Error during file deletion (attempt {attempt+1}): {ex.Message}");
                    await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                    continue;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Lists files matching certain criteria
        /// </summary>
        public async Task<IEnumerable<FileInfo>> ListFilesAsync(string filenamePattern = null, IEnumerable<string> tags = null)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                    
                    // Build the query string
                    var queryParams = new List<string>();
                    if (!string.IsNullOrEmpty(filenamePattern))
                    {
                        queryParams.Add($"filename={Uri.EscapeDataString(filenamePattern)}");
                    }
                    
                    if (tags != null)
                    {
                        foreach (var tag in tags)
                        {
                            queryParams.Add($"tags={Uri.EscapeDataString(tag)}");
                        }
                    }
                    
                    var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                    var response = await _httpClient.GetAsync($"{_registryUrl}/api/gateway/files{queryString}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var files = await response.Content.ReadFromJsonAsync<List<FileInfo>>();
                        return files;
                    }
                    else if (IsNodeFailureStatusCode(response.StatusCode))
                    {
                        // Registry might be failing, retry
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error listing files: {response.StatusCode}, {errorContent}");
                    return new List<FileInfo>();
                }
                catch (HttpRequestException ex)
                {
                    // Network-level issues
                    Console.WriteLine($"Network error: {ex.Message}");
                    
                    if (attempt < _maxRetries - 1)
                    {
                        await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                        continue;
                    }
                    
                    throw new Exception("Failed to connect to registry service", ex);
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    // For other exceptions, retry if we have attempts left
                    Console.WriteLine($"Error listing files (attempt {attempt+1}): {ex.Message}");
                    await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                    continue;
                }
            }
            
            return new List<FileInfo>();
        }

        /// <summary>
        /// Gets information about a specific file
        /// </summary>
        public async Task<FileInfo> GetFileInfoAsync(string fileId)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                    
                    // Try to get file info from current node first
                    var response = await _httpClient.GetAsync($"{_currentNodeUrl}/api/files/{fileId}/info");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadFromJsonAsync<FileInfo>();
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // If not found on current node, try registry
                        var registryResponse = await _httpClient.GetAsync($"{_registryUrl}/api/gateway/files/{fileId}/info");
                        
                        if (registryResponse.IsSuccessStatusCode)
                        {
                            return await registryResponse.Content.ReadFromJsonAsync<FileInfo>();
                        }
                        else
                        {
                            // File not found anywhere
                            return null;
                        }
                    }
                    else if (IsNodeFailureStatusCode(response.StatusCode))
                    {
                        // Node might be failing, try to get reassigned
                        if (await RequestNodeReassignmentAsync("NODE_FAILURE"))
                        {
                            // Retry with new node
                            continue;
                        }
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error getting file info: {response.StatusCode}, {errorContent}");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    // Network-level issues
                    Console.WriteLine($"Network error: {ex.Message}");
                    
                    // Try to get reassigned to a new node
                    if (await RequestNodeReassignmentAsync("NODE_FAILURE") && attempt < _maxRetries - 1)
                    {
                        // Continue to retry with the new node
                        continue;
                    }
                    
                    throw new Exception("Failed to connect to storage node", ex);
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    // For other exceptions, retry if we have attempts left
                    Console.WriteLine($"Error getting file info (attempt {attempt+1}): {ex.Message}");
                    await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                    continue;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Requests reassignment to a new node when the current one fails
        /// </summary>
        private async Task<bool> RequestNodeReassignmentAsync(string reason)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                
                var request = new NodeReassignmentRequest
                {
                    CurrentNodeId = ExtractNodeIdFromUrl(_currentNodeUrl),
                    Reason = reason
                };
                
                var response = await _httpClient.PostAsJsonAsync($"{_registryUrl}/api/gateway/reassign", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var reassignmentInfo = await response.Content.ReadFromJsonAsync<NodeReassignmentResponse>();
                    
                    // Update current node
                    var oldNodeUrl = _currentNodeUrl;
                    _currentNodeUrl = reassignmentInfo.NodeEndpoint;
                    
                    // Update token if a new one was provided
                    if (!string.IsNullOrEmpty(reassignmentInfo.Token))
                    {
                        TokenStorage.JwtToken = reassignmentInfo.Token;
                    }
                    
                    Console.WriteLine($"Node reassignment successful. New node: {_currentNodeUrl}");
                    
                    // Notify subscriber of redirect
                    NodeRedirected?.Invoke(this, _currentNodeUrl);
                    
                    // Notify the chat hub about the redirect if connected
                    await NotifyChatHubAboutRedirect(oldNodeUrl, _currentNodeUrl);
                    
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error requesting node reassignment: {response.StatusCode}, {errorContent}");
                    
                    // Notify subscribers of connection error
                    ConnectionError?.Invoke(this, $"Failed to find an available node: {response.StatusCode}");
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during node reassignment: {ex.Message}");
                
                // Notify subscribers of connection error
                ConnectionError?.Invoke(this, $"Connection error: {ex.Message}");
                
                return false;
            }
        }

        /// <summary>
        /// Notifies the chat hub about a redirect
        /// </summary>
        private async Task NotifyChatHubAboutRedirect(string oldNodeUrl, string newNodeUrl)
        {
            try
            {
                // Use the instance of SignalR hub connection from wherever it's maintained
                // This is highly dependent on how your chat connection is implemented
                // For example:
                // await _hubConnection.SendAsync("NotifyRedirecting", ExtractNodeIdFromUrl(newNodeUrl));
                
                // In this implementation, we leave it to the UI layer to handle this
                Console.WriteLine($"Need to notify chat hub about redirect from {oldNodeUrl} to {newNodeUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying chat hub about redirect: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts the node ID from a node URL
        /// </summary>
        private string ExtractNodeIdFromUrl(string nodeUrl)
        {
            // This is a simple implementation that assumes node IDs are encoded in the URL
            // You might need to adjust this based on your actual URL structure
            try
            {
                var uri = new Uri(nodeUrl);
                var parts = uri.Host.Split('.');
                if (parts.Length > 0 && parts[0].StartsWith("node"))
                {
                    return parts[0];
                }
                
                // Fallback: Look for a node parameter
                var query = uri.Query;
                if (query.Contains("node="))
                {
                    var nodeParam = query.Split(new[] { "node=" }, StringSplitOptions.None)[1];
                    return nodeParam.Split('&')[0];
                }
                
                // Fallback to host if we can't find a node ID
                return uri.Host;
            }
            catch
            {
                // If we can't parse it, return the URL as is
                return nodeUrl;
            }
        }

        /// <summary>
        /// Determines if a status code indicates a node failure
        /// </summary>
        private bool IsNodeFailureStatusCode(System.Net.HttpStatusCode statusCode)
        {
            return statusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                   statusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                   statusCode == System.Net.HttpStatusCode.BadGateway ||
                   statusCode == System.Net.HttpStatusCode.InternalServerError;
        }

        /// <summary>
        /// Gets the MIME type from a filename based on its extension
        /// </summary>
        private string GetMimeTypeFromFileName(string fileName)
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
                _ => "application/octet-stream" // Default content type
            };
        }
    }
}