using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using CPMS.Api.Hubs;
using CPMS.Api.Middleware;
using CPMS.Api.Services;
using CPMS.Api.Swagger;
using CPMS.Application;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Services;
using CPMS.Infrastructure;
using CPMS.Infrastructure.Data;
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
var cpmsConnectionString = GetCpmsConnectionString(builder.Configuration);
var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/cpms-.log", rollingInterval: RollingInterval.Day);
});

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CpmsDbContext>(options =>
    options.UseNpgsql(cpmsConnectionString)
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<CpmsDbContext>());
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddScoped<AssignmentRules>();
builder.Services.AddScoped<DefenseScoringService>();
builder.Services.AddScoped<SemesterResolverService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddSingleton<ReviewChecklistTemplateService>();
builder.Services.AddScoped<IReviewEmailSender, SmtpReviewEmailSender>();
builder.Services.AddScoped<ReviewAssignmentEmailNotifier>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<JwtTokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException("JWT key must be at least 32 characters.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/defense"))
                {
                    context.Token = accessToken;
                }

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
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
    .AddApiVersioning(options =>
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
        Description = """
                      Backend API test flow:
                      1. Open GET /api/test-support/swagger-guide for the role matrix and recommended flows.
                      2. Login with POST /api/auth/login.
                      3. Copy accessToken, click Authorize, paste the token value, then call protected endpoints.
                      4. Use ids returned from GET endpoints instead of guessing ids.
                      """
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
if (app.Configuration.GetValue<bool>("Hangfire:Enabled"))
{
    app.UseHangfireDashboard("/jobs");
}
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live", StringComparer.OrdinalIgnoreCase)
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready", StringComparer.OrdinalIgnoreCase)
});
app.MapGet("/health/database", async (CpmsDbContext dbContext) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "Healthy", database = "PostgreSQL" })
            : Results.Problem("PostgreSQL is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Npgsql.NpgsqlException exception)
    {
        return Results.Problem(
            detail: exception.Message,
            title: "PostgreSQL is not reachable",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapHub<DefenseScoringHub>("/hubs/defense");
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CpmsDbContext>();
        await dbContext.Database.MigrateAsync();
        await Program.SeedAdminAccountAsync(app.Configuration, dbContext);
        await Program.SeedModeratorAccountAsync(dbContext);



        // Business data must come from official import/admin flows. Admin seeding is opt-in for deployments.
    }
    catch (Npgsql.NpgsqlException exception)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
        logger.LogWarning(exception,
            "PostgreSQL is not reachable. The web app will start, but DB-backed APIs will fail until PostgreSQL is configured and reachable.");
    }
}



static string GetCpmsConnectionString(IConfiguration configuration)
{
    var databaseUrl = configuration["DATABASE_URL"];
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var credentials = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials.ElementAtOrDefault(0) ?? string.Empty),
            Password = Uri.UnescapeDataString(credentials.ElementAtOrDefault(1) ?? string.Empty),
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }

    return configuration.GetConnectionString("CpmsDatabase")
        ?? throw new InvalidOperationException("Connection string 'CpmsDatabase' is missing.");
}

public partial class Program
{
    public static async Task SeedAdminAccountAsync(IConfiguration configuration, CpmsDbContext dbContext)
    {
        if (!configuration.GetValue<bool>("AdminSeed:Enabled"))
        {
            return;
        }

        var username = configuration["AdminSeed:Username"]?.Trim();
        var email = configuration["AdminSeed:Email"]?.Trim();
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Admin seed is enabled, but AdminSeed:Username, AdminSeed:Email, or AdminSeed:Password is missing.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Username == username || x.Email == email);

        if (user is null)
        {
            user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                Role = UserRole.SystemAdministrator,
                IsActive = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            user.Username = username;
            user.Email = email;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            user.Role = UserRole.SystemAdministrator;
            user.IsActive = true;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.SystemAdministrators.AnyAsync(x => x.UserId == user.Id))
        {
            dbContext.SystemAdministrators.Add(new SystemAdministrator
            {
                UserId = user.Id,
                AdminLevel = "Root",
                PermissionScope = "System"
            });
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "SEED_ADMIN_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = user.Username,
            UserAgent = "ApplicationStartup"
        });
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public static async Task SeedModeratorAccountAsync(CpmsDbContext dbContext)
    {
        var username = "Admin@gmail.com";
        var email = "Admin@gmail.com";
        var password = "12345";

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Username == username || x.Email == email);

        if (user is null)
        {
            user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                Role = UserRole.TrainingDepartment,
                IsActive = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            user.Username = username;
            user.Email = email;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            user.Role = UserRole.TrainingDepartment;
            user.IsActive = true;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.TrainingDepartments.AnyAsync(x => x.UserId == user.Id))
        {
            dbContext.TrainingDepartments.Add(new TrainingDepartment
            {
                UserId = user.Id,
                DepartmentName = "Training Department",
                StaffCode = "TD001",
                Position = "Moderator"
            });
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "SEED_MODERATOR_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = user.Username,
            UserAgent = "ApplicationStartup"
        });
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
