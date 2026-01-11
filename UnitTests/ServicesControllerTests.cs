// csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Biletado;
using Biletado.Controller;
using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Biletado.Persistence.Contexts;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace UnitTests;

public class ServicesControllerTests
{
    private readonly ITestOutputHelper _output;

    public ServicesControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private AssetsDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AssetsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AssetsDbContext(options);
    }

    private void LogScenario(string testName, string dbName, IActionResult? result)
    {
        _output.WriteLine("========================================");
        _output.WriteLine($"Test: {testName}");
        _output.WriteLine($"InMemoryDb: {dbName}");
        _output.WriteLine($"ResultType: {result?.GetType().Name ?? "null"}");

        if (result is OkObjectResult ok)
        {
            _output.WriteLine($"StatusCode: {ok.StatusCode ?? 200}");
            _output.WriteLine($"Value: {ok.Value ?? "null"}");
        }
        else if (result is ObjectResult obj)
        {
            _output.WriteLine($"StatusCode: {obj.StatusCode?.ToString() ?? "null"}");
            _output.WriteLine($"Value: {obj.Value ?? "null"}");
        }

        _output.WriteLine("========================================");
    }

    [Fact]
    public void GetStatus_ReturnsOk()
    {
        using var ctx = CreateInMemoryContext("status_db");
        var logger = NullLogger<ServicesController>.Instance;
        var httpContextAccessor = new HttpContextAccessor();
        var iamOptions = Options.Create(new IamOptions { Endpoint = "http://test-iam.local" });
        var controller = new ServicesController(ctx, logger, httpContextAccessor, iamOptions);

        var result = controller.GetStatus();

        LogScenario(nameof(GetStatus_ReturnsOk), "status_db", result);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetHealth_ReturnsOk_WhenDbCanConnect()
    {
        using var ctx = CreateInMemoryContext("health_db");
        var logger = NullLogger<ServicesController>.Instance;
        var httpContextAccessor = new HttpContextAccessor();
        var iamOptions = Options.Create(new IamOptions { Endpoint = "http://test-iam.local" });
        var controller = new ServicesController(ctx, logger, httpContextAccessor, iamOptions);

        var result = await controller.GetHealth();

        LogScenario(nameof(GetHealth_ReturnsOk_WhenDbCanConnect), "health_db", result);

        Assert.IsType<OkObjectResult>(result);
    }
}
