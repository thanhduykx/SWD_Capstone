using System.Globalization;
using System.Net;
using System.Security.Claims;
using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class AccountsController(
    CpmsDbContext dbContext,
    IReviewEmailSender emailSender) : ControllerBase
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
    public async Task<ActionResult<AccountCreatedResponse>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fullName = Required(request.FullName, "Full name is required for generated account usernames.");
        var identityCode = Required(request.IdentityCode ?? request.Username, "Identity code is required.");
        var username = AccountUsernameGenerator.Generate(fullName, identityCode);
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
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

        AddRoleProfile(user.Id, request with
        {
            Username = username,
            IdentityCode = identityCode,
            Email = email,
            FullName = fullName
        });
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

        var emailResult = await SendAccountCreatedEmailAsync(user, fullName, request.Password, cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { user.Id }, new AccountCreatedResponse(
            user.Id,
            user.Username,
            user.Email,
            request.Password,
            user.Role,
            user.IsActive,
            user.LastLoginAt,
            user.LockedUntil,
            identityCode,
            emailResult.Status,
            emailResult.Error));
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
                    Code = Required(request.IdentityCode, "Identity code is required for lecturers."),
                    FullName = Required(request.FullName, "Full name is required for lecturers."),
                    Department = OptionalOrDefault(request.Department, string.Empty),
                    IsPartTime = request.IsPartTime
                });
                break;
            case UserRole.TrainingDepartment:
                dbContext.TrainingDepartments.Add(new TrainingDepartment
                {
                    UserId = userId,
                    DepartmentName = OptionalOrDefault(request.Department, "Training Department"),
                    StaffCode = Required(request.IdentityCode, "Identity code is required for moderator accounts."),
                    Position = OptionalOrDefault(request.Position, "Moderator")
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
                    Code = Required(request.IdentityCode, "Identity code is required for students."),
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

    private static string OptionalOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private async Task<AccountEmailResult> SendAccountCreatedEmailAsync(
        User user,
        string fullName,
        string initialPassword,
        CancellationToken cancellationToken)
    {
        var subject = "CPMS account login information";
        var textBody = string.Join(Environment.NewLine, [
            $"Hello {fullName},",
            string.Empty,
            "Your CPMS account has been created successfully.",
            "Use the credentials below to sign in:",
            $"Username / Account: {user.Username}",
            $"Initial password: {initialPassword}",
            string.Empty,
            "Important notes:",
            "- Keep this account information private.",
            "- Do not share your password with anyone.",
            "- Sign in as soon as possible and change your password according to your department process.",
            "- If you did not request or expect this account, contact the moderator immediately."
        ]);
        var logoPath = AccountEmailLogoPath();
        var logoContentId = "fpt-logo";
        var htmlBody = BuildAccountCreatedEmailHtml(
            fullName,
            user.Username,
            initialPassword,
            logoPath is null ? null : $"cid:{logoContentId}");
        var inlineImages = logoPath is null
            ? Array.Empty<EmailInlineImage>()
            : [new EmailInlineImage(logoContentId, logoPath, "image/png")];

        try
        {
            await emailSender.SendHtmlAsync(user.Email, subject, htmlBody, textBody, inlineImages, cancellationToken);
            return new AccountEmailResult("Sent", null);
        }
        catch (BusinessRuleException exception)
        {
            return new AccountEmailResult("Skipped", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return new AccountEmailResult("Failed", exception.Message);
        }
        catch (System.Net.Mail.SmtpException exception)
        {
            return new AccountEmailResult("Failed", exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new AccountEmailResult("Failed", exception.Message);
        }
    }

    private static string? AccountEmailLogoPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "EmailAssets", "fptlogo.png");
        return System.IO.File.Exists(path) ? path : null;
    }

    private static string BuildAccountCreatedEmailHtml(
        string fullName,
        string username,
        string initialPassword,
        string? logoSource)
    {
        var safeFullName = WebUtility.HtmlEncode(fullName);
        var safeUsername = WebUtility.HtmlEncode(username);
        var safePassword = WebUtility.HtmlEncode(initialPassword);
        var logoHtml = string.IsNullOrWhiteSpace(logoSource)
            ? "<div style=\"font-size:20px;font-weight:800;color:#0f5ea8;letter-spacing:.2px;\">FPT University</div>"
            : $"<img src=\"{logoSource}\" width=\"180\" alt=\"FPT University\" style=\"display:block;width:180px;max-width:100%;height:auto;border:0;outline:none;text-decoration:none;\" />";

        return $$"""
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>CPMS account information</title>
  </head>
  <body style="margin:0;padding:0;background:#f3f6fb;font-family:Arial,Helvetica,sans-serif;color:#172033;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6fb;margin:0;padding:28px 12px;">
      <tr>
        <td align="center">
          <table role="presentation" width="640" cellspacing="0" cellpadding="0" style="width:640px;max-width:100%;background:#ffffff;border:1px solid #e1e7f0;border-radius:12px;overflow:hidden;">
            <tr>
              <td style="padding:24px 28px 10px 28px;text-align:left;">
                {{logoHtml}}
              </td>
            </tr>
            <tr>
              <td style="padding:10px 28px 28px 28px;">
                <h1 style="margin:0 0 10px 0;font-size:24px;line-height:32px;color:#10243e;">CPMS account information</h1>
                <p style="margin:0 0 18px 0;font-size:15px;line-height:23px;color:#44546a;">Hello <strong>{{safeFullName}}</strong>, your CPMS account has been created successfully. Use the credentials below to sign in.</p>

                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:separate;border-spacing:0;margin:18px 0;border:1px solid #d8e2ef;border-radius:10px;overflow:hidden;">
                  <tr>
                    <td style="padding:13px 16px;background:#f7fafc;width:34%;font-size:13px;color:#667085;border-bottom:1px solid #d8e2ef;">Username / Account</td>
                    <td style="padding:13px 16px;background:#ffffff;font-size:16px;font-weight:700;color:#0f5ea8;border-bottom:1px solid #d8e2ef;">{{safeUsername}}</td>
                  </tr>
                  <tr>
                    <td style="padding:13px 16px;background:#f7fafc;font-size:13px;color:#667085;">Initial password</td>
                    <td style="padding:13px 16px;background:#ffffff;font-size:16px;font-weight:700;color:#cc4b00;">{{safePassword}}</td>
                  </tr>
                </table>

                <div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:10px;color:#7c2d12;font-size:14px;line-height:22px;">
                  Keep this account information private. Do not forward this email or share your password with anyone.
                </div>

                <p style="margin:0 0 10px 0;font-size:14px;line-height:22px;color:#44546a;">Next steps:</p>
                <ol style="margin:0 0 18px 20px;padding:0;color:#44546a;font-size:14px;line-height:22px;">
                  <li>Sign in to CPMS using the username and initial password above.</li>
                  <li>Change your password according to your department process after the first sign-in.</li>
                  <li>If this account information looks incorrect, contact the moderator before using it.</li>
                </ol>

                <p style="margin:20px 0 0 0;font-size:12px;line-height:18px;color:#98a2b3;">This is an automated CPMS email. Please keep it for your first sign-in only.</p>
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>
""";
    }
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
    string? Username,
    string? IdentityCode,
    string Email,
    string Password,
    UserRole Role,
    string? FullName,
    string? Department,
    string? Position,
    string? PermissionScope,
    bool IsPartTime,
    string? ClassCode,
    string? Batch,
    string? Major);

public sealed record UpdateAccountStatusRequest(bool IsActive, bool Unlock);

public sealed record AccountCreatedResponse(
    int Id,
    string Username,
    string Email,
    string InitialPassword,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LockedUntil,
    string IdentityCode,
    string EmailDeliveryStatus,
    string? EmailDeliveryError);

internal sealed record AccountEmailResult(string Status, string? Error);
