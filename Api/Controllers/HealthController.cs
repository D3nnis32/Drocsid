using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;

    public HealthController(ILogger<HealthController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy" });
    }

    [HttpGet("detailed")]
    public IActionResult GetDetailed()
    {
        try
        {
            // Calculate system metrics
            var process = Process.GetCurrentProcess();
            var uptime = DateTime.Now - process.StartTime;
            var cpuTime = process.TotalProcessorTime;
            var cpuUsage = cpuTime.TotalMilliseconds / (uptime.TotalMilliseconds * Environment.ProcessorCount) * 100;
            
            // Get storage metrics
            var storagePath = _configuration["FileStorage:LocalPath"] ?? 
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileStorage");
            
            var directoryInfo = new DirectoryInfo(storagePath);
            long totalSpace = 0;
            long availableSpace = 0;
            
            if (directoryInfo.Exists)
            {
                var driveInfo = new DriveInfo(directoryInfo.Root.FullName);
                totalSpace = driveInfo.TotalSize;
                availableSpace = driveInfo.AvailableFreeSpace;
            }
            
            // Get memory usage
            var memoryUsage = process.WorkingSet64;
            
            // Get active connections (simplified metric)
            var activeConnections = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .Count(c => c.State == System.Net.NetworkInformation.TcpState.Established);
            
            return Ok(new
            {
                status = "healthy",
                nodeId = _configuration["NodeId"],
                uptime = uptime.ToString(),
                cpuUsage = Math.Round(cpuUsage, 2),
                memoryUsageBytes = memoryUsage,
                memoryUsageMB = Math.Round(memoryUsage / 1024.0 / 1024.0, 2),
                totalStorageBytes = totalSpace,
                availableStorageBytes = availableSpace,
                activeConnections = activeConnections,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating health report");
            return StatusCode(500, new { status = "error", message = "Error generating health report" });
        }
    }
}