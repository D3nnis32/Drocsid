using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
            var (context, ownsContext) = GetContext();
            try
            {
                var existingNode = await context.Nodes.FirstOrDefaultAsync(n => n.Id == node.Id);

                if (existingNode != null)
                {
                    // Update existing node
                    _logger.LogInformation("Updating existing node: {NodeId}", node.Id);
                    
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
                }
                else
                {
                    // Add new node
                    _logger.LogInformation("Registering new node: {NodeId}", node.Id);
                    
                    // Ensure node has a valid ID
                    if (string.IsNullOrEmpty(node.Id))
                    {
                        node.Id = Guid.NewGuid().ToString();
                    }
                    
                    // Ensure node has a valid status
                    if (node.Status == null)
                    {
                        node.Status = new NodeStatus
                        {
                            IsHealthy = true,
                            CurrentLoad = 0,
                            AvailableSpace = 0,
                            ActiveConnections = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                    }
                    
                    await context.Nodes.AddAsync(node);
                }

                await context.SaveChangesAsync();
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
            var (context, ownsContext) = GetContext();
            try
            {
                var existingNode = await context.Nodes.FirstOrDefaultAsync(n => n.Id == node.Id);

                if (existingNode == null)
                {
                    _logger.LogWarning("Attempted to update non-existent node: {NodeId}", node.Id);
                    return false;
                }

                context.Entry(existingNode).CurrentValues.SetValues(node);
                existingNode.LastSeen = DateTime.UtcNow;

                await context.SaveChangesAsync();
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