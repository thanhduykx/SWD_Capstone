using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    CpmsDbContext dbContext,
    JwtTokenService tokenService,
    IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loginCode = request.Username ?? request.ExaminerCode;
        if (string.IsNullOrWhiteSpace(loginCode))
        {
            return BadRequest(new { error = "Username or examinerCode is required." });
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Username == loginCode, cancellationToken);
        if (user is null || !user.IsActive || user.LockedUntil > DateTime.UtcNow)
        {
            return Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Unauthorized();
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        return Ok(await IssueTokenAsync(user, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<TokenResponse>> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clientId = configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Google login is not configured." });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized();
        }

        if (!payload.EmailVerified)
        {
            return Unauthorized(new { error = "Google email is not verified." });
        }

        var email = payload.Email.Trim();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsActive || user.LockedUntil > DateTime.UtcNow)
        {
            return Unauthorized();
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        return Ok(await IssueTokenAsync(user, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hash = JwtTokenService.HashRefreshToken(request.RefreshToken);
        var existing = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (existing is null || existing.IsRevoked || existing.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users.SingleAsync(x => x.Id == existing.UserId, cancellationToken);
        if (!user.IsActive)
        {
            return Unauthorized();
        }

        existing.IsRevoked = true;
        var (rawRefreshToken, rotatedToken) = tokenService.CreateRefreshToken(user, Request.Headers.UserAgent);
        dbContext.RefreshTokens.Add(rotatedToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TokenResponse(tokenService.CreateAccessToken(user), rawRefreshToken, rotatedToken.ExpiresAt));
    }

    [AllowAnonymous]
    [HttpPost("bootstrap-admin")]
    public async Task<ActionResult> BootstrapAdmin(BootstrapAdminRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return Conflict(new { error = "Bootstrap is only allowed before the first account exists." });
        }

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username, email and password are required." });
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = UserRole.SystemAdministrator,
            IsActive = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.SystemAdministrators.Add(new SystemAdministrator
        {
            UserId = user.Id,
            AdminLevel = "Root",
            PermissionScope = "DevelopmentBootstrap"
        });
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "BOOTSTRAP_ADMIN",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = user.Username,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(BootstrapAdmin), new { user.Id }, new
        {
            user.Id,
            user.Username,
            user.Role
        });
    }

    private async Task<TokenResponse> IssueTokenAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var (rawRefreshToken, refreshEntity) = tokenService.CreateRefreshToken(user, Request.Headers.UserAgent);
        dbContext.RefreshTokens.Add(refreshEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponse(accessToken, rawRefreshToken, refreshEntity.ExpiresAt);
    }
}

public sealed record LoginRequest(string? Username, string? ExaminerCode, string Password);
public sealed record GoogleLoginRequest(string IdToken);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt);
public sealed record BootstrapAdminRequest(string Username, string Email, string Password);
