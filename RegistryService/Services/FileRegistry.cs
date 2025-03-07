using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
    /// Entity Framework implementation of the file registry
    /// </summary>
    public class FileRegistry : IFileRegistry
    {
        private readonly RegistryDbContext _dbContext;
        private readonly ILogger<FileRegistry> _logger;
        private readonly DbContextOptions<RegistryDbContext>? _dbContextOptions;

        // Constructor for regular scoped service
        public FileRegistry(RegistryDbContext dbContext, ILogger<FileRegistry> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _dbContextOptions = null;
        }

        // Constructor for singleton background service
        public FileRegistry(DbContextOptions<RegistryDbContext> dbContextOptions, ILogger<FileRegistry> logger)
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

        public async Task RegisterFileAsync(FileStorage fileStorage)
        {
            try
            {
                var context = GetContext();
                var shouldDispose = _dbContext == null;
                
                try
                {
                    // Check if file already exists
                    var existingFile = await context.Files.FindAsync(fileStorage.FileId);
                    if (existingFile != null)
                    {
                        _logger.LogWarning("Attempting to register existing file {FileId}", fileStorage.FileId);
                        return;
                    }

                    // Set creation time if not already set
                    if (fileStorage.CreatedAt == default)
                    {
                        fileStorage.CreatedAt = DateTime.UtcNow;
                    }

                    await context.Files.AddAsync(fileStorage);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Registered file {FileId} on {NodeCount} nodes", 
                        fileStorage.FileId, fileStorage.NodeIds.Count);
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
                _logger.LogError(ex, "Error registering file {FileId}", fileStorage.FileId);
                throw;
            }
        }

        public async Task<FileStorage?> GetFileInfoAsync(string fileId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                return await context.Files.FindAsync(fileId);
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task UpdateFileLocationsAsync(string fileId, List<string> nodeIds)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var file = await context.Files.FindAsync(fileId);
                if (file == null)
                {
                    _logger.LogWarning("Attempting to update locations for non-existent file {FileId}", fileId);
                    return;
                }

                file.NodeIds = nodeIds;
                file.LastAccessed = DateTime.UtcNow;
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Updated file {FileId} locations to {NodeIds}", 
                    fileId, string.Join(", ", nodeIds));
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task AddFileLocationAsync(string fileId, string nodeId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var file = await context.Files.FindAsync(fileId);
                if (file == null)
                {
                    _logger.LogWarning("Attempting to add location for non-existent file {FileId}", fileId);
                    return;
                }

                if (!file.NodeIds.Contains(nodeId))
                {
                    file.NodeIds.Add(nodeId);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Added node {NodeId} to file {FileId} locations", nodeId, fileId);
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

        public async Task RemoveFileLocationAsync(string fileId, string nodeId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var file = await context.Files.FindAsync(fileId);
                if (file == null)
                {
                    _logger.LogWarning("Attempting to remove location for non-existent file {FileId}", fileId);
                    return;
                }

                if (file.NodeIds.Contains(nodeId))
                {
                    file.NodeIds.Remove(nodeId);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Removed node {NodeId} from file {FileId} locations", nodeId, fileId);
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

        public async Task<List<FileStorage>> GetFilesByNodeAsync(string nodeId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                return await context.Files
                    .Where(f => f.NodeIds.Contains(nodeId))
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

        public async Task DeleteFileAsync(string fileId)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var file = await context.Files.FindAsync(fileId);
                if (file != null)
                {
                    context.Files.Remove(file);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Deleted file {FileId} from registry", fileId);
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

        public async Task<bool> CheckReplicationFactorAsync(string fileId, int minReplicationFactor)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                var file = await context.Files.FindAsync(fileId);
                if (file == null)
                {
                    return false;
                }

                return file.NodeIds.Count >= minReplicationFactor;
            }
            finally
            {
                if (shouldDispose)
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<List<FileStorage>> GetFilesNeedingReplicationAsync(int minReplicationFactor)
        {
            var context = GetContext();
            var shouldDispose = _dbContext == null;
            
            try
            {
                // Get all files first, then filter in memory
                // This avoids the PostgreSQL cardinality issue
                var allFiles = await context.Files.ToListAsync();
                return allFiles.Where(f => f.NodeIds.Count < minReplicationFactor).ToList();
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