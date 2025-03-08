using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.RegistryService;
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

builder.WebHost.UseUrls("http://localhost:5000;https://localhost:5001");
//app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request received: {Method} {Path}", 
        context.Request.Method, context.Request.Path);
    
    await next();
});

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
    
    // This will drop the database and recreate it with the correct schema
    // Only do this in Development - remove for Production!
    if (app.Environment.IsDevelopment())
    {
        dbContext.Database.EnsureDeleted();
    }
    
    dbContext.Database.EnsureCreated();
}

app.Run();