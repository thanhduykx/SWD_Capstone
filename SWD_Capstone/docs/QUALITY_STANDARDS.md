# Quality Standards

This project uses ISO/IEC 9126 as the quality model baseline and aligns practical engineering controls with ISO/IEC/IEEE 12207, ISO/IEC/IEEE 29119, ISO/IEC 25010 and IEEE 830 style requirements traceability.

## Quality Model Mapping

| Standard concern | CPMS control | Verification |
| --- | --- | --- |
| Functionality / functional suitability | Business rules live in `ServiceLayer` and are covered by unit tests. Chairman-only session control, council membership, score locking and conflict checks must stay server-side. | `dotnet test .\CPMS.sln` |
| Reliability | Database constraints, immutable score locking, append-only audit logs and EF migrations protect critical data. APIs should fail with explicit business errors, not silent success. | Backend build, tests, migration review |
| Usability | React screens must expose the actual business state: waiting, live scoring, locked, validation errors and audit feed. | Frontend build and manual UI review |
| Efficiency / performance | PostgreSQL indexes exist for login, refresh token lookup, unique assignments and score writes. Realtime scoring uses SignalR groups per session. | Query review before adding high-volume APIs |
| Maintainability | Layered architecture: MVC/API orchestration in `PresentationLayer`, business rules in `ServiceLayer`, entities/context/migrations in `DataAccessLayer`. No business rules hidden only in controllers or UI. | Code review, analyzer warnings |
| Portability | Docker Compose for PostgreSQL, Vite frontend and ASP.NET Core backend. Environment-specific settings stay in config/secrets. | Fresh setup runbook |
| Security | JWT + refresh rotation, BCrypt, auth rate limiting, RBAC, strict CORS, server-side authorization for scoring. | Auth endpoint tests and manual security review |

## Mandatory Quality Gates

Run these before submitting work:

```powershell
dotnet build .\CPMS.sln
dotnet test .\CPMS.sln
cd .\frontend
npm run lint
npm run build
```

For the parent Visual Studio solution:

```powershell
dotnet build ..\SWD_Capstone.sln --no-restore
```

## Engineering Rules

- Keep domain decisions in `ServiceLayer`; controllers should coordinate requests, authorization and persistence.
- Never trust frontend state for scoring permissions. Backend must verify the lecturer, council membership, chairman role and session state.
- Every score write must create audit evidence: `audit_logs` plus `score_submission_histories`.
- Locked score data is immutable at API, EF and database levels.
- New entities need explicit EF configuration: table name, indexes, delete behavior, enum conversion and precision for decimals.
- New business rules need focused unit tests.
- Public API errors must be understandable and safe: no stack traces, no database details.
- Secrets must not be committed. Use environment variables or local secret storage for real JWT keys and database passwords.

## Traceability Expectations

Each major feature should identify:

- Requirement or use case from the CPMS spec.
- Business rule enforced.
- API endpoint or frontend screen implementing it.
- Test or manual verification evidence.

Example: `UC08 - Chairman starts/closes session` maps to `DefenseSessionsController.Start/Close`, `AssignmentRules.EnsureChairman`, SignalR `defenseSessionStarted/Closed`, and unit tests for chairman rejection.
