# CPMS Business Architecture

This document defines the target business architecture for the Capstone Project Management System.
It is based on `CPMS_ProjectSpec_v1.1.docx`, the provided review/defense Excel files and the TEF archive sample.

## System Goal

CPMS is not only a scoring screen. It is an academic workflow system for one full SEP490 semester:

1. Training Department creates a semester.
2. Training Department imports official lecturers, students, groups, topics and supervisors from Excel.
3. Training Department schedules Review 1, Review 2 and Review 3.
4. The system checks supervisor conflict, duplicated reviewers and reviewer workload.
5. Training Department creates Defense 1 and Defense 2 councils.
6. Council members log in using official lecturer codes and Training Department-issued passwords.
7. The chairman starts the defense session through SignalR.
8. Assigned judges score `ChamBaoVe` and `ChamNguoi` independently for each student.
9. The chairman closes the session; scores become locked.
10. The system stores immutable score history, audit logs and export history.
11. The system generates TEF files and workload/dashboard reports from stored data.

## Bounded Contexts

### Identity And Access

Owns users, roles, lecturer accounts, students, Training Department accounts, refresh tokens and login lockout.

Rules:

- Passwords are BCrypt hashed.
- Accounts are not seeded automatically.
- Official accounts come from controlled admin/import flows.
- A lecturer can have multiple responsibilities in a semester: supervisor, reviewer, council chairman, secretary or member.
- API authorization must check both role and data ownership.

### Academic Setup

Owns semesters, topics, capstone groups, students, supervisors and syllabus/CLO definitions.

Rules:

- Only one semester can be active at a time.
- A capstone group belongs to one semester and one topic.
- A group has one primary supervisor in the current schema.
- Student/group/topic import must reject malformed Excel rows, duplicated codes and formula errors.

### Review Management

Owns Review 1, Review 2, Review 3 sessions, reviewer assignment, room/slot/day data and group review result.

Rules:

- Slot must be 1-5.
- Review session code follows the workbook convention: day code + slot code + group position.
- Supervisor cannot review their own group.
- Review 2 cannot reuse Review 1 reviewers.
- Review 3 should prefer matching at least one Review 2 reviewer when applicable.
- Review results are `Pass`, `Fail`, `Defense2` or `Drop`.

### Defense Board And Round Management

Owns defense rounds, defense boards, board members, board-project assignment and defense sessions.

Rules:

- A semester can have multiple defense rounds, for example Defense 1, Defense 2 or make-up rounds.
- A defense round can contain many defense days and hundreds or thousands of defense sessions.
- A defense board is the academic council: chairman, secretary and members.
- A lecturer can view and join many boards across one semester.
- Training Department acts as the system moderator: it creates rounds, creates boards and assigns boards to projects.
- Standard council has 5 members: chairman, secretary and 3 members.
- Reduced council has 3 members: chairman, secretary and 1 member.
- One defense session is exactly one board assessing one project/group in one room/slot/date.
- One board can assess many projects through many defense sessions.
- One project can have multiple defenses across different rounds, for example Defense 1 then Defense 2.
- No supervisor of an assigned group can sit on that council.
- A defense session is controlled only by the chairman.

### Realtime Defense Scoring

Owns start/close scoring, score submission, score history, evidence photos and SignalR events.

Rules:

- A judge can join only if assigned to the council.
- Scores cannot be submitted before the chairman starts.
- Scores cannot be changed after the chairman closes.
- Scores must be 0-10.
- A judge can score only students belonging to the single project/group assigned to that defense session.
- Every score write creates score history and audit log entries.

### Document And CLO Evaluation

Owns uploaded documents, versions, CLO matching, evaluation reports, evaluation details and inline comments.

Rules:

- Documents use soft delete.
- Evaluation report belongs to one document.
- Evaluation details are generated per CLO.
- Manual panel review must be traceable separately from automated evaluation.

### TEF Export And Reporting

Owns TEF export jobs, generated files, archive packaging and report downloads.

Rules:

- TEF generation happens only after real locked scores exist.
- Export separates `ChamBaoVe` and `ChamNguoi`.
- Export output is traceable by group, student, scorer, score type and generation time.
- Exact TEF binary/text format must be confirmed from FPT IT or validated against sample files before production use.

## Layer Responsibilities

### PresentationLayer

Contains MVC/API controllers, SignalR hubs, request/response mapping, authentication middleware and static assets.

PresentationLayer must not own business decisions such as conflict rules, score lock rules or import validation. It coordinates transport concerns only.

### ServiceLayer

Contains application services and business workflows.

Examples:

- `DefenseScoringService`: chairman start/close, judge score submission, audit/history.
- `ReviewSchedulingService`: create review sessions, validate reviewer conflicts and persist group slots.
- `DefenseManagementController`: current API surface for creating defense rounds, boards, members and board-project sessions. This should move into `DefenseManagementService` when the workflow grows.
- `CouncilManagementService`: target service for creating councils, assigning members/groups and validating capacity/conflicts.
- `ExcelImportService`: parse official Excel files into validated import commands.
- `TefExportService`: generate TEF files from locked scores.

### DataAccessLayer

Contains EF Core entities, enums, migrations and database constraints.

DataAccessLayer must enforce low-level integrity such as unique indexes, FK constraints, append-only audit protection and locked score immutability.

### Frontend

React SPA consumes APIs and SignalR.

Frontend must not fake business data. It shows empty states until official data is imported and persisted.

## Current Architecture Gaps

- Most controllers still access `CpmsDbContext` directly; business workflows must move into ServiceLayer.
- Import Excel is not implemented yet.
- TEF writer/export history is not implemented yet.
- Council creation and project-scoped defense assignment APIs exist, but need UI screens and broader integration tests.
- Review result lifecycle is not fully connected to group final result.
- Dashboard/workload must be derived from database queries, not static frontend values.
- Audit immutability is enforced by EF but still needs database trigger protection.

## Implementation Order

1. Move business workflows from controllers into ServiceLayer.
2. Implement official account/import flow for lecturers, students, groups and topics.
3. Complete review scheduling and result lifecycle.
4. Complete council management before expanding realtime scoring UI.
5. Implement TEF export history and format validation.
6. Build dashboard/workload from database projections.
7. Add integration tests for critical workflows.
