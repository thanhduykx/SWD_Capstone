using System.Globalization;
using System.Security.Claims;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class AccountsController(CpmsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AccountResponse>> GetAll(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Username)
            .Select(x => new AccountResponse(
                x.Id,
                x.Username,
                x.Email,
                x.Role,
                x.IsActive,
                x.LastLoginAt,
                x.LockedUntil))
            .ToListAsync(cancellationToken);

        return users;
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Role == UserRole.SystemAdministrator && !User.IsInRole(nameof(UserRole.SystemAdministrator)))
        {
            return Forbid();
        }

        var username = request.Username.Trim();
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username, email and password are required." });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { error = "Password must contain at least 6 characters." });
        }

        if (await dbContext.Users.AnyAsync(x => x.Username == username || x.Email == email, cancellationToken))
        {
            return Conflict(new { error = "Username or email already exists." });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = request.Role,
            IsActive = true,
            CreatedById = CurrentUserId()
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        AddRoleProfile(user.Id, request);
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId(),
            Action = "CREATE_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = user.Username,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { user.Id }, new AccountResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.LastLoginAt,
            user.LockedUntil));
    }

    [HttpPatch("{userId:int}/status")]
    public async Task<ActionResult<AccountResponse>> UpdateStatus(
        int userId,
        UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        user.LockedUntil = request.Unlock ? null : user.LockedUntil;
        user.UpdatedAt = DateTime.UtcNow;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId(),
            Action = request.IsActive ? "ACTIVATE_ACCOUNT" : "DEACTIVATE_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            OldValue = (!request.IsActive).ToString(),
            NewValue = request.IsActive.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AccountResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.LastLoginAt,
            user.LockedUntil));
    }

    private void AddRoleProfile(int userId, CreateAccountRequest request)
    {
        switch (request.Role)
        {
            case UserRole.Lecturer:
                dbContext.Lecturers.Add(new Lecturer
                {
                    UserId = userId,
                    Code = request.Code ?? request.Username,
                    FullName = Required(request.FullName, "Full name is required for lecturers."),
                    Department = Required(request.Department, "Department is required for lecturers."),
                    IsPartTime = request.IsPartTime,
                    MaxGroups = request.MaxGroups ?? 0
                });
                break;
            case UserRole.EvaluationPanel:
                dbContext.EvaluationPanels.Add(new EvaluationPanel
                {
                    UserId = userId,
                    FullName = Required(request.FullName, "Full name is required for panel accounts."),
                    Department = Required(request.Department, "Department is required for panel accounts.")
                });
                break;
            case UserRole.TrainingDepartment:
                dbContext.TrainingDepartments.Add(new TrainingDepartment
                {
                    UserId = userId,
                    DepartmentName = request.Department ?? "Training Department",
                    StaffCode = request.Code ?? request.Username,
                    Position = request.Position ?? "Staff"
                });
                break;
            case UserRole.SystemAdministrator:
                dbContext.SystemAdministrators.Add(new SystemAdministrator
                {
                    UserId = userId,
                    AdminLevel = request.Position ?? "Admin",
                    PermissionScope = request.PermissionScope ?? "System"
                });
                break;
            case UserRole.Student:
                dbContext.Students.Add(new Student
                {
                    UserId = userId,
                    Code = request.Code ?? request.Username,
                    FullName = Required(request.FullName, "Full name is required for students."),
                    ClassCode = Required(request.ClassCode, "Class code is required for students."),
                    Batch = request.Batch,
                    Major = request.Major ?? "SE"
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), "Unsupported role.");
        }
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}

public sealed record AccountResponse(
    int Id,
    string Username,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LockedUntil);

public sealed record CreateAccountRequest(
    string Username,
    string Email,
    string Password,
    UserRole Role,
    string? Code,
    string? FullName,
    string? Department,
    string? Position,
    string? PermissionScope,
    bool IsPartTime,
    int? MaxGroups,
    string? ClassCode,
    string? Batch,
    string? Major);

public sealed record UpdateAccountStatusRequest(bool IsActive, bool Unlock);
