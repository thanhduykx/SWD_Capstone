using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using CPMS.API.Configuration;
using CPMS.API.Hubs;
using CPMS.API.Middleware;
using CPMS.API.Services;
using CPMS.API.Swagger;
using CPMS.BLL;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Accounts;
using CPMS.BLL.Reviews.Assessment;
using CPMS.BLL.Services;
using CPMS.DAL;
using CPMS.DAL.Data;
using CPMS.DAL.ExternalServices;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;

// Render injects PORT and requires binding on 0.0.0.0 (not localhost).
// Set ASPNETCORE_URLS before CreateBuilder — clearing Configuration later can drop UseUrls().
ConfigurePublicHttpBinding();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IConfigurationRoot appSettingsConfiguration = new ConfigurationBuilder()
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();
builder.Configuration.Sources.Clear();
builder.Configuration.AddConfiguration(appSettingsConfiguration);
ApplicationConfiguration.AddDeploymentOverrides(builder.Configuration);
// Re-apply after Sources.Clear() so Kestrel keeps the public bind address.
string? publicUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrWhiteSpace(publicUrls))
{
#pragma warning disable S5332
    builder.WebHost.UseUrls(publicUrls);
#pragma warning restore S5332
}

var cpmsConnectionString = GetCpmsConnectionString(builder.Configuration);
var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        "logs/cpms-.log",
        formatProvider: CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day));

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CpmsDbContext>(options =>
    options.UseNpgsql(cpmsConnectionString).UseSnakeCaseNamingConvention());
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<CpmsDbContext>());
builder.Services.AddBLLServices();
// Đọc Gemini từ builder.Configuration (appsettings + env Gemini__ApiKey trên Render).
builder.Services.AddDALServices(builder.Configuration);
builder.Services.AddSingleton<ReviewChecklistTemplateService>();
builder.Services.AddSingleton<IReviewChecklistTemplateProvider>(provider =>
    provider.GetRequiredService<ReviewChecklistTemplateService>());
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuthenticationTokenIssuer>(provider => provider.GetRequiredService<JwtTokenService>());
builder.Services.AddScoped<IGoogleIdentityVerifier, GoogleIdentityVerifier>();

builder.Services.AddCpmsSecurity(builder.Configuration);
builder.Services.AddCpmsTrafficPolicies(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<CpmsDbContext>("postgresql", tags: ["ready"]);
if (builder.Configuration.GetValue<bool>("Hangfire:Enabled"))
{
    builder.Services.AddHangfire(configuration =>
        configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(cpmsConnectionString)));
    builder.Services.AddHangfireServer();
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CPMS Enterprise API",
        Version = "v1",
        Description = "Backend API for CPMS review and defense workflows."
    });
    options.OperationFilter<SwaggerTestingOperationFilter>();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
});

WebApplication app = builder.Build();
await InitializeDatabaseAsync(app);

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseSerilogRequestLogging();
if (app.Environment.IsDevelopment() || swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "CPMS Enterprise API";
        options.EnablePersistAuthorization();
    });
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Configuration.GetValue<bool>("Hangfire:Enabled")) app.UseHangfireDashboard("/jobs");
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live", StringComparer.OrdinalIgnoreCase)
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready", StringComparer.OrdinalIgnoreCase)
});
app.MapGet("/health/storage", (ProjectDocumentStorageOptions options) => Results.Ok(new
{
    status = "Healthy",
    provider = options.Provider,
    configured = !string.Equals(options.Provider, "Supabase", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(options.SupabaseUrl) && !string.IsNullOrWhiteSpace(options.ServiceRoleKey))
}));
app.MapGet("/health/database", async (
    IDatabaseLifecycleService database, CancellationToken cancellationToken) =>
{
    try
    {
        return await database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "Healthy", database = "PostgreSQL" })
            : Results.Problem("PostgreSQL is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (NpgsqlException exception)
    {
        return Results.Problem(exception.Message, "PostgreSQL is not reachable",
            StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapHub<DefenseScoringHub>("/hubs/defense");
app.MapHub<PresenceHub>("/hubs/presence");
app.MapHub<ReviewProgressHub>("/hubs/review-progress");
app.MapGet("/", () => Results.Redirect("/swagger"));
await app.RunAsync();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    try
    {
        using IServiceScope scope = app.Services.CreateScope();
        IDatabaseLifecycleService database = scope.ServiceProvider.GetRequiredService<IDatabaseLifecycleService>();
        var seedDevelopmentData = app.Environment.IsDevelopment() ||
                                  app.Configuration.GetValue<bool>("Swagger:ExposeTestSupport");
        await database.InitializeAsync(seedDevelopmentData, CancellationToken.None);
    }
    catch (NpgsqlException exception)
    {
        Microsoft.Extensions.Logging.ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
        logger.LogWarning(exception,
            "PostgreSQL is not reachable. DB-backed APIs will fail until PostgreSQL is reachable.");
    }
}

static string GetCpmsConnectionString(IConfiguration configuration) =>
    configuration.GetConnectionString("CpmsDatabase")
    ?? throw new InvalidOperationException("Connection string 'CpmsDatabase' is missing.");

static void ConfigurePublicHttpBinding()
{
    // Prefer Render PORT; fall back to container HTTP ports / 8080.
    string configuredPort = Environment.GetEnvironmentVariable("PORT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")
        ?? "8080";
    configuredPort = configuredPort.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault()
        ?? "8080";
    if (!int.TryParse(configuredPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out int httpPort)
        || httpPort is < 1 or > 65535)
    {
        throw new InvalidOperationException($"The configured HTTP port '{configuredPort}' is invalid.");
    }

    // Force all-interfaces bind. localhost:* makes Render port-scan time out.
    // HTTP is intentional behind Render's TLS-terminating proxy.
#pragma warning disable S5332
    Environment.SetEnvironmentVariable(
        "ASPNETCORE_URLS",
        string.Create(CultureInfo.InvariantCulture, $"http://0.0.0.0:{httpPort}"));
#pragma warning restore S5332
}

#pragma warning disable S1118 // ASP.NET WebApplicationFactory requires an instantiable Program marker.
public partial class Program;
#pragma warning restore S1118
