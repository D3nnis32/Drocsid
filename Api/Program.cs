using System.Text;
using Drocsid.HenrikDennis2025.Api.Controllers;
using Drocsid.HenrikDennis2025.Api.Hub;
using Drocsid.HenrikDennis2025.Api.Services;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), 
        b => b.MigrationsAssembly("Infrastructure")));

// Add repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Add application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.AddScoped<ILogger<BaseController>, Logger<BaseController>>();
builder.Services.AddScoped<ILogger<HealthController>, Logger<HealthController>>();

// Add PasswordHasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddHttpClient("NodeTransfer", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "DrocsidNodeTransfer");
    client.Timeout = TimeSpan.FromMinutes(5); // Longer timeout for file transfers
});

// Configure options for file transfer
builder.Services.Configure<FileTransferOptions>(builder.Configuration.GetSection("FileTransfer"));

// Add FileTransferService
builder.Services.AddScoped<IFileTransferService, FileTransferService>();

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key is not configured");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };

        // Konfigurieren Sie SignalR für JWT-Authentifizierung (optional)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add Node-to-Node authentication
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("NodeAuthorization", policy =>
    {
        policy.RequireAuthenticatedUser();
        // Add custom requirements for node-to-node communication if needed
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Add controllers
builder.Services.AddControllers();

// Add API Explorer für Swagger
builder.Services.AddEndpointsApiExplorer();

// Add Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ChatApp API", Version = "v1" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure NodeRegistration options
builder.Services.Configure<NodeRegistrationOptions>(
    builder.Configuration.GetSection("NodeRegistration"));

// Add HTTP client factory for making requests to the registry
builder.Services.AddHttpClient();

// Register the NodeRegistrationClient as a hosted service
builder.Services.AddHostedService<NodeRegistrationClient>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatApp API v1"));
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Ensure storage directory exists
var storagePath = builder.Configuration["FileStorage:LocalPath"] ?? 
                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileStorage");
    
if (!Directory.Exists(storagePath))
{
    Directory.CreateDirectory(storagePath);
    // Create temp uploads directory
    Directory.CreateDirectory(Path.Combine(storagePath, "temp"));
}

// Register this node with the registry service if configured
var nodeId = builder.Configuration["NodeId"];
var registryEndpoint = builder.Configuration["RegistryService:Endpoint"];

if (!string.IsNullOrEmpty(nodeId) && !string.IsNullOrEmpty(registryEndpoint))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Registering node {NodeId} with registry service at {RegistryEndpoint}", 
            nodeId, registryEndpoint);
            
        // Registration logic would go here
        // In a production system, implement node registration with the registry service
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error registering node: {ex.Message}");
        // Continue startup even if registration fails
    }
}

app.MapGet("/health", () => {
    return Results.Ok(new { status = "healthy" });
});

app.Run();