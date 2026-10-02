# Instructions for AI coding agents

This file is read automatically by Cursor and by Claude Code and Codex-style agents. It is the persistent part of the prompt: rules that apply to every request, so individual prompts can stay short and focused on intent.

## Hard constraints (from the exercise)

- **Do not use Entity Framework, Dapper or MediatR.** Data access is ADO.NET with `Microsoft.Data.SqlClient`.
- Database: **SQL Server**. Backend: **ASP.NET Core (.NET 9) controllers**. Frontend: **Angular** (standalone, signals).
- The business logic layer must not depend on the data layer or the API.

## Architecture (Clean Architecture)

- `Domain`: entities and invariants only. No package references and no I/O.
- `Application`: use cases, DTOs and **ports** (interfaces such as `ITaskRepository`). References `Domain` only.
- `Infrastructure`: implements ports (ADO.NET, hashing, JWT). References `Application`.
- `Api`: controllers stay thin (no business rules), plus auth, error mapping and Swagger.
- `tests/TaskManager.UnitTests/Architecture/DependencyRuleTests.cs` enforces these rules. Never weaken it to make code compile.

## Workflow: TDD

1. Write or extend the failing test first and commit it as `test(<layer>): ... (red)`.
2. Write the minimum implementation to pass and commit it as `feat(<layer>): ... (green)`.
3. Refactor with tests green. Run `dotnet test` in `backend/` and `npm test -- --watch=false` in `frontend/` before declaring done.

## Backend rules

- SQL is always parameterized with explicit `SqlDbType` and size (`WithParameter`). Never use string concatenation or `AddWithValue`.
- Every task query filters by `UserId` taken from the JWT `sub` claim, never from the request body.
- Map `DATETIME2` to `DateTimeKind.Utc` and `DATE` to `DateOnly`.
- Validation lives in the Domain (entity factories and methods) and throws `DomainValidationException` with **all** field errors at once.
- Never return 500 for expected failures. Map them in `GlobalExceptionHandler` (400, 401, 404, 409).
- Use `TimeProvider` (never `DateTime.UtcNow`) so time-dependent rules are testable.
- `async` all the way, and pass the `CancellationToken` through.
- Testing libraries: xUnit, NSubstitute and `FakeTimeProvider`. No FluentAssertions v8+ (commercial license) and no Moq.

## Frontend rules

- Standalone components, `ChangeDetectionStrategy.OnPush`, `inject()`, `input()` and `output()`, signals for state, and the new control flow (`@if`, `@for`).
- Containers talk to stores. Presentational components only take inputs and emit outputs.
- Calendar dates use `shared/date-utils.ts`. Never use `toISOString()` for a due date.
- Every icon-only button needs an `aria-label`. Avoid browser console warnings.

## Definition of done

Build passes with `TreatWarningsAsErrors`, all tests are green, there are no console errors in the browser, and the README stays accurate.
