using Microsoft.AspNetCore.Mvc;

namespace Biletado.Controller;

[Route("api/v3/reservations")]
[ApiController]
public class ServicesController : ControllerBase
{
    private readonly AssetsDbContext _assetsDb;

    public ServicesController(AssetsDbContext assetsDb)
    {
        _assetsDb = assetsDb;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            authors= new[]{"Henri Weber", "Vivian Heidt"},
            api_version="1.0.5",
        });
    }

    [HttpGet("health")]
    public async Task <IActionResult> GetHealth()
    {
        bool assetsConnected = false;
        try
        {
            assetsConnected = await _assetsDb.Database.CanConnectAsync();

        }
        catch
        {
            assetsConnected = false;
        }

        var result = new
        {
            live = true,
            ready = assetsConnected,
            databases = new
            {
                assets = new
                {
                    connected = assetsConnected
                }
            }
        };
        return Ok(result);
    }

    [HttpGet("health/live")]
    public IActionResult GetHealthLive()
    {
        var result = new
        {
            live = true
        };
        return Ok(result);
    }

    [HttpGet("health/ready")]
    public async Task <IActionResult> GetHealtReady()
    {
        bool assetsConnected = false;
        try
        {
            assetsConnected = await _assetsDb.Database.CanConnectAsync();

        }
        catch
        {
            assetsConnected = false;
        }

        var result = new
        {
            ready = assetsConnected
        };
        return Ok(result);
    }
}