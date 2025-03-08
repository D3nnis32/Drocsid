using Drocsid.HenrikDennis2025.Core.Interfaces;

namespace Drocsid.HenrikDennis2025.RegistryService;

// This helper class handles creating scoped instances of services for background processing
public class BackgroundServiceHost<TService, TImplementation> : BackgroundService
    where TService : class
    where TImplementation : class, TService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundServiceHost<TService, TImplementation>> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public BackgroundServiceHost(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundServiceHost<TService, TImplementation>> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{typeof(TService).Name} host service starting");

        DateTime lastReplicationCheck = DateTime.MinValue;
        TimeSpan replicationCheckInterval = TimeSpan.FromMinutes(15);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var service = scope.ServiceProvider.GetRequiredService<TService>();
                    
                    if (service is INodeHealthMonitor healthMonitor)
                    {
                        // Always check node health
                        await healthMonitor.CheckNodeHealthAsync();
                            
                        // Check replication factor less frequently
                        var now = DateTime.UtcNow;
                        if (now - lastReplicationCheck > replicationCheckInterval)
                        {
                            await healthMonitor.EnsureReplicationFactorAsync();
                            lastReplicationCheck = now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in {typeof(TService).Name} host service");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation($"{typeof(TService).Name} host service stopping");
    }
}