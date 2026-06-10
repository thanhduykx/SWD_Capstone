using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CPMS.Api.Swagger;

public sealed class SwaggerTestingOperationFilter : IOperationFilter
{
    private static readonly Dictionary<string, OperationDoc> OperationDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET /api/test-support/swagger-guide"] = new(
            "FE testing guide",
            "Start here. Returns the Swagger login flow, local test accounts, role matrix, and recommended endpoint order."),
        ["GET /api/test-support/test-accounts"] = new(
            "Local test accounts",
            "Development-only helper. Lists the expected local accounts and passwords for Swagger testing."),

        ["POST /api/auth/login"] = new(
            "Login and get tokens",
            "Use username/password login first. Copy accessToken from the response, click Authorize, paste the token value, then call protected APIs."),
        ["POST /api/auth/refresh"] = new(
            "Refresh access token",
            "Use the refreshToken returned by login to rotate and get a new accessToken."),
        ["POST /api/auth/google"] = new(
            "Google login",
            "For frontend Google Sign-In. The Google email must already exist in users.email."),
        ["POST /api/auth/bootstrap-admin"] = new(
            "Create first admin in Development",
            "Only works in Development and only when the users table is empty. Normal Swagger testing should use a moderator account."),

        ["GET /api/accounts"] = new(
            "List accounts",
            "Use this after login to verify account ids, roles, active status, and existing test users."),
        ["POST /api/accounts"] = new(
            "Create account",
            "Creates a user and the required role profile. Do not enter username manually; API generates it from fullName and identityCode, for example DuyDTTSE194673."),
        ["PATCH /api/accounts/{userId}/status"] = new(
            "Activate/deactivate account",
            "Use unlock=true to clear lockout after repeated failed login attempts."),

        ["GET /api/semesters"] = new(
            "List semesters",
            "Use this to get semesterId before review or defense scheduling."),
        ["GET /api/semesters/resolve"] = new(
            "Resolve semester by date",
            "Pass date as yyyy-MM-dd. Useful before choosing weekStart for review availability."),
        ["POST /api/semesters"] = new(
            "Create semester",
            "Creates an academic semester. Setting isActive=true automatically deactivates the previous active semester."),

        ["GET /api/review-availability/week"] = new(
            "Get my weekly availability",
            "Lecturer-only. weekStart may be any date in the week; API normalizes it to Monday."),
        ["PUT /api/review-availability/week"] = new(
            "Save my weekly availability",
            "Lecturer-only. Saves a draft availability week. Saving after submit returns the week to draft until the lecturer submits again."),
        ["POST /api/review-availability/week/submit"] = new(
            "Submit availability to moderator",
            "Lecturer-only. Submits the saved weekly availability. Moderator scheduling only uses submitted availability."),

        ["GET /api/review-scheduling/board"] = new(
            "Review scheduling board",
            "Moderator view. Returns lecturers, submitted availability, groups, and sessions needed by FE scheduling screens. Legacy SystemAdministrator tokens are still accepted."),
        ["POST /api/review-scheduling/random-assign"] = new(
            "Random assign groups to submitted lecturer slots",
            "Moderator view. Randomly assigns active unscheduled groups to lecturers using only submitted availability, avoiding supervisor/self-review, repeated Review1 reviewers for Review2, and reviewer slot conflicts. Sends lecturer notification email after a successful assignment and returns delivery counts."),
        ["POST /api/review-sessions"] = new(
            "Create one review session",
            "Assigns one group to one or two reviewers. Reviewers must have submitted availability for the selected date and slot. Sends lecturer notification email after the assignment is created."),
        ["POST /api/review-sessions/bulk-assign"] = new(
            "Bulk assign review sessions",
            "Assigns multiple sessions in one transaction. Reviewers must have submitted availability for each selected date and slot. If one assignment violates a business rule, the whole batch is rejected. Sends lecturer notification email after commit and returns delivery counts."),
        ["PATCH /api/review-sessions/{sessionId}"] = new(
            "Update review session",
            "Changes reviewer/date/slot/room/status and keeps checklist submissions aligned with assigned reviewers. New reviewers must have submitted availability for the target date and slot."),
        ["GET /api/review-sessions/my"] = new(
            "My review sessions",
            "Lecturer view. Use this to get submissionId before opening or saving a checklist."),
        ["POST /api/review-schedules/publish"] = new(
            "Publish weekly review schedule",
            "Publishes all matching sessions for the selected week and sends email if SMTP is configured."),
        ["GET /api/review-submissions/{submissionId}"] = new(
            "Open review checklist",
            "Use submissionId from GET /api/review-sessions/my. Students can only view published submissions for their own group."),
        ["PUT /api/review-submissions/{submissionId}/draft"] = new(
            "Autosave review checklist draft",
            "Reviewer-only. Send only item keys returned by GET /api/review-submissions/{submissionId}."),
        ["POST /api/review-submissions/{submissionId}/submit"] = new(
            "Submit review checklist",
            "Reviewer-only. Marks the checklist submitted."),
        ["GET /api/review-submissions/{submissionId}/export.xlsx"] = new(
            "Export one checklist workbook",
            "Returns an .xlsx file for a single review submission."),
        ["GET /api/review-submissions/export.zip"] = new(
            "Export review workbooks as zip",
            "Moderator export for all sessions in a semester and review round. Legacy SystemAdministrator tokens are still accepted."),

        ["GET /api/defense-management/rounds"] = new(
            "List defense rounds",
            "Moderator view. Use id values from here when assigning defense sessions. Legacy SystemAdministrator tokens are still accepted."),
        ["POST /api/defense-management/rounds"] = new(
            "Create defense round",
            "Creates a Defense1 or Defense2 round inside a semester date range."),
        ["POST /api/defense-management/boards"] = new(
            "Create defense board",
            "Creates a council/board and automatically adds chairman, secretary, and members."),
        ["POST /api/defense-management/boards/{councilId}/members"] = new(
            "Add defense board member",
            "Adds a lecturer to an existing council if missing."),
        ["POST /api/defense-management/sessions"] = new(
            "Assign project to defense board",
            "Schedules a group into a board and defense round. Round, board, and group must belong to the same semester."),
        ["GET /api/defense-management/my-board-sessions"] = new(
            "My defense board sessions",
            "Lecturer view. Use this to get defense session ids for scoring."),
        ["GET /api/defense-sessions/resolve/{code}"] = new(
            "Resolve defense session by code",
            "Lecturer view. code can be session code, council code, or session id."),
        ["POST /api/defense-sessions/{sessionId}/start"] = new(
            "Start defense scoring",
            "Chairman-only. Judges cannot submit scores until this succeeds."),
        ["POST /api/defense-sessions/{sessionId}/scores"] = new(
            "Submit defense score",
            "Assigned council member only. scoreType is BaoVe or Nguoi. scoreValue must be between 0 and 10."),
        ["GET /api/defense-sessions/{sessionId}/evidences"] = new(
            "List defense evidences",
            "Assigned council member only. Returns captured image evidence for a defense session."),
        ["POST /api/defense-sessions/{sessionId}/evidences"] = new(
            "Upload defense evidence image",
            "Assigned council member only. Multipart form-data. File must be an image and max request size is 5 MB."),
        ["POST /api/defense-sessions/{sessionId}/close"] = new(
            "Close defense scoring",
            "Chairman-only. Locks the session and prevents further score edits.")
    };

    private static readonly Dictionary<string, IOpenApiAny> RequestExamples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POST /api/auth/login"] = Obj(
            ("username", Str("test.lecturer")),
            ("examinerCode", Null()),
            ("password", Str("Test@123456"))),
        ["POST /api/auth/refresh"] = Obj(
            ("refreshToken", Str("paste-refresh-token-from-login"))),
        ["POST /api/auth/google"] = Obj(
            ("idToken", Str("paste-google-id-token-from-frontend"))),
        ["POST /api/auth/bootstrap-admin"] = Obj(
            ("username", Str("admin")),
            ("email", Str("admin@cpms.local")),
            ("password", Str("123456"))),

        ["POST /api/accounts"] = Obj(
            ("username", Null()),
            ("identityCode", Str("SE194673")),
            ("email", Str("gv001@cpms.local")),
            ("password", Str("Test@123456")),
            ("role", Str("Lecturer")),
            ("fullName", Str("Duong Thanh Thanh Duy")),
            ("department", Null()),
            ("position", Null()),
            ("permissionScope", Null()),
            ("isPartTime", Bool(false)),
            ("classCode", Null()),
            ("batch", Null()),
            ("major", Null())),
        ["PATCH /api/accounts/{userId}/status"] = Obj(
            ("isActive", Bool(true)),
            ("unlock", Bool(true))),

        ["POST /api/semesters"] = Obj(
            ("code", Str("SU26")),
            ("name", Str("Summer 2026")),
            ("academicYear", Str("2025-2026")),
            ("startDate", Str("2026-05-01")),
            ("endDate", Str("2026-08-31")),
            ("isActive", Bool(true))),

        ["PUT /api/review-availability/week"] = Obj(
            ("slots", Arr(
                Obj(("dayOfWeek", Int(1)), ("slot", Int(1))),
                Obj(("dayOfWeek", Int(3)), ("slot", Int(2))),
                Obj(("dayOfWeek", Int(5)), ("slot", Int(4)))))),
        ["POST /api/review-scheduling/random-assign"] = Obj(
            ("semesterId", Int(1)),
            ("reviewType", Str("Review1")),
            ("weekStart", Str("2026-06-15")),
            ("reviewersPerSession", Int(2)),
            ("roomPrefix", Str("AUTO")),
            ("seed", Null())),

        ["POST /api/review-sessions"] = Obj(
            ("code", Str("R1-SU26-G01")),
            ("groupId", Int(1)),
            ("groupPosition", Int(1)),
            ("type", Str("Review1")),
            ("reviewer1Id", Int(2)),
            ("reviewer2Id", Int(3)),
            ("previousReviewerIds", Arr()),
            ("slot", Int(1)),
            ("room", Str("NVH 601")),
            ("sessionDate", Str("2026-06-15T00:00:00Z"))),
        ["POST /api/review-sessions/bulk-assign"] = Obj(
            ("sessions", Arr(Obj(
                ("code", Str("R1-SU26-G01")),
                ("groupId", Int(1)),
                ("groupPosition", Int(1)),
                ("type", Str("Review1")),
                ("reviewerIds", Arr(Int(2), Int(3))),
                ("previousReviewerIds", Arr()),
                ("slot", Int(1)),
                ("room", Str("NVH 601")),
                ("sessionDate", Str("2026-06-15T00:00:00Z")))))),
        ["PATCH /api/review-sessions/{sessionId}"] = Obj(
            ("code", Str("R1-SU26-G01")),
            ("reviewerIds", Arr(Int(2), Int(3))),
            ("previousReviewerIds", Arr()),
            ("slot", Int(2)),
            ("room", Str("NVH 602")),
            ("sessionDate", Str("2026-06-16T00:00:00Z")),
            ("status", Str("Draft"))),
        ["POST /api/review-schedules/publish"] = Obj(
            ("semesterId", Int(1)),
            ("reviewType", Str("Review1")),
            ("weekStart", Str("2026-06-15")),
            ("subject", Str("Review1 schedule - Week 2026-06-15")),
            ("message", Str("Please check your assigned review sessions in CPMS."))),
        ["PUT /api/review-submissions/{submissionId}/draft"] = Obj(
            ("workProductVersion", Str("v1.0")),
            ("workProductSize", Str("45 pages")),
            ("effortHours", Double(3.5)),
            ("reviewerComment", Str("Draft review comment.")),
            ("suggestion", Str("Improve scope and validation evidence.")),
            ("resultText", Str("Pass with minor revisions")),
            ("items", Arr(Obj(
                ("itemKey", Str("sample-item-key-from-get-response")),
                ("answer", Str("Yes")),
                ("comment", Str("Meets the expected evidence.")))))),

        ["POST /api/defense-management/rounds"] = Obj(
            ("code", Str("DEF1-SU26")),
            ("name", Str("Defense 1 - Summer 2026")),
            ("semesterId", Int(1)),
            ("type", Str("Defense1")),
            ("startDate", Str("2026-07-01")),
            ("endDate", Str("2026-07-15"))),
        ["POST /api/defense-management/boards"] = Obj(
            ("code", Str("BOARD-DEF1-01")),
            ("semesterId", Int(1)),
            ("type", Str("Defense1")),
            ("chairmanId", Int(2)),
            ("secretaryId", Int(3)),
            ("memberLecturerIds", Arr(Int(4), Int(5)))),
        ["POST /api/defense-management/boards/{councilId}/members"] = Obj(
            ("lecturerId", Int(4)),
            ("role", Str("Member"))),
        ["POST /api/defense-management/sessions"] = Obj(
            ("code", Str("DEF1-G01")),
            ("defenseRoundId", Int(1)),
            ("councilId", Int(1)),
            ("groupId", Int(1)),
            ("sessionDate", Str("2026-07-05T00:00:00Z")),
            ("slot", Int(1)),
            ("room", Str("NVH 701"))),
        ["POST /api/defense-sessions/{sessionId}/scores"] = Obj(
            ("studentId", Int(1)),
            ("scoreType", Str("BaoVe")),
            ("scoreValue", Double(8.5))),
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var key = OperationKey(context);
        if (OperationDocs.TryGetValue(key, out var doc))
        {
            operation.Summary = doc.Summary;
            operation.Description = AppendParagraph(operation.Description, doc.Description);
        }
        else
        {
            operation.Summary ??= BuildDefaultSummary(context);
        }

        ApplyAuthorization(operation, context);
        ApplyRequestExample(operation, key);
        ApplyResponseHints(operation);
    }

    private static void ApplyAuthorization(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!TryGetAuthorization(context, out var roles))
        {
            operation.Description = AppendParagraph(
                operation.Description,
                "Auth: anonymous endpoint. No bearer token required.");
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            }] = []
        });

        var roleText = HumanizeRoles(roles);
        operation.Description = AppendParagraph(
            operation.Description,
            $"Auth: Bearer token required. Allowed roles: {roleText}. In Swagger, login first, copy accessToken, click Authorize, and paste the token value.");
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing, invalid, or expired bearer token." });
        if (roles.Count > 0)
        {
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Token is valid, but the account role is not allowed for this endpoint." });
        }
    }

    private static bool TryGetAuthorization(OperationFilterContext context, out IReadOnlyCollection<string> roles)
    {
        roles = [];
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
        {
            return false;
        }

        var controllerAllowsAnonymous = actionDescriptor.ControllerTypeInfo
            .GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
            .Any();
        var methodAllowsAnonymous = actionDescriptor.MethodInfo
            .GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
            .Any();
        if (controllerAllowsAnonymous || methodAllowsAnonymous)
        {
            return false;
        }

        var authorizeAttributes = actionDescriptor.ControllerTypeInfo
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(actionDescriptor.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .ToArray();

        if (authorizeAttributes.Length == 0)
        {
            return false;
        }

        roles = authorizeAttributes
            .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static string HumanizeRoles(IReadOnlyCollection<string> roles)
    {
        if (roles.Count == 0)
        {
            return "any authenticated user";
        }

        if (roles.Contains("TrainingDepartment") && roles.Contains("SystemAdministrator"))
        {
            return "Moderator (TrainingDepartment; legacy SystemAdministrator accepted)";
        }

        return string.Join(", ", roles);
    }

    private static void ApplyRequestExample(OpenApiOperation operation, string key)
    {
        if (operation.RequestBody?.Content is null ||
            !RequestExamples.TryGetValue(key, out var example))
        {
            return;
        }

        foreach (var mediaType in operation.RequestBody.Content.Values)
        {
            mediaType.Example = example;
        }
    }

    private static void ApplyResponseHints(OpenApiOperation operation)
    {
        operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Invalid input or business rule violation. Check the response error/title field." });
        operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Unexpected server error. Check backend logs." });
    }

    private static string OperationKey(OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod?.ToUpperInvariant() ?? "GET";
        var relativePath = context.ApiDescription.RelativePath?.Split('?')[0].TrimEnd('/') ?? string.Empty;
        return $"{method} /{relativePath}";
    }

    private static string BuildDefaultSummary(OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor actionDescriptor)
        {
            return $"{actionDescriptor.ControllerName}: {actionDescriptor.ActionName}";
        }

        return context.ApiDescription.HttpMethod ?? "API operation";
    }

    private static string AppendParagraph(string? current, string paragraph)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return paragraph;
        }

        return $"{current.Trim()}\n\n{paragraph}";
    }

    private static OpenApiObject Obj(params (string Key, IOpenApiAny Value)[] properties)
    {
        var value = new OpenApiObject();
        foreach (var property in properties)
        {
            value[property.Key] = property.Value;
        }

        return value;
    }

    private static OpenApiArray Arr(params IOpenApiAny[] items)
    {
        var value = new OpenApiArray();
        foreach (var item in items)
        {
            value.Add(item);
        }

        return value;
    }

    private static OpenApiString Str(string value) => new(value);
    private static OpenApiInteger Int(int value) => new(value);
    private static OpenApiBoolean Bool(bool value) => new(value);
    private static OpenApiDouble Double(double value) => new(value);
    private static OpenApiNull Null() => new();

    private sealed record OperationDoc(string Summary, string Description);
}
