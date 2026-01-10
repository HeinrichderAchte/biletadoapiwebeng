using Biletado;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json.Serialization;
using Biletado.Persistence.Contexts;
using Biletado.Services;
using Serilog;

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

// Databases
builder.Services.AddDbContext<AssetsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AssetsConnection")));

{
    var reservationsConn = builder.Configuration.GetConnectionString("ReservationConnection");
    if (string.IsNullOrWhiteSpace(reservationsConn))
    {
        throw new InvalidOperationException("Connection string `ReservationConnection` is not configured.");
    }
    builder.Services.AddDbContext<ReservationsDbContext>(options =>
        options
            .UseNpgsql(reservationsConn)
            .LogTo(message => Console.WriteLine(message), LogLevel.Information)
            .EnableSensitiveDataLogging()
    );
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

