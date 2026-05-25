using CPMS.Api.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(CpmsDbContext dbContext, JwtTokenService tokenService) : ControllerBase
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
        var accessToken = tokenService.CreateAccessToken(user);
        var (rawRefreshToken, refreshEntity) = tokenService.CreateRefreshToken(user, Request.Headers.UserAgent);
        dbContext.RefreshTokens.Add(refreshEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TokenResponse(accessToken, rawRefreshToken, refreshEntity.ExpiresAt));
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
}

public sealed record LoginRequest(string? Username, string? ExaminerCode, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt);
