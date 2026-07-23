using CPMS.API.Contracts.Accounts;
using CPMS.BLL.Accounts;
using CPMS.BLL.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthenticationService authenticationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            AuthenticationResult? result = await authenticationService.LoginAsync(
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
            AuthenticationResult? result = await authenticationService.GoogleLoginAsync(
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
        AuthenticationResult? result = await authenticationService.RefreshAsync(
            request.RefreshToken, Request.Headers.UserAgent, cancellationToken);
        return result is null ? Unauthorized() : Ok(Map(result));
    }

    private static TokenResponse Map(AuthenticationResult result) =>
        new(result.AccessToken, result.RefreshToken, result.RefreshTokenExpiresAt);
}
