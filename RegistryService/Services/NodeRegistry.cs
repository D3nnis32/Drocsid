using System.Text.Json;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

public class NodeRegistry : INodeRegistry
{
    private readonly RegistryDbContext _dbContext;
    private readonly ILogger<NodeRegistry> _logger;
    private readonly DbContextOptions<RegistryDbContext> _options;

    // Constructor for scoped service (normal usage)
    public NodeRegistry(RegistryDbContext dbContext, ILogger<NodeRegistry> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = null;
    }

    // Constructor for singleton service (health monitor)
    public NodeRegistry(DbContextOptions<RegistryDbContext> options, ILogger<NodeRegistry> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = null;
    }

    private (RegistryDbContext context, bool ownsContext) GetContext()
    {
        if (_dbContext != null)
        {
            return (_dbContext, false); // DI-provided context, don't dispose
        }
        else if (_options != null)
        {
            return (new RegistryDbContext(_options), true); // We created this, do dispose
        }
        
        throw new InvalidOperationException("No valid database context available");
    }

    public async Task<bool> RegisterNodeAsync(StorageNode node)
{
    try
    {
        NodeRegistryInitialization.EnsureJsonSerialization(node);
        
        var (context, ownsContext) = GetContext();
        try
        {
            var existingNode = await context.Nodes.FirstOrDefaultAsync(n => n.Id == node.Id);

            if (existingNode != null)
            {
                // Update existing node
                _logger.LogInformation("Updating existing node: {NodeId}", node.Id);
                
                // Ensure collections are initialized to prevent serialization issues
                if (node.Tags == null)
                    node.Tags = new List<string>();
                    
                if (node.Metadata == null)
                    node.Metadata = new Dictionary<string, string>();
                
                context.Entry(existingNode).CurrentValues.SetValues(node);
                existingNode.LastSeen = DateTime.UtcNow;
                
                // Make sure the node is marked as healthy
                if (existingNode.Status == null)
                {
                    existingNode.Status = new NodeStatus
                    {
                        IsHealthy = true,
                        CurrentLoad = 0,
                        AvailableSpace = 0,
                        ActiveConnections = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                }
                else
                {
                    existingNode.Status.IsHealthy = true;
                    existingNode.Status.LastUpdated = DateTime.UtcNow;
                }
                
                // For updates, use FormattableString to ensure parameters are properly typed
                await context.Database.ExecuteSqlRawAsync(
                    @"UPDATE ""Nodes"" SET 
                      ""Hostname"" = @hostname, 
                      ""Endpoint"" = @endpoint,
                      ""Region"" = @region,
                      ""LastSeen"" = @lastSeen,
                      ""TotalStorage"" = @totalStorage,
                      ""Metadata"" = @metadata::jsonb,
                      ""Tags"" = @tags::jsonb,
                      ""Status_IsHealthy"" = @isHealthy,
                      ""Status_CurrentLoad"" = @currentLoad,
                      ""Status_AvailableSpace"" = @availableSpace,
                      ""Status_ActiveConnections"" = @activeConnections,
                      ""Status_LastUpdated"" = @lastUpdated,
                      ""Status_ActiveTransfers"" = @activeTransfers,
                      ""Status_NetworkCapacity"" = @networkCapacity,
                      ""Status_UsedSpace"" = @usedSpace
                      WHERE ""Id"" = @id",
                    new NpgsqlParameter("@id", node.Id),
                    new NpgsqlParameter("@hostname", node.Hostname),
                    new NpgsqlParameter("@endpoint", node.Endpoint),
                    new NpgsqlParameter("@region", (object)node.Region ?? DBNull.Value),
                    new NpgsqlParameter("@lastSeen", node.LastSeen),
                    new NpgsqlParameter("@totalStorage", node.TotalStorage),
                    new NpgsqlParameter("@metadata", JsonSerializer.Serialize(node.Metadata)),
                    new NpgsqlParameter("@tags", JsonSerializer.Serialize(node.Tags)),
                    new NpgsqlParameter("@isHealthy", node.Status.IsHealthy),
                    new NpgsqlParameter("@currentLoad", node.Status.CurrentLoad),
                    new NpgsqlParameter("@availableSpace", node.Status.AvailableSpace),
                    new NpgsqlParameter("@activeConnections", node.Status.ActiveConnections), 
                    new NpgsqlParameter("@lastUpdated", node.Status.LastUpdated),
                    new NpgsqlParameter("@activeTransfers", node.Status.ActiveTransfers),
                    new NpgsqlParameter("@networkCapacity", node.Status.NetworkCapacity),
                    new NpgsqlParameter("@usedSpace", node.Status.UsedSpace)
                );
            }
            else
            {
                // Add new node with raw SQL to ensure proper JSONB casting
                _logger.LogInformation("Registering new node: {NodeId}", node.Id);
                
                // Ensure node has a valid ID
                if (string.IsNullOrEmpty(node.Id))
                {
                    node.Id = Guid.NewGuid().ToString();
                }
                
                // Ensure node has initialized collections
                if (node.Tags == null)
                    node.Tags = new List<string>();
                    
                if (node.Metadata == null)
                    node.Metadata = new Dictionary<string, string>();
                
                // Ensure node has a valid status
                if (node.Status == null)
                {
                    node.Status = new NodeStatus
                    {
                        IsHealthy = true,
                        CurrentLoad = 0,
                        AvailableSpace = 0,
                        ActiveConnections = 0,
                        LastUpdated = DateTime.UtcNow,
                        ActiveTransfers = 0,
                        NetworkCapacity = 1000,
                        UsedSpace = 0
                    };
                }
                
                // Use raw SQL with explicit casting for JSONB columns
                await context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO ""Nodes"" (""Id"", ""Hostname"", ""Endpoint"", ""Region"", 
                      ""LastSeen"", ""TotalStorage"", ""Metadata"", ""Tags"", 
                      ""Status_IsHealthy"", ""Status_CurrentLoad"", ""Status_AvailableSpace"", 
                      ""Status_ActiveConnections"", ""Status_LastUpdated"",
                      ""Status_ActiveTransfers"", ""Status_NetworkCapacity"", ""Status_UsedSpace"")
                    VALUES (@id, @hostname, @endpoint, @region,
                       @lastSeen, @totalStorage, @metadata::jsonb, @tags::jsonb,
                       @isHealthy, @currentLoad, @availableSpace,
                       @activeConnections, @lastUpdated,
                       @activeTransfers, @networkCapacity, @usedSpace)",
                    new NpgsqlParameter("@id", node.Id),
                    new NpgsqlParameter("@hostname", node.Hostname),
                    new NpgsqlParameter("@endpoint", node.Endpoint),
                    new NpgsqlParameter("@region", (object)node.Region ?? DBNull.Value),
                    new NpgsqlParameter("@lastSeen", node.LastSeen),
                    new NpgsqlParameter("@totalStorage", node.TotalStorage),
                    new NpgsqlParameter { 
                        ParameterName = "@metadata", 
                        NpgsqlDbType = NpgsqlDbType.Jsonb, 
                        Value = JsonSerializer.Serialize(node.Metadata) 
                    },
                    new NpgsqlParameter { 
                        ParameterName = "@tags", 
                        NpgsqlDbType = NpgsqlDbType.Jsonb, 
                        Value = JsonSerializer.Serialize(node.Tags) 
                    },
                    new NpgsqlParameter("@isHealthy", node.Status.IsHealthy),
                    new NpgsqlParameter("@currentLoad", node.Status.CurrentLoad),
                    new NpgsqlParameter("@availableSpace", node.Status.AvailableSpace),
                    new NpgsqlParameter("@activeConnections", node.Status.ActiveConnections), 
                    new NpgsqlParameter("@lastUpdated", node.Status.LastUpdated),
                    new NpgsqlParameter("@activeTransfers", node.Status.ActiveTransfers),
                    new NpgsqlParameter("@networkCapacity", node.Status.NetworkCapacity),
                    new NpgsqlParameter("@usedSpace", node.Status.UsedSpace)
                );
            }

            return true;
        }
        finally
        {
            // Only dispose the context if we created it
            if (ownsContext)
            {
                await context.DisposeAsync();
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error registering node: {NodeId}", node.Id);
        return false;
    }
}

    public async Task<bool> UpdateNodeAsync(StorageNode node)
    {
        try
        {
            NodeRegistryInitialization.EnsureJsonSerialization(node);
            
            var (context, ownsContext) = GetContext();
            try
            {
                var existingNode = await context.Nodes.FirstOrDefaultAsync(n => n.Id == node.Id);

                if (existingNode == null)
                {
                    _logger.LogWarning("Attempted to update non-existent node: {NodeId}", node.Id);
                    return false;
                }

                // Use the same raw SQL approach with explicit casting
                await context.Database.ExecuteSqlRawAsync(
                    @"UPDATE ""Nodes"" SET 
                      ""Hostname"" = @hostname, 
                      ""Endpoint"" = @endpoint,
                      ""Region"" = @region,
                      ""LastSeen"" = @lastSeen,
                      ""TotalStorage"" = @totalStorage,
                      ""Metadata"" = @metadata::jsonb,
                      ""Tags"" = @tags::jsonb,
                      ""Status_IsHealthy"" = @isHealthy,
                      ""Status_CurrentLoad"" = @currentLoad,
                      ""Status_AvailableSpace"" = @availableSpace,
                      ""Status_ActiveConnections"" = @activeConnections,
                      ""Status_LastUpdated"" = @lastUpdated,
                      ""Status_ActiveTransfers"" = @activeTransfers,
                      ""Status_NetworkCapacity"" = @networkCapacity,
                      ""Status_UsedSpace"" = @usedSpace
                      WHERE ""Id"" = @id",
                    new NpgsqlParameter("@id", node.Id),
                    new NpgsqlParameter("@hostname", node.Hostname),
                    new NpgsqlParameter("@endpoint", node.Endpoint),
                    new NpgsqlParameter("@region", (object)node.Region ?? DBNull.Value),
                    new NpgsqlParameter("@lastSeen", node.LastSeen),
                    new NpgsqlParameter("@totalStorage", node.TotalStorage),
                    new NpgsqlParameter { 
                        ParameterName = "@metadata", 
                        NpgsqlDbType = NpgsqlDbType.Jsonb, 
                        Value = JsonSerializer.Serialize(node.Metadata) 
                    },
                    new NpgsqlParameter { 
                        ParameterName = "@tags", 
                        NpgsqlDbType = NpgsqlDbType.Jsonb, 
                        Value = JsonSerializer.Serialize(node.Tags) 
                    },
                    new NpgsqlParameter("@isHealthy", node.Status?.IsHealthy ?? true),
                    new NpgsqlParameter("@currentLoad", node.Status?.CurrentLoad ?? 0),
                    new NpgsqlParameter("@availableSpace", node.Status?.AvailableSpace ?? 0),
                    new NpgsqlParameter("@activeConnections", node.Status?.ActiveConnections ?? 0),
                    new NpgsqlParameter("@lastUpdated", node.Status?.LastUpdated ?? DateTime.UtcNow),
                    new NpgsqlParameter("@activeTransfers", node.Status?.ActiveTransfers ?? 0),
                    new NpgsqlParameter("@networkCapacity", node.Status?.NetworkCapacity ?? 1000),
                    new NpgsqlParameter("@usedSpace", node.Status?.UsedSpace ?? 0)
                );

                return true;
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating node: {NodeId}", node.Id);
            return false;
        }
    }

    public async Task<StorageNode> GetNodeAsync(string nodeId)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                return await context.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId);
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving node: {NodeId}", nodeId);
            return null;
        }
    }

    public async Task<IEnumerable<StorageNode>> GetAllNodesAsync(bool includeOffline = false)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var query = context.Nodes.AsQueryable();

                if (!includeOffline)
                {
                    // Filter to only include healthy nodes
                    query = query.Where(n => n.Status.IsHealthy);
                }

                return await query.ToListAsync();
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all nodes");
            return Enumerable.Empty<StorageNode>();
        }
    }

    public async Task<bool> MarkNodeUnhealthyAsync(string nodeId)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var node = await context.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId);

                if (node == null)
                {
                    _logger.LogWarning("Attempted to mark non-existent node as unhealthy: {NodeId}", nodeId);
                    return false;
                }

                node.Status.IsHealthy = false;
                node.Status.LastUpdated = DateTime.UtcNow;
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Node marked as unhealthy: {NodeId}", nodeId);
                return true;
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking node as unhealthy: {NodeId}", nodeId);
            return false;
        }
    }

    public async Task<bool> RemoveNodeAsync(string nodeId)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var node = await context.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId);

                if (node == null)
                {
                    _logger.LogWarning("Attempted to remove non-existent node: {NodeId}", nodeId);
                    return false;
                }

                context.Nodes.Remove(node);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Node removed: {NodeId}", nodeId);
                return true;
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing node: {NodeId}", nodeId);
            return false;
        }
    }

    public async Task<IEnumerable<StorageNode>> FindNodesAsync(
        string region = null, 
        IEnumerable<string> tags = null, 
        long minAvailableStorage = 0,
        bool healthyOnly = true)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var query = context.Nodes.AsQueryable();

                // Filter based on health status if requested
                if (healthyOnly)
                {
                    query = query.Where(n => n.Status.IsHealthy);
                }

                // Filter by region if specified
                if (!string.IsNullOrEmpty(region))
                {
                    query = query.Where(n => n.Region == region);
                }

                // Filter by minimum available storage
                if (minAvailableStorage > 0)
                {
                    query = query.Where(n => n.Status.AvailableSpace >= minAvailableStorage);
                }

                // Get the result
                var nodes = await query.ToListAsync();

                // Filter by tags if specified (needs to be done in memory due to list property)
                if (tags != null && tags.Any())
                {
                    var tagsList = tags.ToList();
                    nodes = nodes.Where(n => 
                        n.Tags != null && tagsList.All(t => n.Tags.Contains(t))).ToList();
                }

                return nodes;
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding nodes with specified criteria");
            return Enumerable.Empty<StorageNode>();
        }
    }
    
    public async Task<IEnumerable<StorageNode>> GetNodesWithMostStorageAsync(int count, string region = null)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var query = context.Nodes.Where(n => n.Status.IsHealthy);
                
                // Filter by region if specified
                if (!string.IsNullOrEmpty(region))
                {
                    query = query.Where(n => n.Region == region);
                }
                
                // Get all nodes so we can sort in memory by AvailableSpace
                var nodes = await query.ToListAsync();
                
                // Order by available space and take the requested number
                var orderedNodes = nodes
                    .OrderByDescending(n => n.Status.AvailableSpace)
                    .Take(count)
                    .ToList();
                
                return orderedNodes;
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nodes with most storage");
            return Enumerable.Empty<StorageNode>();
        }
    }
    
    public async Task<IEnumerable<StorageNode>> GetNodesWithLowestLoadAsync(int count, string region = null)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var query = context.Nodes.Where(n => n.Status.IsHealthy);
                
                // Filter by region if specified
                if (!string.IsNullOrEmpty(region))
                {
                    query = query.Where(n => n.Region == region);
                }
                
                // Get all nodes so we can sort in memory
                var nodes = await query.ToListAsync();
                
                // Order by current load and take the requested number
                var orderedNodes = nodes
                    .OrderBy(n => n.Status.CurrentLoad)
                    .ThenBy(n => n.Status.ActiveConnections)
                    .Take(count)
                    .ToList();
                
                return orderedNodes;
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nodes with lowest load");
            return Enumerable.Empty<StorageNode>();
        }
    }
    
    public async Task<IEnumerable<NodeInfo>> GetNodesByIdsAsync(List<string> nodeIds)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var nodes = await context.Nodes
                    .Where(n => nodeIds.Contains(n.Id))
                    .ToListAsync();
                
                return nodes.Select(MapToNodeInfo).ToList();
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nodes by IDs");
            return Enumerable.Empty<NodeInfo>();
        }
    }
    
    public async Task<IEnumerable<NodeInfo>> GetAvailableNodesAsync(long minAvailableSpace = 0, string region = null)
    {
        try
        {
            var (context, ownsContext) = GetContext();
            try
            {
                var query = context.Nodes.Where(n => n.Status.IsHealthy);
                
                // Filter by minimum available space
                if (minAvailableSpace > 0)
                {
                    query = query.Where(n => n.Status.AvailableSpace >= minAvailableSpace);
                }
                
                // Filter by region if specified
                if (!string.IsNullOrEmpty(region))
                {
                    query = query.Where(n => n.Region == region);
                }
                
                var nodes = await query.ToListAsync();
                
                return nodes.Select(MapToNodeInfo).ToList();
            }
            finally
            {
                if (ownsContext)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available nodes");
            return Enumerable.Empty<NodeInfo>();
        }
    }
    
    private NodeInfo MapToNodeInfo(StorageNode node)
    {
        return new NodeInfo
        {
            Id = node.Id,
            Endpoint = node.Endpoint,
            Region = node.Region,
            IsHealthy = node.Status.IsHealthy,
            CurrentLoad = node.Status.CurrentLoad,
            AvailableSpace = node.Status.AvailableSpace
        };
    }
}