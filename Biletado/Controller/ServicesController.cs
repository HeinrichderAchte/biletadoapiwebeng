using Elastic.CommonSchema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Biletado.Controller;

[Route("api/v3/reservations")]
[ApiController]
public class ServicesController : ControllerBase
{
    private readonly AssetsDbContext _assetsDb;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(AssetsDbContext assetsDb, ILogger<ServicesController> logger)
    {
        _assetsDb = assetsDb;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {   
        _logger.LogInformation("Reservations Database connection established." +
                               "RemoteIP: {RemoteIP} ", HttpContext.Connection.RemoteIpAddress.ToString() ?? "unknown");
        return Ok(new
        {
            authors= new[]{"Henri Weber", "Vivian Heidt"},
            api_version="1.1.0",
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
            _logger.LogError("Reservations Database connection failed and health check could not be completed.");
        }

        if (assetsConnected)
        {
            _logger.LogInformation("Reservations Database is ready."); 
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
        var serviceUnavailable = new
        {
            code = 503, 
            message = "Service Unavailable",
            live = true,
            ready = assetsConnected,
        };
        return StatusCode(503, serviceUnavailable);
    }

    [HttpGet("health/live")]
    public IActionResult GetHealthLive()
    {
        
        var result = new
        {
            live = true
        };
        _logger.LogInformation("The process is alive."); 
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
            _logger.LogError("Reservations Database connection failed and readiness check could not be completed.");
        }

        if (assetsConnected)
        {
            _logger.LogInformation("Reservations Database connection established.");
            var result = new
            {
                ready = true
            }; 
            return Ok(result);
        }
        var serviceUnavailable = new
        {
            code = 503, 
            message = "Service Unavailable",
            ready = false
        };
        return StatusCode(503, serviceUnavailable);

        
    }
}