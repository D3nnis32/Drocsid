using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
    /// Entity Framework implementation of the node registry
    /// </summary>
    public class NodeRegistry : INodeRegistry
    {
        private readonly RegistryDbContext _dbContext;
        private readonly ILogger<NodeRegistry> _logger;
        private readonly DbContextOptions<RegistryDbContext>? _dbContextOptions;

        // Constructor for regular scoped service
        public NodeRegistry(RegistryDbContext dbContext, ILogger<NodeRegistry> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _dbContextOptions = null;
        }

        // Constructor for singleton background service
        public NodeRegistry(DbContextOptions<RegistryDbContext> dbContextOptions, ILogger<NodeRegistry> logger)
        {
            _dbContext = null!; // Not used in this constructor
            _logger = logger;
            _dbContextOptions = dbContextOptions;
        }

        // Helper to get the appropriate DbContext
        private RegistryDbContext GetContext()
        {
            // If we have a direct DbContext from DI, use it
            if (_dbContext != null)
                return _dbContext;
            
            // Otherwise, create a new one from options
            if (_dbContextOptions != null)
                return new RegistryDbContext(_dbContextOptions);
            
            throw new InvalidOperationException("No valid DbContext available");
        }

        public async Task<string> RegisterNodeAsync(Node node)
        {
            try
            {
                var context = GetContext();
                var shouldDispose = _dbContext == null;
                
                try
                {
                    // Generate ID if not provided
                    if (string.IsNullOrEmpty(node.Id))
                    {
                        node.Id = Guid.NewGuid().ToString();
                    }

                    // Set initial heartbeat
                    node.LastHeartbeat = DateTime.UtcNow;
                    
                    // Ensure metadata isn't null
                    node.Metadata ??= new Dictionary<string, string>();

                    await context.Nodes.AddAsync(node);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Registered node {NodeId} at {Endpoint}", node.Id, node.Endpoint);
                    
                    return node.Id;
                }
                finally
                {
                    if (shouldDispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering node {Endpoint}", node.Endpoint);
                throw;
            }
        }

        public async Task UpdateNodeStatusAsync(string nodeId, NodeStatus status)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var node = await context.Nodes.FindAsync(nodeId);
                if (node == null)
                {
                    _logger.LogWarning("Attempting to update status for non-existent node {NodeId}", nodeId);
                    return;
                }

                // Update node properties
                node.IsHealthy = status.IsHealthy;
                node.CurrentLoad = status.CurrentLoad;
                node.AvailableSpace = status.AvailableSpace;
                node.LastHeartbeat = DateTime.UtcNow;

                await context.SaveChangesAsync();
                
                _logger.LogDebug("Updated node {NodeId} status: Healthy={IsHealthy}, Load={Load}, Space={Space}", 
                    nodeId, status.IsHealthy, status.CurrentLoad, status.AvailableSpace);
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<Node?> GetNodeAsync(string nodeId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                return await context.Nodes.FindAsync(nodeId);
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<List<Node>> GetNodesByIdsAsync(List<string> nodeIds)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                return await context.Nodes
                    .Where(n => nodeIds.Contains(n.Id))
                    .ToListAsync();
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<List<Node>> GetAllNodesAsync()
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                return await context.Nodes.ToListAsync();
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<List<Node>> GetAvailableNodesAsync(int? limit = null)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                // Create the base query
                var query = context.Nodes
                    .Where(n => n.IsHealthy && 
                               n.LastHeartbeat > DateTime.UtcNow.AddMinutes(-1));

                // Apply ordering
                var orderedQuery = query.OrderBy(n => n.CurrentLoad);

                // Apply limit if provided
                if (limit.HasValue)
                {
                    return await orderedQuery.Take(limit.Value).ToListAsync();
                }
                else
                {
                    return await orderedQuery.ToListAsync();
                }
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<List<Node>> SelectNodesForStorageAsync(int count, string? preferredRegion = null)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                // Start with healthy nodes
                var query = context.Nodes
                    .Where(n => n.IsHealthy && 
                               n.LastHeartbeat > DateTime.UtcNow.AddMinutes(-1));

                // Apply region preference if specified
                if (!string.IsNullOrEmpty(preferredRegion))
                {
                    // First, get nodes in the preferred region
                    var preferredNodes = await query
                        .Where(n => n.Region == preferredRegion)
                        .OrderByDescending(n => n.AvailableSpace)
                        .Take(count)
                        .ToListAsync();
                    
                    // If we have enough nodes in the preferred region, return them
                    if (preferredNodes.Count >= count)
                    {
                        return preferredNodes;
                    }
                    
                    // Otherwise, get additional nodes from other regions
                    var remainingCount = count - preferredNodes.Count;
                    var otherNodes = await query
                        .Where(n => n.Region != preferredRegion)
                        .OrderByDescending(n => n.AvailableSpace)
                        .Take(remainingCount)
                        .ToListAsync();
                    
                    // Combine the lists
                    preferredNodes.AddRange(otherNodes);
                    return preferredNodes;
                }
                else
                {
                    // Just order by available space if no region preference
                    return await query
                        .OrderByDescending(n => n.AvailableSpace)
                        .Take(count)
                        .ToListAsync();
                }
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task MarkNodeOfflineAsync(string nodeId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var node = await context.Nodes.FindAsync(nodeId);
                if (node != null)
                {
                    node.IsHealthy = false;
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Marked node {NodeId} as offline", nodeId);
                }
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task RemoveNodeAsync(string nodeId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var node = await context.Nodes.FindAsync(nodeId);
                if (node != null)
                {
                    context.Nodes.Remove(node);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Removed node {NodeId} from registry", nodeId);
                }
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
    }