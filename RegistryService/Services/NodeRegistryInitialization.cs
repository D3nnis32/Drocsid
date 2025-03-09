using System.Text.Json;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

public static class NodeRegistryInitialization
    {
        /// <summary>
        /// Initializes a StorageNode with empty collections to prevent JSON serialization issues
        /// </summary>
        public static StorageNode InitializeNode(string id, string hostname, string endpoint, string region, long totalStorage)
        {
            return new StorageNode
            {
                Id = id,
                Hostname = hostname,
                Endpoint = endpoint,
                Region = region,
                TotalStorage = totalStorage,
                LastSeen = DateTime.UtcNow,
                // Initialize with empty collections to prevent JSONB serialization issues
                Tags = new List<string>(),
                Metadata = new Dictionary<string, string>(),
                Status = new NodeStatus
                {
                    IsHealthy = true,
                    CurrentLoad = 0,
                    AvailableSpace = totalStorage,
                    ActiveConnections = 0,
                    LastUpdated = DateTime.UtcNow,
                    ActiveTransfers = 0,
                    NetworkCapacity = 1000,
                    UsedSpace = 0
                }
            };
        }
        
        /// <summary>
        /// Modified RegisterNodeAsync method for NodeRegistry service that properly serializes JSON
        /// </summary>
        public static void EnsureJsonSerialization(StorageNode node)
        {
            // Ensure collections are properly initialized to avoid null issues
            if (node.Tags == null)
                node.Tags = new List<string>();
                
            if (node.Metadata == null)
                node.Metadata = new Dictionary<string, string>();
                
            // Test JSON serialization to ensure it works
            var tagsJson = JsonSerializer.Serialize(node.Tags);
            var metadataJson = JsonSerializer.Serialize(node.Metadata);
            
            // Ensure Status object is initialized
            if (node.Status == null)
            {
                node.Status = new NodeStatus
                {
                    IsHealthy = true,
                    CurrentLoad = 0,
                    AvailableSpace = node.TotalStorage,
                    ActiveConnections = 0,
                    LastUpdated = DateTime.UtcNow,
                    ActiveTransfers = 0,
                    NetworkCapacity = 1000,
                    UsedSpace = 0
                };
            }
        }
    }