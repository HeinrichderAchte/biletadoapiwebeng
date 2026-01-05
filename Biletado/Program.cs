using Biletado;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Biletado.Repository.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Enable detailed IdentityModel error messages in Development for debugging token issues
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

// Setup Serilog early so startup logs are captured
var serilogConfig = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .Enrich.WithProperty("environment", builder.Environment.EnvironmentName)
    .Enrich.WithProperty("machine", System.Environment.MachineName);

Log.Logger = serilogConfig.CreateLogger();

// integrate Serilog into generic host logging
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// central flag to enable/disable authentication globally (default = false)
var authEnabled = builder.Configuration.GetValue<bool?>("Authentication:Enabled") ?? false;
Log.Information("Authentication enabled: {AuthEnabled}", authEnabled);

// Authentication configuration will be read from configuration (appsettings or environment variables)
// Expected keys (when enabled): Authentication:Authority (issuer URL), Authentication:Audience (API audience)

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

    // register custom OperationFilter to tweak the Reservations GET parameter descriptions and examples
    c.OperationFilter<ReservationsOperationFilter>();

    // register enum schema filter so enums are displayed as string names in Swagger
    c.SchemaFilter<EnumSchemaFilter>();

    // register uuid parameter filter to relax the pattern to accept upper-case hex
    c.OperationFilter<UuidParameterFilter>();

    // Only add Swagger JWT security when auth is enabled
    if (authEnabled)
    {
        c.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        // Only add the security requirement to operations that actually require authorization
        c.OperationFilter<AuthorizeCheckOperationFilter>();
    }
});

// Configure JSON serializer to use string enums (so API expects 'Replace'/'Restore' strings)
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure authentication: JWT Bearer only when enabled
if (authEnabled)
{
    var authority = builder.Configuration["Authentication:Authority"];
    var audience = builder.Configuration["Authentication:Audience"];

    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException("Authentication is enabled but Authentication:Authority or Authentication:Audience is not configured. Set these values in configuration or disable authentication.");
    }

    // Explicitly set default authentication/challenge schemes to avoid 'no default scheme' errors
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        // Accept tokens from the authority; for local dev set RequireHttpsMetadata=false if needed
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Authentication:RequireHttpsMetadata") ?? true;

        // read allowed audiences from configuration (optional)
        var allowedAudiences = builder.Configuration.GetSection("Authentication:ValidAudiences").Get<string[]>() ?? new[] { audience };

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "roles",
            ValidAudiences = allowedAudiences
        };

        // Optional: add events for helpful debugging
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine("Auth failed: " + ctx.Exception?.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                Console.WriteLine("Token validated for: " + ctx.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();
}
else
{
    // Authentication disabled central - do not register auth services here (we may register DevAuth below for Development)
    Console.WriteLine("Authentication disabled via configuration (Authentication:Enabled=false)");

    // If we're in Development, register a simple DevAuth scheme as default so [Authorize] works without real tokens
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "DevAuth";
            options.DefaultChallengeScheme = "DevAuth";
        })
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Biletado.Repository.DevAuth.DevAuthHandler>("DevAuth", options => { });

        builder.Services.AddAuthorization();
        Console.WriteLine("DevAuth registered as default authentication scheme for Development.");
    }
}

builder.Services.AddDbContext<AssetsDbContext>(options=>options.UseNpgsql(builder.Configuration.GetConnectionString("AssetsConnection")));
// Register ReservationsDbContext using the correct connection string key and fail fast if missing
{
    var reservationsConn = builder.Configuration.GetConnectionString("ReservationConnection");
    if (string.IsNullOrWhiteSpace(reservationsConn))
    {
        throw new InvalidOperationException("Connection string 'ReservationConnection' is not configured.");
    }
    builder.Services.AddDbContext<ReservationsDbContext>(options =>
        options
            .UseNpgsql(reservationsConn)
            .LogTo(message => Console.WriteLine(message), LogLevel.Information)
            .EnableSensitiveDataLogging()
    );
}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication + Authorization middleware (order matters). Only add when enabled or DevAuth registered.
app.UseHttpsRedirection();
if (authEnabled || app.Environment.IsDevelopment())
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();


app.Run();

