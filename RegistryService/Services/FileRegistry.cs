using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

public class FileRegistry : IFileRegistry
    {
        private readonly RegistryDbContext _dbContext;
        private readonly ILogger<FileRegistry> _logger;
        private readonly DbContextOptions<RegistryDbContext> _options;

        // Constructor for scoped service (normal usage)
        public FileRegistry(RegistryDbContext dbContext, ILogger<FileRegistry> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = null;
        }

        // Constructor for singleton service (health monitor)
        public FileRegistry(DbContextOptions<RegistryDbContext> options, ILogger<FileRegistry> logger)
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

        public async Task<bool> RegisterFileAsync(StoredFile file)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                var existingFile = await context.Files.FirstOrDefaultAsync(f => f.Id == file.Id);

                if (existingFile != null)
                {
                    _logger.LogWarning("Attempted to register file with existing ID: {FileId}", file.Id);
                    return false;
                }

                // Ensure file has a valid ID
                if (string.IsNullOrEmpty(file.Id))
                {
                    file.Id = Guid.NewGuid().ToString();
                }

                file.CreatedAt = DateTime.UtcNow;
                file.ModifiedAt = DateTime.UtcNow;
                
                await context.Files.AddAsync(file);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("File registered: {FileId}, Name: {Filename}", file.Id, file.Filename);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering file: {FileId}", file.Id);
                return false;
            }
        }

        // New method to handle FileStorage registrations from API
        public async Task<bool> RegisterFileStorageAsync(FileStorage fileStorage)
        {
            try
            {
                // Convert FileStorage to StoredFile
                var storedFile = new StoredFile
                {
                    Id = fileStorage.FileId,
                    Filename = fileStorage.FileName,
                    ContentType = fileStorage.ContentType,
                    Size = fileStorage.Size,
                    Checksum = fileStorage.Checksum,
                    NodeLocations = fileStorage.NodeIds,
                    Metadata = fileStorage.Metadata,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    OwnerId = "system" // Default owner, should be replaced with actual user ID in real implementation
                };
                
                return await RegisterFileAsync(storedFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering file from storage: {FileId}", fileStorage.FileId);
                return false;
            }
        }

        public async Task<bool> UpdateFileAsync(StoredFile file)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                var existingFile = await context.Files.FirstOrDefaultAsync(f => f.Id == file.Id);

                if (existingFile == null)
                {
                    _logger.LogWarning("Attempted to update non-existent file: {FileId}", file.Id);
                    return false;
                }

                // Update the file properties
                context.Entry(existingFile).CurrentValues.SetValues(file);
                existingFile.ModifiedAt = DateTime.UtcNow;
                
                await context.SaveChangesAsync();
                
                _logger.LogInformation("File updated: {FileId}", file.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file: {FileId}", file.Id);
                return false;
            }
        }

        public async Task<StoredFile> GetFileAsync(string fileId)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                try
                {
                    return await context.Files.FirstOrDefaultAsync(f => f.Id == fileId);
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
                _logger.LogError(ex, "Error retrieving file: {FileId}", fileId);
                return null;
            }
        }

        public async Task<IEnumerable<StoredFile>> FindFilesByNameAsync(string filename)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                return await context.Files
                    .Where(f => f.Filename.Contains(filename))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding files by name: {Filename}", filename);
                return Enumerable.Empty<StoredFile>();
            }
        }

        public async Task<IEnumerable<StoredFile>> GetFilesByNodeAsync(string nodeId)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                // Load all files first, then filter in memory
                var allFiles = await context.Files.ToListAsync();
                return allFiles.Where(f => f.NodeLocations.Contains(nodeId)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving files for node: {NodeId}", nodeId);
                return Enumerable.Empty<StoredFile>();
            }
        }

        public async Task<bool> AddFileLocationAsync(string fileId, string nodeId)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                var file = await context.Files.FirstOrDefaultAsync(f => f.Id == fileId);

                if (file == null)
                {
                    _logger.LogWarning("Attempted to add location for non-existent file: {FileId}", fileId);
                    return false;
                }

                // This check is now in-memory, not in SQL
                if (!file.NodeLocations.Contains(nodeId))
                {
                    file.NodeLocations.Add(nodeId);
                    file.ModifiedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
            
                    _logger.LogInformation("Added node location {NodeId} for file {FileId}", nodeId, fileId);
                }
        
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding file location: File {FileId}, Node {NodeId}", fileId, nodeId);
                return false;
            }
        }

        public async Task<bool> RemoveFileLocationAsync(string fileId, string nodeId)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                var file = await context.Files.FirstOrDefaultAsync(f => f.Id == fileId);

                if (file == null)
                {
                    _logger.LogWarning("Attempted to remove location for non-existent file: {FileId}", fileId);
                    return false;
                }

                // This check is now in-memory, not in SQL
                if (file.NodeLocations.Contains(nodeId))
                {
                    file.NodeLocations.Remove(nodeId);
                    file.ModifiedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
            
                    _logger.LogInformation("Removed node location {NodeId} for file {FileId}", nodeId, fileId);
                }
        
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing file location: File {FileId}, Node {NodeId}", fileId, nodeId);
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                var file = await context.Files.FirstOrDefaultAsync(f => f.Id == fileId);

                if (file == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent file: {FileId}", fileId);
                    return false;
                }

                context.Files.Remove(file);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("File deleted: {FileId}", fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FileId}", fileId);
                return false;
            }
        }
        
        public async Task<IEnumerable<StoredFile>> GetAllFilesAsync()
        {
            try
            {
                var (context, ownsContext) = GetContext();
                return await context.Files.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all files");
                return Enumerable.Empty<StoredFile>();
            }
        }

        public async Task<FileStorage> GetFileInfoAsync(string fileId)
        {
            try
            {
                var (context, ownsContext) = GetContext();
                var file = await context.Files.FirstOrDefaultAsync(f => f.Id == fileId);
        
                if (file == null)
                {
                    return null;
                }
        
                return new FileStorage
                {
                    FileId = file.Id,
                    FileName = file.Filename,
                    ContentType = file.ContentType,
                    Size = file.Size,
                    NodeIds = file.NodeLocations,
                    Checksum = file.Checksum,
                    Metadata = file.Metadata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file info: {FileId}", fileId);
                return null;
            }
        }
        
        public async Task UpdateFileAccessTimeAsync(string fileId)
        {
            var (context, ownsContext) = GetContext();
            var shouldDispose = _dbContext == null;
    
            try
            {
                var file = await context.Files.FindAsync(fileId);
                if (file != null)
                {
                    file.LastAccessed = DateTime.UtcNow;
                    await context.SaveChangesAsync();
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