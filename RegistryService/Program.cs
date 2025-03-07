using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.RegistryService.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

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
        Description = "API for managing distributed file storage registry" 
    });
});

// Add database context as a regular scoped service
builder.Services.AddDbContext<RegistryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("RegistryConnection")));

// Register services - but use factory methods to avoid constructor ambiguity
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

// Add background service
var connectionString = builder.Configuration.GetConnectionString("RegistryConnection");
builder.Services.AddSingleton<INodeHealthMonitor>(sp => 
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var options = new DbContextOptionsBuilder<RegistryDbContext>()
        .UseNpgsql(connectionString)
        .Options;
        
    var nodeRegistry = new NodeRegistry(options, loggerFactory.CreateLogger<NodeRegistry>());
    var fileRegistry = new FileRegistry(options, loggerFactory.CreateLogger<FileRegistry>());
    
    return new NodeHealthMonitor(
        nodeRegistry, 
        fileRegistry, 
        loggerFactory.CreateLogger<NodeHealthMonitor>());
});

// Register the background service
builder.Services.AddHostedService<NodeHealthMonitorHostedService>();

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

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Drocsid Registry API v1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();