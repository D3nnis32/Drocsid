using System.Diagnostics;
using System.Runtime.InteropServices;
using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.Server.Services;

/// <summary>
/// Client service that registers this node with the registry service and sends periodic heartbeats
/// </summary>
public class NodeRegistrationClient : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeRegistrationOptions _options;
    private readonly ILogger<NodeRegistrationClient> _logger;
    private string _nodeId;
    private readonly string _hostname;
    private readonly long _totalStorage;
    private readonly string _nodeEndpoint;

    public NodeRegistrationClient(
        IHttpClientFactory httpClientFactory,
        IOptions<NodeRegistrationOptions> options,
        ILogger<NodeRegistrationClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _hostname = Environment.MachineName;
        _nodeEndpoint = _options.NodeEndpoint;
        
        // Calculate total storage
        if (!string.IsNullOrEmpty(_options.DataDirectory))
        {
            try
            {
                var directoryInfo = new DirectoryInfo(_options.DataDirectory);
                if (directoryInfo.Exists)
                {
                    var driveInfo = new DriveInfo(directoryInfo.Root.FullName);
                    _totalStorage = driveInfo.TotalSize;
                }
                else
                {
                    _totalStorage = unchecked(1024 * 1024 * 1024 * 50); // Default 50GB if directory doesn't exist yet
                    
                    // Create directory
                    Directory.CreateDirectory(_options.DataDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total storage for data directory");
                _totalStorage = unchecked(1024 * 1024 * 1024 * 50); // Default 50GB
            }
        }
        else
        {
            _totalStorage = unchecked(1024 * 1024 * 1024 * 50); // Default 50GB
        }
        
        // Generate a default node ID if not specified
        _nodeId = string.IsNullOrEmpty(_options.NodeId) ? Guid.NewGuid().ToString() : _options.NodeId;
    }

    /// <summary>
    /// Main execution loop for the background service
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Node registration service starting");

        try
        {
            // Register with the registry
            await RegisterNodeAsync(stoppingToken);
            
            // Start sending heartbeats
            using var timer = new PeriodicTimer(_options.HeartbeatInterval);
            
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SendHeartbeatAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping token was canceled, this is normal during shutdown
            _logger.LogInformation("Node registration service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in node registration service");
        }
    }

    /// <summary>
    /// Register this node with the registry service
    /// </summary>
    private async Task RegisterNodeAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Registering node with registry at {RegistryUrl}", _options.RegistryUrl);
            
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _options.RegistryUrl.TrimEnd('/');
            
            // Calculate available storage
            long availableStorage = CalculateAvailableStorage();
            
            // Create registration request
            var request = new NodeRegistrationRequest
            {
                Id = _nodeId,
                Hostname = _hostname,
                Endpoint = _nodeEndpoint,
                Region = _options.NodeRegion,
                TotalStorage = _totalStorage,
                AvailableStorage = availableStorage,
                Tags = _options.NodeTags
            };
            
            // Send registration request
            var response = await client.PostAsJsonAsync($"{registryUrl}/api/nodes/register", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<NodeRegistrationResponse>(cancellationToken: cancellationToken);
                
                if (result != null)
                {
                    // Update node ID from response (in case it was generated by the registry)
                    _nodeId = result.NodeId;
                    _logger.LogInformation("Node registered successfully with ID: {NodeId}", _nodeId);
                }
                else
                {
                    _logger.LogWarning("Node registration response was empty");
                }
            }
            else
            {
                _logger.LogError("Failed to register node. Status code: {StatusCode}", response.StatusCode);
                throw new InvalidOperationException($"Failed to register node. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering node with registry");
            throw;
        }
    }

    /// <summary>
    /// Send a heartbeat to the registry service
    /// </summary>
    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Sending heartbeat to registry");
            
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _options.RegistryUrl.TrimEnd('/');
            
            // Create node status
            var status = new NodeStatus
            {
                IsHealthy = true,
                CurrentLoad = CalculateCurrentLoad(),
                AvailableSpace = CalculateAvailableStorage(),
                ActiveConnections = CalculateActiveConnections(),
                LastUpdated = DateTime.UtcNow
            };
            
            // Create heartbeat request
            var request = new NodeHeartbeatRequest
            {
                Status = status
            };
            
            // Send heartbeat
            var response = await client.PutAsJsonAsync(
                $"{registryUrl}/api/nodes/{_nodeId}/heartbeat", 
                request, 
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send heartbeat. Status code: {StatusCode}", response.StatusCode);
                
                // If it's been too long since we registered, try to register again
                // This handles cases where the registry might have restarted or lost our registration
                await RegisterNodeAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat to registry");
        }
    }

    /// <summary>
    /// Calculate the available storage space
    /// </summary>
    private long CalculateAvailableStorage()
    {
        try
        {
            if (!string.IsNullOrEmpty(_options.DataDirectory))
            {
                var directoryInfo = new DirectoryInfo(_options.DataDirectory);
                if (directoryInfo.Exists)
                {
                    var driveInfo = new DriveInfo(directoryInfo.Root.FullName);
                    return driveInfo.AvailableFreeSpace;
                }
            }
            
            return unchecked(1024 * 1024 * 1024 * 25); // Default 25GB
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating available storage");
            return unchecked(1024 * 1024 * 1024 * 25); // Default 25GB
        }
    }

    /// <summary>
    /// Calculate the current CPU load
    /// </summary>
    private double CalculateCurrentLoad()
    {
        try
        {
            // This is a simplified approach - in a real implementation,
            // you would use platform-specific methods to get actual CPU usage
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var cpuTime = process.TotalProcessorTime;
            var uptime = DateTime.Now - process.StartTime;
            
            // Calculate CPU usage as a percentage (0-100)
            return Math.Min(cpuTime.TotalMilliseconds / (uptime.TotalMilliseconds * Environment.ProcessorCount) * 100, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating CPU load");
            return 0;
        }
    }

    /// <summary>
    /// Calculate the number of active connections
    /// </summary>
    private int CalculateActiveConnections()
    {
        // In a real implementation, you would track actual connection count
        // For now, we'll return a dummy value
        return 0;
    }
}