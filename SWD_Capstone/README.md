# CPMS - Capstone Project Management System

Foundation implementation based on `CPMS_ProjectSpec_v1.1.docx` for FPT University SEP490 management and syllabus-based document evaluation.

## Current Implementation

- Modular monolith backend solution: `CPMS.Api`, `CPMS.Core`, `CPMS.Infrastructure`.
- PostgreSQL 16 with EF Core/Npgsql covering the production reference entities: authentication, academic structure, documents/evaluation, review/defense, audit and notifications.
- JWT access token and rotating refresh token endpoints, BCrypt password verification and login lockout handling.
- API foundation for semesters, review-session assignment and defense score submission/locking.
- SignalR defense scoring room: council members join a live session, the chairman starts/closes scoring, and score submissions are broadcast to the council.
- React 18/Vite frontend shell for Dashboard, Semesters, Reviews, Defense Scoring and Documents/CLO evaluation.
- Unit tests for supervisor conflicts, repeated reviewers, council membership, chairman-only session control, council capacity and score locking rules.
- Repository quality gates based on ISO/IEC 9126, ISO/IEC 25010, ISO/IEC/IEEE 12207 and ISO/IEC/IEEE 29119 are documented in `docs/QUALITY_STANDARDS.md`.

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

- Frontend: React 18, TypeScript, Vite and React Router.
- Backend: ASP.NET Core 8 Web API and EF Core 8.
- Real-time: ASP.NET Core SignalR over `/hubs/defense`.
- Database: PostgreSQL 16 through `Npgsql.EntityFrameworkCore.PostgreSQL`.

## Run Database

```powershell
docker compose up -d postgres
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\backend\CPMS.Infrastructure\CPMS.Infrastructure.csproj --startup-project .\backend\CPMS.Api\CPMS.Api.csproj
```

The Docker development connection string targets PostgreSQL at `localhost:5433`, database `cpms`, username `postgres`, password `postgres`. Port `5433` avoids colliding with an existing local PostgreSQL installation on the default port. Supply secrets securely outside local development.

## Run Backend

```powershell
dotnet restore .\CPMS.sln
dotnet build .\CPMS.sln
dotnet test .\CPMS.sln
dotnet run --project .\backend\CPMS.Api\CPMS.Api.csproj
```

Before running outside local development, replace `Jwt:Key` and the PostgreSQL password using environment variables or a secret store.

## Run Frontend

```powershell
cd .\frontend
npm install
npm run dev
```

## Quality Gates

```powershell
dotnet build .\CPMS.sln
dotnet test .\CPMS.sln
cd .\frontend
npm run lint
npm run build
```

## Planned Modules From The Specification

- Excel import with strict validation for topics, groups, students and supervisors.
- Council management and participant verification UI.
- Notification delivery beyond the live defense room.
- TEF export only after FPT provides or validates the `.tef` file specification. The source document explicitly identifies this as an early unresolved risk.
