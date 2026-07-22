using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using CPMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace CPMS.API.Configuration;

internal static class ApiSecurityConfiguration
{
    internal static IServiceCollection AddCpmsSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");
        if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
            throw new InvalidOperationException("JWT key must be at least 32 characters.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwt(options, jwtOptions));
        services.AddAuthorization();
        return services;
    }

    internal static IServiceCollection AddCpmsTrafficPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("auth", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
            options.AddPolicy("ai-suggestions", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            .SetIsOriginAllowed(origin => IsAllowedOrigin(origin, origins))
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }

    private static void ConfigureJwt(JwtBearerOptions options, JwtOptions jwtOptions)
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
            OnTokenValidated = context => AddRoleClaims(context.Principal?.Identity as ClaimsIdentity),
            OnMessageReceived = context => SetHubAccessToken(context)
        };
    }

    private static Task AddRoleClaims(ClaimsIdentity? identity)
    {
        if (identity is null) return Task.CompletedTask;
        var roles = identity.Claims
            .Where(claim => claim.Type == "role" || claim.Type == ClaimTypes.Role ||
                            claim.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value).Distinct().ToArray();
        foreach (var role in roles)
        {
            if (!identity.HasClaim("role", role)) identity.AddClaim(new Claim("role", role));
            if (!identity.HasClaim(ClaimTypes.Role, role)) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        return Task.CompletedTask;
    }

    private static Task SetHubAccessToken(MessageReceivedContext context)
    {
        StringValues accessToken = context.Request.Query["access_token"];
        PathString path = context.HttpContext.Request.Path;
        if (!string.IsNullOrWhiteSpace(accessToken) &&
            (path.StartsWithSegments("/hubs/defense") ||
             path.StartsWithSegments("/hubs/presence")))
            context.Token = accessToken;
        return Task.CompletedTask;
    }

    private static bool IsAllowedOrigin(string origin, string[] origins) =>
        origins.Length == 0 || origins.Contains(origin) ||
        origin.StartsWith("http://localhost:", StringComparison.Ordinal) ||
        origin.StartsWith("http://127.0.0.1:", StringComparison.Ordinal);
}
