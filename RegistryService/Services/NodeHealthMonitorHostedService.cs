using Drocsid.HenrikDennis2025.Core.Interfaces;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Background service that periodically calls the node health monitor
/// </summary>
public class NodeHealthMonitorHostedService : BackgroundService
{
    private readonly INodeHealthMonitor _monitor;
    private readonly ILogger<NodeHealthMonitorHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public NodeHealthMonitorHostedService(
        INodeHealthMonitor monitor, 
        ILogger<NodeHealthMonitorHostedService> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Node health monitoring service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _monitor.CheckNodeHealthAsync();
                await _monitor.EnsureReplicationFactorAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during node health monitoring");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
            
        _logger.LogInformation("Node health monitoring service stopped");
    }
}