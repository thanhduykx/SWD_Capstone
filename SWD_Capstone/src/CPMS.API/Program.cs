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
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
ApplicationConfiguration.AddDeploymentOverrides(builder.Configuration);

var cpmsConnectionString = GetCpmsConnectionString(builder.Configuration);
var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/cpms-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CpmsDbContext>(options =>
    options.UseNpgsql(cpmsConnectionString).UseSnakeCaseNamingConvention());
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<CpmsDbContext>());
builder.Services.AddBLLServices();
builder.Services.AddDALServices(builder.Configuration);
builder.Services.AddScoped<AssignmentRules>();
builder.Services.AddSingleton<ReviewChecklistTemplateService>();
builder.Services.AddSingleton<IReviewChecklistTemplateProvider>(provider =>
    provider.GetRequiredService<ReviewChecklistTemplateService>());
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuthenticationTokenIssuer>(provider => provider.GetRequiredService<JwtTokenService>());
builder.Services.AddScoped<IGoogleIdentityVerifier, GoogleIdentityVerifier>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
    throw new InvalidOperationException("JWT key must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var roles = identity.Claims
                        .Where(claim => claim.Type == "role" || claim.Type == ClaimTypes.Role ||
                                        claim.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                        .Select(claim => claim.Value).Distinct().ToArray();
                    foreach (var role in roles)
                    {
                        if (!identity.HasClaim("role", role)) identity.AddClaim(new Claim("role", role));
                        if (!identity.HasClaim(ClaimTypes.Role, role))
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                }
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/defense"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .SetIsOriginAllowed(origin => origins.Length == 0 || origins.Contains(origin) ||
        origin.StartsWith("http://localhost:") || origin.StartsWith("http://127.0.0.1:") ||
        origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase) ||
        origin.StartsWith("https://swd-capstone", StringComparison.OrdinalIgnoreCase))
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
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

var app = builder.Build();
await InitializeDatabaseAsync(app);

app.UseSerilogRequestLogging();
app.UseMiddleware<ApiExceptionMiddleware>();
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
app.MapGet("/", () => Results.Redirect("/swagger"));
app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    try
    {
        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IDatabaseLifecycleService>();
        // Idempotent upsert on every deploy/restart (no truncate, no password reset for existing accounts).
        await database.InitializeAsync(seedBaselineDataset: true, CancellationToken.None);
    }
    catch (NpgsqlException exception)
    {
        logger.LogWarning(exception,
            "PostgreSQL is not reachable. DB-backed APIs will fail until PostgreSQL is reachable.");
    }
    catch (Exception exception)
    {
        // Never block API boot if baseline seed fails (e.g. partial conflict with live website data).
        logger.LogError(exception, "Baseline dataset seed failed during startup.");
    }
}

static string GetCpmsConnectionString(IConfiguration configuration) =>
    configuration.GetConnectionString("CpmsDatabase")
    ?? throw new InvalidOperationException("Connection string 'CpmsDatabase' is missing.");

public partial class Program;
