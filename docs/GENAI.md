# Generative AI usage

**Tools:** Cursor (agent mode) with Claude as the main coding assistant, plus ChatGPT as a second opinion while planning.
**My principle:** the AI writes a draft quickly. I own the design, the constraints and the verification. Nothing is accepted because it "looks right"; it is accepted because a test, the compiler or a real run proves it.

This document answers the exercise's GenAI section:

1. [The prompt I would use](#1-the-prompt) to generate a RESTful task management API
2. [The output](#2-the-output-representative-sample) (this repository is the output; key excerpts below)
3. [How I validated the suggestions](#3-how-i-validated-the-ais-suggestions)
4. [What I corrected or improved](#4-what-i-corrected-or-improved)
5. [How edge cases, authentication and validation were handled](#5-edge-cases-authentication-and-validation)

---

## 1. The prompt

A good prompt for code generation reads like a **ticket for a senior engineer**: context, hard constraints, explicit non-goals, acceptance criteria and the expected output format. Vague prompts ("make a task API") produce generic code, usually with EF Core, no ownership checks and happy-path-only tests.

Persistent rules (architecture, libraries, conventions) live in [`AGENTS.md`](../AGENTS.md), which Cursor loads into every request. The task prompt can then focus on intent.

### Main prompt (scaffold and full implementation)

```text
ROLE
You are a senior .NET engineer pairing with me. Follow AGENTS.md strictly.

CONTEXT
Build a RESTful API for a simple task management system. Users register and log in;
each user manages only their own tasks. Assume a basic User model (Id, Username, Email,
PasswordHash, CreatedAtUtc).

STACK & HARD CONSTRAINTS
- .NET 9, ASP.NET Core Web API with controllers, SQL Server.
- Data access with raw ADO.NET (Microsoft.Data.SqlClient). Do NOT use Entity Framework,
  Dapper or MediatR.
- Clean Architecture: Domain (no dependencies) <- Application (use cases + repository
  interfaces) <- Infrastructure (ADO.NET, hashing, JWT) ; Api depends on Application.
  The business layer must not reference the data layer or ASP.NET.

FUNCTIONAL REQUIREMENTS
- Task: Id (GUID), Title, Description, Status (Pending | InProgress | Completed), DueDate
  (calendar date), CreatedAtUtc, UpdatedAtUtc, owned by one user.
- CRUD endpoints under /api/tasks with correct verbs and status codes
  (GET list + optional ?status filter, GET by id, POST -> 201 + Location, PUT -> 200,
  DELETE -> 204, 404 when not found).
- /api/auth/register and /api/auth/login (public) returning a JWT; /api/auth/me (protected).

BUSINESS RULES
- Title required, trimmed, max 200. Description optional, max 2000 (blank -> null).
- DueDate required and not in the past when created or changed. An overdue task must stay
  editable without changing its date.
- New tasks start as Pending. Unknown status values are rejected.
- Report ALL validation errors at once, keyed by field name.

SECURITY
- Hash passwords with a vetted PBKDF2 implementation (no custom crypto).
- JWT: validate issuer, audience, lifetime and signing key; key >= 256 bits, validated on startup.
- Every endpoint requires auth unless explicitly marked [AllowAnonymous].
- A user must never read or modify another user's task: filter by UserId in SQL and return
  404 (not 403) for foreign ids.
- Login must not reveal whether the username exists (same message and similar timing).
- All SQL parameterized with explicit SqlDbType and size.

ERRORS
- RFC 7807 ProblemDetails via a global IExceptionHandler: 400 validation, 401, 404,
  409 duplicate username/email (also handle the unique-constraint race), 500 without
  internal details.

TESTS (TDD)
- Write the tests FIRST for each layer, then the implementation.
- xUnit + NSubstitute + FakeTimeProvider (no Moq, no FluentAssertions).
- Unit tests: domain invariants, services (mocked ports).
- Integration tests: repositories against a disposable real SQL Server database (skip if
  unavailable); API tests with WebApplicationFactory covering 401s, validation and
  cross-user isolation.

OUTPUT
- Work layer by layer: Domain -> Application -> Infrastructure -> Api. For each layer,
  show the tests, then the code, then run the tests and report results.
- List any assumption you made instead of silently guessing.
- Do not add features that were not requested.
```

### Why it is written this way

| Technique | Purpose |
| --- | --- |
| Role plus a persistent rules file (`AGENTS.md`) | Consistent conventions across dozens of requests without repeating them |
| **Negative constraints** ("Do NOT use EF, Dapper, MediatR") | Models default to EF Core for .NET CRUD unless told otherwise |
| Business rules as bullet points | Each bullet becomes one or more tests, so the prompt doubles as acceptance criteria |
| Explicit security section | Ownership checks (IDOR), user enumeration and key validation are the first things AI scaffolds omit |
| "Tests first, layer by layer, run them" | Small verifiable steps instead of a 3,000-line diff I can't review |
| "List assumptions, don't add unrequested features" | Prevents silent scope creep and hallucinated requirements |

### Follow-up prompts used during development

Short, focused prompts on top of the main one:

- *"Write the failing xUnit tests for `TaskItem` invariants only. Don't implement anything yet."*
- *"Now implement `TaskItem` with the minimum code to make those tests pass."*
- *"Review `TaskRepository` as a security reviewer: injection, ownership and type mapping. List problems before changing code."*
- *"The repository tests must run against real SQL Server, not mocks. Create a fixture that creates and drops a uniquely named database and skips when SQL is unavailable."*
- *"Write a Playwright script that drives the whole UI flow headless and reports console warnings and failed requests."*

---

## 2. The output (representative sample)

The whole repository is the output after review. A few excerpts show the style and the rules from the prompt in action.

**Ownership enforced in SQL** ([`TaskRepository.cs`](../backend/src/TaskManager.Infrastructure/Persistence/TaskRepository.cs)):

```csharp
command.CommandText = """
    UPDATE dbo.Tasks
    SET Title = @Title, Description = @Description, Status = @Status,
        DueDate = @DueDate, UpdatedAtUtc = @UpdatedAtUtc
    WHERE Id = @Id AND UserId = @UserId;
    """;
AddTaskParameters(command, task);   // explicit SqlDbType + size for every parameter

return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
```

**Business rule in the domain, not in the controller** ([`TaskItem.cs`](../backend/src/TaskManager.Domain/Tasks/TaskItem.cs)):

```csharp
// An overdue task must remain editable (e.g. to mark it Completed), so the
// "not in the past" rule only applies when the due date actually changes.
if (dueDate != DueDate)
{
    ValidateDueDate(dueDate, nowUtc, errors);
}
errors.ThrowIfAny();
```

**Thin controller** ([`TasksController.cs`](../backend/src/TaskManager.Api/Controllers/TasksController.cs)):

```csharp
[HttpPost]
public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request, CancellationToken ct)
{
    var created = await taskService.CreateAsync(User.GetRequiredUserId(), request, ct);
    return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
}
```

**A test written before the code** ([`TaskEndpointsTests.cs`](../backend/tests/TaskManager.IntegrationTests/Api/TaskEndpointsTests.cs)):

```csharp
[Fact]
public async Task UsersCannotSeeOrModifyEachOthersTasks()
{
    var (alice, _) = await factory.CreateAuthenticatedClientAsync();
    var (bob, _) = await factory.CreateAuthenticatedClientAsync();
    var aliceTask = await CreateTaskAsync(alice, "Alice's secret");

    Assert.Empty((await bob.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json))!);
    Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/tasks/{aliceTask.Id}")).StatusCode);
    // ... PUT and DELETE also return 404, and Alice's task is unchanged
}
```

---

## 3. How I validated the AI's suggestions

Verification happens in layers, from cheapest to most realistic:

1. **Compiler as the first reviewer.** `TreatWarningsAsErrors`, nullable reference types, .NET analyzers at `latest-recommended`, and TypeScript `strict` plus `strictTemplates`. Generated code that is sloppy doesn't compile.
2. **Tests first (red, then green).** The specs were written and committed *before* each layer's implementation (see `git log`). AI-written code had to satisfy tests whose expectations I had reviewed, rather than tests written afterwards to match whatever the code did.
3. **Real infrastructure, not mocks, where it matters.** Repository tests run against a throw-away SQL Server database, which catches collation, `DateTimeKind`, `DATE` mapping and constraint behaviour that mocks would hide. The tests confirm that `DateTimeKind.Utc` is preserved and that `FRANCISCO` collides with `francisco`.
4. **The HTTP contract tested end to end** with `WebApplicationFactory`: real routing, JWT validation (tampered and expired tokens), JSON enum serialization, ProblemDetails shape.
5. **Executable architecture rules.** [`DependencyRuleTests`](../backend/tests/TaskManager.UnitTests/Architecture/DependencyRuleTests.cs) fails if Application references SQL or ASP.NET, or if EF, Dapper or MediatR ever appear.
6. **Driving the real UI.** A headless Chrome script (Playwright) ran the full journey: login with a wrong password, demo login, validation, create, status change, edit, filter and search, delete, mobile viewport, register, duplicate username, and protected-route redirect. It collected console warnings and failed requests. **This found a real bug (below).**
7. **Manual review against a checklist** (section 6) and the official docs for anything version-specific (Angular 22 zoneless APIs, .NET 9 templates without Swagger, Swashbuckle with Microsoft.OpenApi v1).

Result: 131 backend tests and 44 frontend tests, 86% line and 82% branch coverage on the backend, and zero app-generated console warnings.

---

## 4. What I corrected or improved

These are real issues from this project, not hypothetical ones.

### Bugs and mistakes caught

| # | What the AI produced | How it was caught | Fix |
| --- | --- | --- | --- |
| 1 | The task dialog's **Cancel** showed a snackbar: *Task "undefined" created*. The code filtered `afterClosed()` with `task !== undefined`, but a bare `mat-dialog-close` closes with `""`. | Headless browser run (screenshot) | Use a type guard that accepts only an object result. It now only fires for a saved task. |
| 2 | A code comment explaining the `toISOString()` date bug was **factually wrong**: it said the shift happens in Mexico (UTC-6), but local midnight converted to UTC is still the same day there. The real failures are `new Date('yyyy-MM-dd')` west of Greenwich and `toISOString()` east of it. | Reading the reasoning, not just the code | Rewrote the explanation and added tests for the round trip. Lesson: AI explanations need the same scrutiny as AI code. |
| 3 | The .NET 9 template `Program.cs` used `AddOpenApi()` without a Bearer-token UI, and the first build failed after packages changed. | Build | Swashbuckle with a JWT security scheme and XML comments, so reviewers can call protected endpoints from Swagger. |
| 4 | The production build exceeded Angular's 500 kB initial budget because the authenticated shell was eagerly loaded. | Build budget warning | Lazy-load the shell route. The initial bundle is now 486 kB, with no warnings. |
| 5 | A "red" TDD commit accidentally included the half-generated Angular project (the scaffolder was running in the background). | Reviewing `git show --stat` | Rewrote the commit so the history shows only the test files, keeping the TDD story honest. |

### Recommendations I deliberately did **not** follow

| Suggestion (ChatGPT, during planning) | Decision | Reason |
| --- | --- | --- |
| PostgreSQL with Npgsql | **SQL Server** with `Microsoft.Data.SqlClient` | It's my strongest database and the team's stack. Same ADO.NET patterns. |
| Leave `GET /api/tasks` public "to show authorized vs non-authorized" | **Rejected** | Tasks are private per user; a public list would be a data leak. The non-authorized endpoints are `register`, `login` and `health`. |
| Generic xUnit setup (AI models often add Moq and FluentAssertions) | **NSubstitute** and plain `Assert` | FluentAssertions v8 requires a commercial license; Moq had the SponsorLink controversy. Fewer dependencies to justify. |
| Store the token in localStorage without discussion | Kept, but **documented the trade-off** | It's simpler for a stateless demo API. The safer alternative (httpOnly refresh cookie) is listed as next step. |

### Hardening I required beyond the first draft

The prompt asked for these explicitly because AI scaffolds commonly miss them, and each one has a test:

- `AddWithValue` replaced by explicitly typed parameters (plan cache and implicit conversions).
- `DateTime` read from SQL is marked `Utc` (it is `Unspecified` by default).
- A random GUID primary key is **non-clustered**; the table is clustered on `(UserId, DueDate)`, which matches the main query.
- JWT `MapInboundClaims = false`, so `sub` stays `sub` instead of a long SOAP-style claim URI.
- Request DTOs use nullable properties, so a missing `status` is a validation error instead of silently defaulting to `Pending`.
- A unique-constraint violation (concurrent registration race) maps to `409`, not `500`.

---

## 5. Edge cases, authentication and validation

| Edge case | Where it's handled | Test |
| --- | --- | --- |
| Missing, blank, too long title or description | `TaskItem.Create` / `Update` | `TaskItemTests` |
| Several invalid fields at once, all reported together | `ValidationErrors` collects them, `DomainValidationException` | `Create_WithSeveralInvalidFields_ReportsAllErrorsAtOnce` |
| Due date in the past, with **time-zone tolerance** (UTC "today" vs the user's local today) | `TaskItem.ValidateDueDate` allows UTC today minus 1 day | `Create_WithDueDateYesterdayInUtc_...` |
| Overdue task edited without changing the date | Rule applies only when the date changes | `Update_OverdueTaskKeepingSameDueDate_IsAllowed` |
| Unknown status (`"Archived"`, `99`) | JSON enum binding, then `Enum.IsDefined` | `Update_WithUnknownStatus_Returns400`, `Update_WithUndefinedStatus_Throws` |
| Malformed JSON | `[ApiController]` automatic 400 | `Create_WithMalformedJson_Returns400` |
| Non-GUID id in the route | `{id:guid}` constraint returns 404 | `Get_WithNonGuidId_Returns404` |
| Another user's task id (IDOR) | `WHERE Id = @Id AND UserId = @UserId`, 404 | `UsersCannotSeeOrModifyEachOthersTasks`, repository tests |
| Task deleted between read and update | `UpdateAsync` returns false, 404 | `UpdateAsync_WhenTaskDeletedConcurrently_ThrowsNotFound` |
| Token for a user who no longer exists | `ExistsAsync` check before creating, 404 | `CreateAsync_WhenUserNoLongerExists_ThrowsNotFound` |
| Missing, tampered or expired token | JwtBearer validation, 401 | `ProtectedEndpoint_WithTamperedToken/ExpiredToken_Returns401` |
| Username enumeration via message or timing | Generic message, and hash verify runs even for unknown users | `LoginAsync_WithUnknownUser_StillVerifiesAPassword...` |
| Duplicate username (case-insensitive) or email | Pre-check, then `UQ_` constraint mapped to 409 | `Register_WithTakenUsername_Returns409`, repository tests |
| Weak or huge password | `PasswordPolicy` (8 to 128 characters, upper, lower, digit) | `RegisterAsync_WithWeakPassword_ThrowsValidation` |
| SQL injection and Unicode in input | Parameters, `NVARCHAR` | `..._WithSqlInjectionPayload_...`, `..._WithUnicodeAndQuotes_...` |
| Brute force on login | Fixed-window rate limiter per IP, 429 | Config-driven; the UI shows a friendly message |
| XSS in a task title | Angular interpolation escapes it | `renders user content as text, not HTML` |
| Session expiry while the app is open | Timer logs out, and the interceptor logs out on 401 | `auth.service.spec.ts` |
| Open redirect via `?returnUrl=//evil.com` | `safeReturnUrl` | `auth.service.spec.ts` |
| Token leaking to third-party URLs | The interceptor attaches it only to `/api/*` | `never sends the token to other origins` |
| Corrupted localStorage | Parsing wrapped in try/catch, session cleared | `survives corrupted storage` |

---

## 6. My checklist for reviewing AI-generated code

1. **Does it meet the constraints?** (here: no EF, Dapper or MediatR, and the layer rule) Check by automation, not by eye.
2. **Authorization, not just authentication:** can user A touch user B's data?
3. **Input at every boundary:** missing fields, wrong types, extreme lengths, unknown enums, malformed JSON.
4. **Time:** UTC versus local time, calendar date versus instant, testability (`TimeProvider`).
5. **Data access:** parameterized SQL, explicit types, connection disposal, constraint violations, concurrency.
6. **Errors:** expected failures never return 500; no stack traces or internal details leak.
7. **Secrets:** nothing real in source control; weak configuration fails fast at startup.
8. **Tests that would fail if the code were wrong:** I read the assertions, not just the green checkmark.
9. **Version drift:** AI often writes code for an older framework version. Check against the actual SDK and docs.
10. **Is every line needed?** Remove speculative abstractions and unrequested features.

## Where AI helped most, and where I didn't trust it

- **Most helpful:** boilerplate (ADO.NET mapping, DI wiring, Material templates), generating many edge-case tests quickly, and acting as a second reviewer when asked to critique code.
- **Not trusted without proof:** security decisions, date and time logic, framework-version-specific APIs, and its own explanations of *why* something works (see correction #2).
