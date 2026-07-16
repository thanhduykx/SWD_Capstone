using System.Globalization;
using System.Security.Claims;
using Asp.Versioning;
using CPMS.API.Contracts.Common;
using CPMS.BLL.Features.Accounts.Commands.CreateAccount;
using CPMS.BLL.Features.Accounts.Queries.GetAccounts;
using CPMS.BLL.Enums;
using CPMS.BLL.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CPMS.BLL.DTOs.Accounts;

namespace CPMS.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounts")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class AccountsController(
    GetAccountsQueryHandler getAccounts,
    CreateAccountCommandHandler createAccount,
    IValidator<CreateAccountCommand> createValidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CPMS.BLL.DTOs.Accounts.AccountResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CPMS.BLL.DTOs.Accounts.AccountResponse>>>> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<AccountResponse> result = await getAccounts.HandleAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(
            result,
            HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CPMS.BLL.DTOs.Accounts.AccountCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CPMS.BLL.DTOs.Accounts.AccountCreatedResponse>>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = new CreateAccountCommand(
            request.IdentityCode,
            request.Email,
            request.Password,
            request.Role,
            request.FullName,
            request.Department,
            request.Position,
            request.PermissionScope,
            request.IsPartTime,
            request.ClassCode,
            request.Batch,
            request.Major,
            CurrentUserId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent);

        await createValidator.ValidateAndThrowAsync(command, cancellationToken);
        AccountCreatedResponse response = await createAccount.HandleAsync(command, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Created(
                response,
                HttpContext.TraceIdentifier));
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);
}

public sealed record CreateAccountRequest(
    string? IdentityCode,
    string Email,
    string Password,
    [property: JsonRequired] UserRole Role,
    string? FullName,
    string? Department,
    string? Position,
    string? PermissionScope,
    [property: JsonRequired] bool IsPartTime,
    string? ClassCode,
    string? Batch,
    string? Major);
