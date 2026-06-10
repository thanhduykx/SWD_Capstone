using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CPMS.Api.Hubs;
using CPMS.Api.Middleware;
using CPMS.Api.Services;
using CPMS.Api.Swagger;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);
var cpmsConnectionString = GetCpmsConnectionString(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CpmsDbContext>(options =>
    options.UseNpgsql(cpmsConnectionString)
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<AssignmentRules>();
builder.Services.AddScoped<DefenseScoringService>();
builder.Services.AddScoped<SemesterResolverService>();
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
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CPMS API - FE Testing",
        Version = "v1",
        Description = """
                      Frontend test flow:
                      1. Open GET /api/test-support/swagger-guide for the role matrix and recommended flows.
                      2. Login with POST /api/auth/login.
                      3. Copy accessToken, click Authorize, paste the token value, then call protected endpoints.
                      4. Use ids returned from GET endpoints instead of guessing ids.
                      """
    });
    options.CustomOperationIds(SwaggerOperationId);
    options.TagActionsBy(api => [SwaggerTagFor(api)]);
    options.OrderActionsBy(SwaggerOrderFor);
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "CPMS API - FE Testing";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnablePersistAuthorization();
        options.DisplayOperationId();
        options.DocExpansion(DocExpansion.None);
        options.DefaultModelExpandDepth(2);
        options.DefaultModelsExpandDepth(1);
    });
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
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
if (app.Environment.IsProduction() && File.Exists(Path.Combine(app.Environment.WebRootPath, "index.html")))
{
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
}

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CpmsDbContext>();
        await dbContext.Database.MigrateAsync();
        await SeedAdminAccountAsync(app, dbContext);

        // Business data must come from official import/admin flows. Admin seeding is opt-in for deployments.
    }
    catch (Npgsql.NpgsqlException exception)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
        logger.LogWarning(exception,
            "PostgreSQL is not reachable. The web app will start, but DB-backed APIs will fail until PostgreSQL is configured and reachable.");
    }
}

static async Task SeedAdminAccountAsync(WebApplication app, CpmsDbContext dbContext)
{
    var configuration = app.Configuration;
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

static string SwaggerOperationId(ApiDescription apiDescription)
{
    apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller);
    apiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
    controller ??= "Api";
    action ??= apiDescription.HttpMethod ?? "Action";
    return $"{controller}_{action}";
}

static string SwaggerTagFor(ApiDescription apiDescription)
{
    apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller);
    if (apiDescription.RelativePath?.StartsWith("health/", StringComparison.OrdinalIgnoreCase) == true)
    {
        return "99 - Health";
    }

    return controller switch
    {
        "TestSupport" => "00 - FE Test Guide",
        "Auth" => "01 - Auth",
        "Accounts" => "02 - Accounts",
        "Semesters" => "03 - Semesters",
        "ReviewAvailability" => "04 - Review Availability",
        "ReviewScheduling" => "05 - Review Scheduling",
        "ReviewSessions" => "06 - Review Sessions",
        "ReviewSchedules" => "07 - Review Publish",
        "ReviewSubmissions" => "08 - Review Submissions",
        "DefenseManagement" => "09 - Defense Management",
        "DefenseSessions" => "10 - Defense Sessions",
        _ => controller ?? "API"
    };
}

static string SwaggerOrderFor(ApiDescription apiDescription)
{
    var tag = SwaggerTagFor(apiDescription);
    var methodOrder = apiDescription.HttpMethod?.ToUpperInvariant() switch
    {
        "GET" => "1",
        "POST" => "2",
        "PUT" => "3",
        "PATCH" => "4",
        "DELETE" => "5",
        _ => "9"
    };

    return $"{tag}_{methodOrder}_{apiDescription.RelativePath}";
}

public partial class Program;
