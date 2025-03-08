using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Healthy");
    }
}