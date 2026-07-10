# CPMS - Capstone Project Management System

Foundation implementation based on `CPMS_ProjectSpec_v1.1.docx` for FPT University SEP490 management and syllabus-based document evaluation.

## Current Implementation

- Clean Architecture ASP.NET Core solution under `src/`: `CPMS.API`, `CPMS.Application`, `CPMS.Domain`, `CPMS.Infrastructure`, `CPMS.Shared`.
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
- Architecture: Clean Architecture / N-Layer with Domain, Application, Infrastructure and API boundaries.
- Real-time: ASP.NET Core SignalR over `/hubs/defense`.
- Database: PostgreSQL 16 through `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Cache / messaging: Redis and RabbitMQ adapters are available through Application abstractions.
- Logging / jobs: Serilog and optional Hangfire.

## Run Database

```powershell
docker compose up -d postgres
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\src\CPMS.Infrastructure\CPMS.Infrastructure.csproj --startup-project .\src\CPMS.API\CPMS.API.csproj
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

Before running outside local development, set `ConnectionStrings__CpmsDatabase` and `Jwt__Key` using environment variables or a secret store.

## Gmail / Google Login

Google login is account-bound: the Google email must already exist in `users.email`.

Configure the Google OAuth Client ID in `Authentication:Google:ClientId`.

Configure SMTP and Google OAuth through environment variables. In production,
set at least `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSsl`, `Smtp__FromEmail`,
`Smtp__UserName`, `Smtp__Password`, and `Authentication__Google__ClientId`.

## Deploy to Render

The Render Blueprint is in the repository-root `render.yaml`. It builds the
existing Docker image, uses the Render-provided `PORT`, and checks `/health`.
The database remains the existing Supabase PostgreSQL instance, so no database
is recreated or data migrated during this provider migration.

Before applying the Blueprint, provide the required values marked `sync: false`:

- `ConnectionStrings__CpmsDatabase` (the existing Supabase connection string)
- `Cors__AllowedOrigins__0` (the deployed frontend origin)
- SMTP settings and Google OAuth client ID, if those features are used

Render generates `Jwt__Key`; keep it stable after the first deployment so
existing JWTs remain valid until their normal expiry.

## Quality Gates

```powershell
dotnet build .\CPMS.sln
dotnet test .\CPMS.sln
```

## Planned Modules From The Specification

- Excel import with strict validation for topics, groups, students and supervisors.
- Council management and participant verification UI.
- Notification delivery beyond the live defense room.
- TEF export only after FPT provides or validates the `.tef` file specification. The source document explicitly identifies this as an early unresolved risk.
