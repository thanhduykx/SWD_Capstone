using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Accounts;
using CPMS.BLL.Accounts;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class AccountsController(IAccountAdministrationService accountService) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<AccountResponse>> GetAll(
        [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        PagedResult<AccountResult> result = await accountService.GetAllAsync(request, cancellationToken);
        return new PagedResult<AccountResponse>(
            result.Items.Select(Map).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    [HttpPost]
    public async Task<ActionResult<AccountCreatedResponse>> Create(
        CreateAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            AccountCreatedResult result = await accountService.CreateAsync(
                new CreateAccountData(
                    request.Username, request.IdentityCode, request.Email, request.Password, request.Role,
                    request.FullName, request.Department, request.Position, request.PermissionScope,
                    request.IsPartTime, request.ClassCode, request.Batch, request.Major),
                CurrentAudit(),
                cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { result.Id }, new AccountCreatedResponse(
                result.Id, result.Username, result.Email, result.InitialPassword, result.Role,
                result.IsActive, result.LastLoginAt, result.LockedUntil, result.IdentityCode,
                result.EmailDeliveryStatus, result.EmailDeliveryError));
        }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpPatch("{userId:int}/status")]
    public async Task<ActionResult<AccountResponse>> UpdateStatus(
        int userId, UpdateAccountStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return Ok(Map(await accountService.UpdateStatusAsync(
                userId, request.IsActive, request.Unlock, CurrentAudit(), cancellationToken)));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPatch("{userId:int}/role")]
    public async Task<ActionResult<AccountResponse>> UpdateRole(
        int userId, UpdateAccountRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return Ok(Map(await accountService.UpdateRoleAsync(
                userId, request.Role, CurrentAudit(), cancellationToken)));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
    }

    private AccountAuditContext CurrentAudit() => new(
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user identifier."), CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent);
    private static AccountResponse Map(AccountResult x) =>
        new(x.Id, x.Username, x.Email, x.Role, x.IsActive, x.LastLoginAt, x.LockedUntil);
}
