using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Biletado;
using Biletado.Controller;
using Xunit;
using Microsoft.AspNetCore.Mvc;

namespace UnitTests;

public class ServicesControllerTests
{
    private AssetsDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AssetsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AssetsDbContext(options);
    }

    [Fact]
    public void GetStatus_ReturnsOk()
    {
        using var ctx = CreateInMemoryContext("status_db");
        var logger = NullLogger<ServicesController>.Instance;
        var controller = new ServicesController(ctx, logger);

        var result = controller.GetStatus();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetHealth_ReturnsOk_WhenDbCanConnect()
    {
        using var ctx = CreateInMemoryContext("health_db");
        var logger = NullLogger<ServicesController>.Instance;
        var controller = new ServicesController(ctx, logger);

        var result = await controller.GetHealth();

        Assert.IsType<OkObjectResult>(result);
    }
}

