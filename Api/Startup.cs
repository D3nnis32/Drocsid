using System.Text;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Drocsid.HenrikDennis2025.Server.Services;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Drocsid.HenrikDennis2025.Api.Hub;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Drocsid.HenrikDennis2025.Api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Add DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"), 
                b => b.MigrationsAssembly("Infrastructure")));

        // Add repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Add application services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Add PasswordHasher
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // Configure JWT authentication
        var jwtKey = Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("JWT Key is not configured");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Configuration["Jwt:Issuer"],
                    ValidAudience = Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
                };
            });

        // Add CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        // Add SignalR
        services.AddSignalR();

        // Add controllers
        services.AddControllers();

        // Add Swagger
        services.AddSwaggerGen(c =>
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
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
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

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<ChatHub>("/hubs/chat");
        });
    }
}