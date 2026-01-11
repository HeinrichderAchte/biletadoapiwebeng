using Biletado;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json.Serialization;
using Biletado.Persistence.Contexts;
using Biletado.Services;
using Serilog;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var authEnabled = builder.Configuration.GetValue<bool>("Authentication:Enabled");

if (!authEnabled)
{
    // Registriere NoAuth als Default-Scheme, verhindert die 'No authenticationScheme was specified' Exception
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "NoAuth";
            options.DefaultChallengeScheme = "NoAuth";
            options.DefaultScheme = "NoAuth";
        })
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Biletado.Repository.DevAuth.NoAuthHandler>(
            "NoAuth", options => { });

    // Optional: Falls noch [Authorize] vorhanden ist, erlaubt diese FallbackPolicy alle Anfragen
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });
}

// Loggin Configuration is read from appsettings.json 
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("environment", builder.Environment.EnvironmentName)
    .CreateLogger();


builder.Host.UseSerilog(); 
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations(); // erlaubt [SwaggerOperation], [SwaggerParameter], [SwaggerSchema]
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure JSON serializer to use string enums
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var isDevelopment = builder.Environment.IsDevelopment();

// Register IHttpContextAccessor for controllers/tests that need the remote IP etc.
builder.Services.AddHttpContextAccessor();

// Bind IAM options if present
builder.Services.Configure<IamOptions>(builder.Configuration.GetSection("IAM"));

// Databases: try ConnectionStrings -> Environment variables -> fallback to InMemory for local/dev
var assetsConn = builder.Configuration.GetConnectionString("AssetsConnection")
                 ?? builder.Configuration.GetConnectionString("Assets")
                 ?? Environment.GetEnvironmentVariable("ASSETS_CONNECTION");

if (!string.IsNullOrWhiteSpace(assetsConn))
{
    builder.Services.AddDbContext<AssetsDbContext>(options =>
        options.UseNpgsql(assetsConn));
}
else
{
    // Fallback to InMemory for development / tests
    builder.Services.AddDbContext<AssetsDbContext>(options =>
        options.UseInMemoryDatabase("Assets_Dev"));
}

{
    var reservationsConn = builder.Configuration.GetConnectionString("ReservationConnection")
                          ?? builder.Configuration.GetConnectionString("Reservations")
                          ?? Environment.GetEnvironmentVariable("RESERVATION_CONNECTION");
    if (!string.IsNullOrWhiteSpace(reservationsConn))
    {
        builder.Services.AddDbContext<ReservationsDbContext>(options =>
            options
                .UseNpgsql(reservationsConn)
                .LogTo(message => Console.WriteLine(message), LogLevel.Information)
                .EnableSensitiveDataLogging()
        );
    }
    else
    {
        // Previously threw if missing; keep app runnable locally by falling back to InMemory and log a warning
        Console.WriteLine("Warning: ReservationConnection not configured, using InMemory database for ReservationsDbContext.");
        builder.Services.AddDbContext<ReservationsDbContext>(options =>
            options
                .UseInMemoryDatabase("Reservations_Dev")
                .EnableSensitiveDataLogging()
        );
    }
}

builder.Services.AddScoped<IReservationService, ReservationService>();


try
{
    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.MapControllers();
    app.UseAuthentication();
    app.UseAuthorization();
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
