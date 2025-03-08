using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
    /// Background service that periodically runs node health checks and replication factor checks
    /// </summary>
    public class NodeHealthMonitorHostedService : BackgroundService
    {
        private readonly INodeHealthMonitor _monitor;
        private readonly ILogger<NodeHealthMonitorHostedService> _logger;
        private readonly NodeHealthMonitorOptions _options;

        public NodeHealthMonitorHostedService(
            INodeHealthMonitor monitor,
            IOptions<NodeHealthMonitorOptions> options,
            ILogger<NodeHealthMonitorHostedService> logger)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Node health monitoring service started");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Run node health check
                    try
                    {
                        await _monitor.CheckNodeHealthAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during node health check");
                    }

                    // Wait a short time between checks
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                    // Run replication factor check if not stopping
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            await _monitor.EnsureReplicationFactorAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during replication factor check");
                        }
                    }

                    // Wait for next check interval
                    await Task.Delay(_options.HealthCheckInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown, don't log as error
                _logger.LogInformation("Node health monitoring service shutting down");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in node health monitoring service");
                throw;
            }
            finally
            {
                _logger.LogInformation("Node health monitoring service stopped");
            }
        }
    }