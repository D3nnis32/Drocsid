using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Service that runs at startup to verify and ensure database is properly initialized
/// </summary>
public class DatabaseIntegrationTestService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseIntegrationTestService> _logger;
    private readonly IWebHostEnvironment _environment;

    public DatabaseIntegrationTestService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseIntegrationTestService> logger,
        IWebHostEnvironment environment)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DatabaseIntegrationTestService starting - checking database setup");
        
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();

        try
        {
            _logger.LogInformation("Testing database connection...");
            bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            
            if (!canConnect)
            {
                _logger.LogCritical("Cannot connect to the database! Check connection string and ensure PostgreSQL is running");
                return;
            }
            
            _logger.LogInformation("Database connection successful");
            
            // Check if tables exist by doing a count query
            _logger.LogInformation("Checking if tables exist...");
            
            bool tablesExist = false;
            try
            {
                // Try to access the Nodes table - if it doesn't exist, this will throw an exception
                var nodeCount = await dbContext.Nodes.CountAsync(cancellationToken);
                _logger.LogInformation("Found {Count} nodes in the database", nodeCount);
                tablesExist = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nodes table does not exist. Database needs initialization");
            }
            
            if (!tablesExist)
            {
                _logger.LogInformation("Database does not have required tables. Creating schema...");
                
                // Force creation of the database
                try
                {
                    // Ensure we have a clean database
                    await dbContext.Database.EnsureDeletedAsync(cancellationToken);
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                    
                    _logger.LogInformation("Database schema created successfully");
                    
                    // Add test data for verification
                    _logger.LogInformation("Adding test node to verify schema...");
                    
                    var testNode = new StorageNode
                    {
                        Id = "test-node-1",
                        Hostname = "test-host",
                        Endpoint = "http://test-endpoint:5000",
                        ApiKey = "test-api-key",
                        Region = "test-region",
                        TotalStorage = 1000000,
                        Status = new NodeStatus
                        {
                            IsHealthy = true,
                            AvailableSpace = 900000,
                            CurrentLoad = 10,
                            LastUpdated = DateTime.UtcNow
                        },
                        LastSeen = DateTime.UtcNow,
                        Tags = new List<string> { "test" },
                        Metadata = new Dictionary<string, string> { { "test", "value" } }
                    };
                    
                    await dbContext.Nodes.AddAsync(testNode, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    
                    _logger.LogInformation("Test node added successfully. Database is ready.");
                    
                    // Verify tables were created properly
                    _logger.LogInformation("Verifying table creation...");
                    var tables = await GetTableNamesAsync(dbContext, cancellationToken);
                    _logger.LogInformation("Tables in database: {Tables}", string.Join(", ", tables));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating database schema");
                    throw;
                }
            }
            else
            {
                _logger.LogInformation("Database schema already exists. No initialization needed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error in database initialization");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DatabaseIntegrationTestService stopping");
        return Task.CompletedTask;
    }
    
    private async Task<List<string>> GetTableNamesAsync(RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Database
            .SqlQuery<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(cancellationToken);
    }
}