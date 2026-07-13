using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using CPMS.Api.Configuration;
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
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
ApplicationConfiguration.AddDeploymentOverrides(builder.Configuration);

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
                    var existingRoles = identity.Claims
                        .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                        .Select(c => c.Value)
                        .Distinct()
                        .ToList();

                    foreach (var roleVal in existingRoles)
                    {
                        if (!identity.HasClaim("role", roleVal))
                        {
                            identity.AddClaim(new Claim("role", roleVal));
                        }
                        if (!identity.HasClaim(ClaimTypes.Role, roleVal))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, roleVal));
                        }
                    }
                }
                return Task.CompletedTask;
            },
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
    policy.SetIsOriginAllowed(origin => origins.Length == 0 || origins.Contains(origin) || origin.StartsWith("http://localhost:") || origin.StartsWith("http://127.0.0.1:"))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
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
        await Program.SeedModeratorAccountAsync(dbContext);
        await Program.SeedTestAccountsAsync(dbContext);
        await Program.SeedTestAcademicDataAsync(dbContext);
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
    return configuration.GetConnectionString("CpmsDatabase")
        ?? throw new InvalidOperationException("Connection string 'CpmsDatabase' is missing.");
}

public partial class Program
{
    public static async Task SeedModeratorAccountAsync(CpmsDbContext dbContext)
    {
        var username = "Admin@gmail.com";
        var email = "Admin@gmail.com";
        var password = "12345";

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Username == username || x.Email == email);

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

    public static async Task SeedTestAccountsAsync(CpmsDbContext dbContext)
    {
        var testAccounts = new[]
        {
            ("test.training", "test.training@cpms.local", "Test@123456", UserRole.TrainingDepartment),
            ("test.lecturer", "test.lecturer@cpms.local", "Test@123456", UserRole.Lecturer),
            ("test.student", "test.student@cpms.local", "Test@123456", UserRole.Student)
        };

        foreach (var (username, email, password, role) in testAccounts)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == username || x.Email == email);
            if (user is null)
            {
                user = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                    Role = role,
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
                user.Role = role;
                user.IsActive = true;
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                user.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            if (role == UserRole.TrainingDepartment && !await dbContext.TrainingDepartments.AnyAsync(x => x.UserId == user.Id))
            {
                dbContext.TrainingDepartments.Add(new TrainingDepartment { UserId = user.Id, DepartmentName = "Training Dept", StaffCode = "TD_TEST", Position = "Officer" });
            }
            else if (role == UserRole.Lecturer && !await dbContext.Lecturers.AnyAsync(x => x.UserId == user.Id))
            {
                dbContext.Lecturers.Add(new Lecturer { UserId = user.Id, Code = "LEC_TEST", FullName = "Dr. Test Lecturer", Department = "SWD" });
            }
            else if (role == UserRole.Student && !await dbContext.Students.AnyAsync(x => x.UserId == user.Id))
            {
                dbContext.Students.Add(new Student { UserId = user.Id, Code = "SE190000", FullName = "Test Student", ClassCode = "SE1901", Major = "SWD", GroupId = null, IsLeader = false });
            }

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }

    public static async Task SeedTestAcademicDataAsync(CpmsDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var lecturer = await dbContext.Lecturers.SingleOrDefaultAsync(x => x.Code == "LEC_TEST")
            ?? throw new InvalidOperationException("The test lecturer profile must be seeded before test academic data.");
        var semester = await dbContext.Semesters.FirstOrDefaultAsync(x => x.IsActive);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        if (semester is null)
        {
            semester = await dbContext.Semesters.FirstOrDefaultAsync(x => x.Code == "SU2026");
            if (semester is null)
            {
                semester = new Semester
                {
                    Code = "SU2026",
                    Name = "Summer 2026",
                    AcademicYear = "2025-2026",
                    StartDate = new DateOnly(2026, 5, 1),
                    EndDate = new DateOnly(2026, 8, 31),
                    IsActive = true
                };
                dbContext.Semesters.Add(semester);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                semester.IsActive = true;
            }
        }

        const string projectTitle = "Smart Home Inventory";
        var topic = await dbContext.Topics.FirstOrDefaultAsync(x =>
            x.SemesterId == semester.Id && (x.NameVn == projectTitle || x.NameEn == projectTitle));
        if (topic is null)
        {
            topic = new Topic
            {
                Code = BuildUniqueTestTopicCode(semester.Code),
                NameVn = projectTitle,
                NameEn = projectTitle,
                SemesterId = semester.Id
            };
            dbContext.Topics.Add(topic);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            topic.NameVn = projectTitle;
            topic.NameEn = projectTitle;
        }

        var group = await dbContext.CapstoneGroups.FirstOrDefaultAsync(x =>
            x.SemesterId == semester.Id && x.TopicId == topic.Id);
        if (group is null)
        {
            group = new CapstoneGroup
            {
                Code = "TEST_GROUP_SMART_HOME",
                TopicId = topic.Id,
                SemesterId = semester.Id,
                LecturerId = lecturer.Id,
                Status = GroupStatus.Active
            };
            dbContext.CapstoneGroups.Add(group);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            group.LecturerId = lecturer.Id;
            group.Status = GroupStatus.Active;
        }

        var students = new[]
        {
            new TestStudentSeed("Huỳnh Nhật Thịnh", "SE194673", "duongduy12317@gmail.com", false),
            new TestStudentSeed("Phạm Thị Ánh", "SE182446", "thanhduykx@gmail.com", false),
            new TestStudentSeed("Dương Thành Thanh Duy", "SS180745", "victornhatthinh@gmail.com", true),
            new TestStudentSeed("Trương Đinh Hồng Nhung", "SS194356", "a@gmail.com", false),
            new TestStudentSeed("Vũ Minh Quang", "SE192755", "acc@gmail.com", false),
            new TestStudentSeed("Nguyễn Minh Thành", "SE191034", "ab@gmail.com", false)
        };
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", workFactor: 12);

        foreach (var seed in students)
        {
            var student = await dbContext.Students.FirstOrDefaultAsync(x => x.Code == seed.Code);
            var username = AccountUsernameGenerator.Generate(seed.FullName, seed.Code);
            User? user;

            if (student is not null)
            {
                user = await dbContext.Users.SingleAsync(x => x.Id == student.UserId);
            }
            else
            {
                user = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == username || x.Email == seed.Email);
            }

            if (user is null)
            {
                user = new User
                {
                    Username = username,
                    Email = seed.Email,
                    PasswordHash = passwordHash,
                    Role = UserRole.Student,
                    IsActive = true
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                if (user.Role != UserRole.Student)
                {
                    throw new InvalidOperationException($"Test student {seed.Code} conflicts with a non-student account.");
                }

                user.Username = username;
                user.Email = seed.Email;
                user.PasswordHash = passwordHash;
                user.IsActive = true;
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                user.UpdatedAt = DateTime.UtcNow;
            }

            if (student is null)
            {
                student = new Student
                {
                    UserId = user.Id,
                    Code = seed.Code,
                    FullName = seed.FullName,
                    ClassCode = "EXE101",
                    Major = "Unspecified",
                    GroupId = group.Id,
                    IsLeader = seed.IsLeader
                };
                dbContext.Students.Add(student);
            }
            else
            {
                student.FullName = seed.FullName;
                student.ClassCode = "EXE101";
                student.Major = "Unspecified";
                student.GroupId = group.Id;
                student.IsLeader = seed.IsLeader;
            }
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static string BuildUniqueTestTopicCode(string semesterCode)
    {
        var code = $"TEST_TOPIC_{semesterCode}_SMART_HOME";
        return code.Length <= 50 ? code : code[..50];
    }

    private sealed record TestStudentSeed(string FullName, string Code, string Email, bool IsLeader);
}
