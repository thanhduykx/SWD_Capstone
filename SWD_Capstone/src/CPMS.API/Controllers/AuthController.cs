using CPMS.API.Contracts.Accounts;
using CPMS.BLL.Accounts;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var result = await authenticationService.LoginAsync(
                request.Username, request.Password, Request.Headers.UserAgent, cancellationToken);
            return result is null ? Unauthorized() : Ok(Map(result));
        }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<TokenResponse>> GoogleLogin(
        GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var result = await authenticationService.GoogleLoginAsync(
                request.IdToken, Request.Headers.UserAgent, cancellationToken);
            return result is null ? Unauthorized() : Ok(Map(result));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(
        RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await authenticationService.RefreshAsync(
            request.RefreshToken, Request.Headers.UserAgent, cancellationToken);
        return result is null ? Unauthorized() : Ok(Map(result));
    }

    [AllowAnonymous]
    [HttpPost("bootstrap-admin")]
    public async Task<ActionResult> BootstrapAdmin(
        BootstrapAdminRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!environment.IsDevelopment()) return NotFound();
        try
        {
            var result = await authenticationService.BootstrapAdminAsync(
                request.Username, request.Email, request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent, cancellationToken);
            return CreatedAtAction(nameof(BootstrapAdmin), new { result.Id }, result);
        }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
    }

    private static TokenResponse Map(AuthenticationResult result) =>
        new(result.AccessToken, result.RefreshToken, result.RefreshTokenExpiresAt);
}
