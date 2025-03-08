using System.Text;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseDebugController : ControllerBase
{
    private readonly RegistryDbContext _dbContext;
    private readonly ILogger<DatabaseDebugController> _logger;
    private readonly IWebHostEnvironment _environment;

    public DatabaseDebugController(
        RegistryDbContext dbContext,
        ILogger<DatabaseDebugController> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetDatabaseStatus()
    {
        var result = new StringBuilder();
        
        try
        {
            // Check if the database exists
            bool canConnect = await _dbContext.Database.CanConnectAsync();
            result.AppendLine($"Database connection: {(canConnect ? "Success" : "Failed")}");
            
            if (!canConnect)
            {
                _logger.LogError("Cannot connect to database");
                return Problem("Cannot connect to database");
            }
            
            // Get database provider
            var provider = _dbContext.Database.ProviderName;
            result.AppendLine($"Database provider: {provider}");
            
            // Get connection string (sanitized)
            var connectionString = _dbContext.Database.GetConnectionString();
            var sanitizedConnectionString = SanitizeConnectionString(connectionString);
            result.AppendLine($"Connection string: {sanitizedConnectionString}");
            
            // Log all tables in the database
            var tables = new List<string>();
            
            if (provider.Contains("Npgsql"))
            {
                // PostgreSQL specific query
                tables = await _dbContext.Database
                    .SqlQuery<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
                    .ToListAsync();
            }
            
            result.AppendLine($"Number of tables found: {tables.Count}");
            if (tables.Count > 0)
            {
                result.AppendLine("Tables:");
                foreach (var table in tables)
                {
                    result.AppendLine($"  - {table}");
                }
            }
            else
            {
                result.AppendLine("No tables found in the database!");
                
                // Try to create the database schema
                result.AppendLine("\nAttempting to create database schema...");
                await _dbContext.Database.EnsureCreatedAsync();
                
                // Check again
                if (provider.Contains("Npgsql"))
                {
                    // PostgreSQL specific query
                    tables = await _dbContext.Database
                        .SqlQuery<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
                        .ToListAsync();
                }
                
                result.AppendLine($"After EnsureCreated, number of tables found: {tables.Count}");
                if (tables.Count > 0)
                {
                    result.AppendLine("Tables created:");
                    foreach (var table in tables)
                    {
                        result.AppendLine($"  - {table}");
                    }
                }
                else
                {
                    result.AppendLine("Still no tables found after EnsureCreated!");
                }
            }
            
            // Environment information
            result.AppendLine($"\nEnvironment: {_environment.EnvironmentName}");
            
            // Print model information from DbContext
            result.AppendLine("\nDbContext model information:");
            foreach (var entityType in _dbContext.Model.GetEntityTypes())
            {
                result.AppendLine($"  - Entity: {entityType.Name}");
                result.AppendLine($"    Table: {entityType.GetTableName()}");
                result.AppendLine($"    Schema: {entityType.GetSchema() ?? "default"}");
            }
            
            // Log the result
            _logger.LogInformation("Database status: {Status}", result.ToString());
            
            return Ok(result.ToString());
        }
        catch (Exception ex)
        {
            result.AppendLine($"\nError: {ex.Message}");
            if (ex.InnerException != null)
            {
                result.AppendLine($"Inner exception: {ex.InnerException.Message}");
            }
            result.AppendLine($"Stack trace: {ex.StackTrace}");
            
            _logger.LogError(ex, "Error checking database status");
            return Problem(result.ToString());
        }
    }
    
    [HttpPost("force-create-schema")]
    public async Task<IActionResult> ForceCreateSchema()
    {
        try
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.Database.EnsureCreatedAsync();
            
            return Ok("Database schema recreated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recreating database schema");
            return Problem($"Error recreating schema: {ex.Message}");
        }
    }
    
    private string SanitizeConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "null or empty";
            
        // Remove password
        var sanitized = connectionString;
        
        // Simple sanitization - might need to be more sophisticated depending on your connection string format
        if (sanitized.Contains("Password="))
        {
            var parts = sanitized.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Contains("Password="))
                {
                    parts[i] = "Password=*****";
                }
            }
            sanitized = string.Join(";", parts);
        }
        
        return sanitized;
    }
}