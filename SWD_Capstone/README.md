# CPMS - Capstone Project Management System

Foundation implementation based on `CPMS_ProjectSpec_v1.1.docx` for FPT University SEP490 management and syllabus-based document evaluation.

## Current Implementation

- Three-layer ASP.NET Core solution under `src/`: `CPMS.API`, `CPMS.BLL`, `CPMS.DAL`.
- PostgreSQL 16 with EF Core/Npgsql covering the production reference entities: authentication, academic structure, documents/evaluation, review/defense, audit and notifications.
- JWT access token and rotating refresh token endpoints, BCrypt password verification and login lockout handling.
- API foundation for semesters, review-session assignment and defense score submission/locking.
- SignalR defense scoring room: council members join a live session, the chairman starts/closes scoring, and score submissions are broadcast to the council.
- Swagger is the primary API testing surface; the repository is backend-only.
- Unit tests for supervisor conflicts, repeated reviewers, council membership, chairman-only session control, council capacity and score locking rules.
- Repository quality gates based on ISO/IEC 9126, ISO/IEC 25010, ISO/IEC/IEEE 12207 and ISO/IEC/IEEE 29119 are documented in `docs/QUALITY_STANDARDS.md`.
- Business architecture and implementation boundaries are documented in `docs/BUSINESS_ARCHITECTURE.md`.

## Non-Negotiable Rules Implemented

- A supervisor cannot review or sit on a council for their own group.
- Review 2 cannot reuse reviewers supplied from Review 1.
- Councils cannot assess more than eight groups in a session.
- Only assigned council members can join and score a defense session.
- Only the council chairman can start or close defense scoring.
- Judges cannot submit scores until the chairman starts the session.
- Scores must be between `0` and `10`.
- Once a defense session is closed, score edits are rejected.
- Every score submission writes both an immutable audit log entry and a detailed score history row containing scorer, student, old value, new value, IP and user agent.
- `audit_logs` are append-only at the application persistence boundary.
- Documents use soft delete through an EF Core query filter.

## Stack

- Backend: ASP.NET Core 9 MVC/Web API hybrid and EF Core 9.
- Architecture: three-layer backend with API, BLL and DAL projects.
- Real-time: ASP.NET Core SignalR over `/hubs/defense`.
- Database: PostgreSQL 16 through `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Cache / messaging: Redis and RabbitMQ adapters are available through BLL abstractions.
- Logging / jobs: Serilog and optional Hangfire.

## Run Database

```powershell
docker compose up -d postgres
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\src\CPMS.DAL\CPMS.DAL.csproj --startup-project .\src\CPMS.API\CPMS.API.csproj
```

The Docker development connection string targets PostgreSQL at `localhost:5433`, database `cpms`, username `postgres`, password `123456`. Port `5433` avoids colliding with an existing local PostgreSQL installation on the default port.

No application account is seeded automatically. Accounts must be created from official Training Department data or a controlled administration/import flow.

For local development only, create the first administrator explicitly after the backend starts:

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://localhost:5122/api/auth/bootstrap-admin `
  -ContentType 'application/json' `
  -Body '{"username":"","email":"admin@cpms.local","password":""}'
```

This endpoint is available only in `Development` and only when the `users` table is empty.

## Run Backend

```powershell
dotnet restore .\CPMS.sln
dotnet build .\CPMS.sln
dotnet test .\CPMS.sln
dotnet run --project .\src\CPMS.API\CPMS.API.csproj
```

`src/CPMS.API/appsettings.json` contains non-secret defaults only. Never put a real
password, API key, JWT signing key, service-role key, or connection string in a
tracked file, even temporarily.

For local development, store secrets with .NET User Secrets:

```powershell
$project = '.\src\CPMS.API\CPMS.API.csproj'
dotnet user-secrets --project $project set 'ConnectionStrings:CpmsDatabase' '<connection-string>'
dotnet user-secrets --project $project set 'Jwt:Key' '<at-least-32-random-characters>'
dotnet user-secrets --project $project set 'Smtp:FromEmail' '<sender-address>'
dotnet user-secrets --project $project set 'Smtp:UserName' '<smtp-user>'
dotnet user-secrets --project $project set 'Smtp:Password' '<new-app-password>'
dotnet user-secrets --project $project set 'Gemini:ApiKey' '<api-key>'
```

User Secrets are for development only. Production secrets must be injected by the
deployment platform as environment variables. Environment variables have the
highest configuration precedence.

## Gmail / Google Login

Google login is account-bound: the Google email must already exist in `users.email`.

Configure the Google OAuth Client ID in `Authentication:Google:ClientId`. Configure
SMTP credentials through User Secrets locally or `Smtp__FromEmail`,
`Smtp__UserName`, and `Smtp__Password` environment variables in production. Gmail
must use a dedicated app password with 2-Step Verification; never use the account
password and never reuse the app password for another application.

## Gemini Project Suggestions

Lecturers can call `POST /api/project-suggestions/summary` to generate Vietnamese
reference text containing a project-content summary, strengths, and areas for
improvement. Keep the non-secret model and timeout in `appsettings.json`. Configure
`Gemini:ApiKey` through User Secrets locally or `Gemini__ApiKey` in production.

## Deploy to Render

Render automatically detects the repository-root `Dockerfile`. Add the GitHub
repository as a Docker web service. Configure `ConnectionStrings__CpmsDatabase`
as a Render environment variable with a PostgreSQL connection string that is
reachable from the service. For Supabase on Render, use the IPv4-compatible
Supavisor session-pooler host and port 5432 from the Supabase **Connect** dialog;
the direct `db.<project-ref>.supabase.co` endpoint requires IPv6 unless the project
has the IPv4 add-on. Deployment environment variables override values in
`appsettings.json`. The application runs EF Core migrations against the configured
database during startup.

At minimum, configure `ConnectionStrings__CpmsDatabase`, `Jwt__Key`,
`Smtp__FromEmail`, `Smtp__UserName`, and `Smtp__Password` as Render secrets. Rotate
each value immediately if it ever appears in Git, a build log, a screenshot, or a
chat transcript. Rotation is still required after Git history is rewritten.

After deployment, verify that `/health/ready` returns HTTP 200 before testing
database-backed endpoints such as `/api/auth/login`.

## Quality Gates

```powershell
git config core.hooksPath SWD_Capstone/.githooks
powershell -ExecutionPolicy Bypass -File .\scripts\scan-secrets.ps1 -Tracked
dotnet build .\CPMS.sln
dotnet test .\CPMS.sln
```

## Planned Modules From The Specification

- Excel import with strict validation for topics, groups, students and supervisors.
- Council management and participant verification UI.
- Notification delivery beyond the live defense room.
- TEF export only after FPT provides or validates the `.tef` file specification. The source document explicitly identifies this as an early unresolved risk.
