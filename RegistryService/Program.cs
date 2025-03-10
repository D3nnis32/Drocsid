using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.RegistryService;
using Drocsid.HenrikDennis2025.RegistryService.Controllers;
using Drocsid.HenrikDennis2025.RegistryService.Services;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.Win32;
using RegistryOptions = Drocsid.HenrikDennis2025.Core.Interfaces.Options.RegistryOptions;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Services.AddLogging();

// Add controllers
builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Drocsid Registry Service API", 
        Version = "v1",
        Description = "API for managing distributed chat and file storage system" 
    });
});

// Add database context
builder.Services.AddDbContext<RegistryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("RegistryConnection")));

var logger2 = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
var dbContextOptions = builder.Services.BuildServiceProvider().GetRequiredService<DbContextOptions<RegistryDbContext>>();
using var tempContext = new RegistryDbContext(dbContextOptions);
logger2.LogInformation("DbContext created - checking entity types in model:");
foreach (var entityType in tempContext.Model.GetEntityTypes())
{
    logger2.LogInformation("Entity Type: {EntityType}, Table: {TableName}, Schema: {Schema}", 
        entityType.Name, entityType.GetTableName(), entityType.GetSchema() ?? "default");
}

// Register registry services using factory pattern to resolve constructor ambiguity
builder.Services.AddScoped<INodeRegistry>(sp => 
{
    var dbContext = sp.GetRequiredService<RegistryDbContext>();
    var logger = sp.GetRequiredService<ILogger<NodeRegistry>>();
    return new NodeRegistry(dbContext, logger);
});

builder.Services.AddScoped<IFileRegistry>(sp => 
{
    var dbContext = sp.GetRequiredService<RegistryDbContext>();
    var logger = sp.GetRequiredService<ILogger<FileRegistry>>();
    return new FileRegistry(dbContext, logger);
});

// Add new registry services
builder.Services.AddScoped<IUserRegistry, UserRegistry>();
builder.Services.AddScoped<IMessageRegistry, MessageRegistry>();
builder.Services.AddScoped<IChannelRegistry, ChannelRegistry>();

// Add user service for authentication in gateway
builder.Services.AddScoped<IUserService, RegistryUserService>();

// Configure NodeHealthMonitorOptions
builder.Services.Configure<NodeHealthMonitorOptions>(builder.Configuration.GetSection("NodeHealthMonitor"));

// Configure DrocsidClientOptions
builder.Services.Configure<DrocsidClientOptions>(builder.Configuration.GetSection("DrocsidClient"));

// Configure Registry Options
builder.Services.Configure<RegistryOptions>(builder.Configuration.GetSection("Registry"));

// Register NodeHealthMonitor as a scoped service
builder.Services.AddScoped<INodeHealthMonitor, NodeHealthMonitor>();
builder.Services.AddScoped<IFileTransferService, FileTransferService>();

// Add the hosted service that will create scoped instances of INodeHealthMonitor
builder.Services.AddHostedService<BackgroundServiceHost<INodeHealthMonitor, NodeHealthMonitor>>();
builder.Services.AddHostedService<DatabaseIntegrationTestService>();
builder.Services.AddControllers()
    .AddApplicationPart(typeof(NodeReassignmentController).Assembly);

// Register the improved node health monitor instead of the original one
builder.Services.AddScoped<INodeHealthMonitor, ImprovedNodeHealthMonitor>();

// Register required loggers
builder.Services.AddScoped<ILogger<EnhancedNodeHealthChecker>, Logger<EnhancedNodeHealthChecker>>();
builder.Services.AddScoped<ILogger<ImprovedNodeHealthMonitor>, Logger<ImprovedNodeHealthMonitor>>();

// Adjust the background service to use improved monitor
builder.Services.AddHostedService<BackgroundServiceHost<INodeHealthMonitor, ImprovedNodeHealthMonitor>>();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Register the DrocsidClient
builder.Services.AddScoped<IDrocsidClient, DrocsidClient>();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add JWT Authentication (for Gateway authentication)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
})
.AddJwtBearer("Bearer", options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Drocsid Registry API v1"));
}

builder.WebHost.UseUrls("http://*:5261");
//app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Add explicit health check endpoint
app.MapGet("/health", () => {
    return Results.Ok(new { status = "healthy" });
});

app.MapGet("/health/db", async (IServiceProvider serviceProvider) => {
    using var scope = serviceProvider.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
    
    try {
        logger.LogInformation("Checking database connection...");
        bool canConnect = await dbContext.Database.CanConnectAsync();
        
        if (!canConnect) {
            logger.LogError("Cannot connect to database");
            return Results.Problem("Cannot connect to database");
        }
        
        // Try to create the database schema
        logger.LogInformation("Ensuring database schema exists...");
        
        // Check if it worked by querying the tables
        var tables = await dbContext.Database
            .SqlQuery<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        
        logger.LogInformation("Tables in database: {Tables}", string.Join(", ", tables));
        
        return Results.Ok(new { 
            status = "healthy", 
            dbConnection = true,
            tables = tables 
        });
    }
    catch (Exception ex) {
        logger.LogError(ex, "Error checking database status");
        return Results.Problem(ex.Message);
    }
});

// With this more robust version:
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
    
    try 
    {
        logger.LogInformation("Initializing database schema...");
        
        // Check if database exists
        bool dbExists = await dbContext.Database.CanConnectAsync();
        if (!dbExists)
        {
            logger.LogWarning("Cannot connect to database - check connection string and ensure PostgreSQL is running");
        }
        else
        {
            logger.LogInformation("Successfully connected to database");
            
            // Check if tables exist by querying information schema
            var tablesQuery = await dbContext.Database
                .SqlQuery<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
                .ToListAsync();
                
            logger.LogInformation("Found {Count} tables in database. Tables: {Tables}", 
                tablesQuery.Count, string.Join(", ", tablesQuery));
                
            // If no tables or missing expected tables, recreate schema
            if (tablesQuery.Count == 0 || !tablesQuery.Contains("Nodes"))
            {
                logger.LogWarning("Required tables don't exist. Recreating database schema...");
                
                // Force recreation
                await dbContext.Database.EnsureDeletedAsync();
                await dbContext.Database.EnsureCreatedAsync();
                
                logger.LogInformation("Database schema created successfully");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database initialization");
    }
}

app.Run();