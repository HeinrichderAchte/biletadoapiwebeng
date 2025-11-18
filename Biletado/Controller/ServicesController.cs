using Microsoft.AspNetCore.Mvc;

namespace Biletado.Controller;

[Route("api/v3/reservations")]
[ApiController]
public class ServicesController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            authors= new[]{"Henri Weber", "Vivian Heidt"},
            api_version="3.0.0",
        });
    }
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok();
    }
    [HttpGet("health/live")]
    public IActionResult GetHealthLive()
    {
        return Ok();
    }
    [HttpGet("healt/ready")]
    public IActionResult GetHealtReady()
    {
        return Ok();
    }
}