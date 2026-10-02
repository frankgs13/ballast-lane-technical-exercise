# Presentation: thought process and design decisions

A suggested 15-minute walkthrough followed by the code review. Each section maps to an evaluation criterion.

---

## 1. Understanding the problem (1 min)

The exercise asks for a small but **production-minded** full-stack app. I read the evaluation criteria first and used them as my definition of done:

| Criterion | How I addressed it |
| --- | --- |
| Clean Architecture | 4 projects with a one-way dependency rule, **enforced by tests** |
| Application testing / TDD | Tests committed before each layer's implementation; 175 automated tests |
| Code quality | Warnings as errors, analyzers, nullable, strict TypeScript, small focused classes |
| Functionality | Full CRUD, auth, filtering, overdue detection; verified with a headless browser run |
| Presentation | This document, the README and a demo script |
| GenAI | [GENAI.md](GENAI.md): prompt, output, validation, corrections |

Constraint that shaped the design: **no EF, Dapper or MediatR**. They want to see that I understand what an ORM does for me: parameters, mapping, connection lifetime, constraints and transactions.

## 2. User story (1 min)

> As a busy professional, I want a private task list with title, description, status and due date, so that I know what is pending, in progress or overdue, and nobody else can see my tasks.

From that story I derived the acceptance criteria in the README. Every criterion maps to at least one test. The story also made **ownership** the most important non-functional requirement: it's a multi-user system where each user's data is private.

## 3. Demo (4 min)

1. Open http://localhost:4200, which redirects to **login** (route guard).
2. Wrong password: a generic error ("Invalid username or password"), with no hint about which part was wrong.
3. **Use demo**, then sign in. Five seeded tasks; the red one is **overdue**.
4. **New task** with an empty title shows inline validation. Fill it in and save: a snackbar confirms, and the list stays sorted by due date.
5. Change status from the card menu (Pending to Completed); the title gets a strike-through.
6. Edit the overdue task: it can be saved **without** changing its past due date (business rule).
7. Filter by status plus search; the counts update live (computed signals).
8. Delete with a confirmation dialog.
9. Resize to a phone width: single column and a floating "New task" button.
10. Sign out and sign in as `jane` / `Jane123!`: a different list (**isolation**).
11. Swagger at http://localhost:5080/swagger: log in, **Authorize**, call `GET /api/tasks`. Without the token it returns `401`.
12. Optional: SSMS on `(localdb)\MSSQLLocalDB` > `TaskManager` > `dbo.Tasks` shows the data and the clustered index on `(UserId, DueDate)`.

## 4. Architecture (3 min)

```
Api ──► Application ──► Domain
             ▲
Infrastructure (implements Application's ports)
```

- **Domain**: `TaskItem` and `User` protect their own invariants (factory methods, private setters). Zero dependencies, so it is trivially unit-testable.
- **Application**: use cases (`TaskService`, `AuthService`) orchestrate the domain and talk to **ports** (`ITaskRepository`, `IPasswordHasher`, `ITokenService`). It doesn't know SQL Server or HTTP exist.
- **Infrastructure**: adapters. ADO.NET repositories, the schema initializer, the PBKDF2 hasher and the JWT issuer.
- **Api**: thin controllers (a couple of lines each), a global exception handler that maps exceptions to RFC 7807, JWT, rate limiting and Swagger.

Why it matters: I can swap SQL Server for another store, or controllers for gRPC, without touching a single business rule. `DependencyRuleTests` makes the rule a failing test, not a wiki page.

Request flow for `PUT /api/tasks/{id}`:

1. JwtBearer validates the token, and the controller reads `sub` (never a user id from the body).
2. `TaskService.UpdateAsync` loads the task **scoped by user**, or returns 404.
3. `TaskItem.Update` validates everything and throws `DomainValidationException` with all field errors.
4. `TaskRepository.UpdateAsync` runs `WHERE Id = @Id AND UserId = @UserId` and returns whether a row was affected.
5. `GlobalExceptionHandler` maps any exception to 400, 404 or 409 problem details.

## 5. Key design decisions (3 min)

| Decision | Alternatives | Why |
| --- | --- | --- |
| ADO.NET with explicit `SqlDbType` and size | `AddWithValue`, string SQL | Plan-cache stability, no implicit conversions, injection-proof |
| Embedded idempotent `.sql` scripts run at startup | EF migrations (not allowed), DbUp | Zero dependencies and a reviewer can run it in one step. A real migrations tool comes next. |
| GUID PK **non-clustered**, cluster on `(UserId, DueDate)` | Clustered GUID | Avoids page splits and matches the main query: "my tasks by due date" |
| Validation in the **Domain**, returning all errors at once | DataAnnotations or FluentValidation in the API | One source of truth, testable without HTTP, better UX |
| 404 (not 403) for other users' tasks | 403 | Don't confirm that a resource exists |
| ASP.NET Identity `PasswordHasher` only | Custom SHA-256 | Never roll your own crypto. It's salted, iterated and versioned, and doesn't pull in EF stores. |
| `TimeProvider` everywhere | `DateTime.UtcNow` | Deterministic tests for due-date and expiry rules |
| Fallback auth policy (secure by default) | `[Authorize]` per controller | New endpoints are protected unless someone explicitly opts out |
| Angular zoneless + signals + OnPush | Zone.js + RxJS state | Current Angular direction, fine-grained updates, simpler state |
| Page-scoped `TasksStore` | NgRx | Right-sized for one feature; easy to replace if the app grows |
| Calendar dates as `DateOnly` / `yyyy-MM-dd` | `DateTime` | A due date is a day, not an instant. Avoids time-zone off-by-one bugs. |

## 6. TDD approach (1 min)

`git log --oneline` shows the rhythm per layer:

```
test(domain): specify TaskItem and User invariants (red)
feat(domain): implement TaskItem and User with invariants (green)
test(application): specify TaskService and AuthService behaviour (red)
feat(application): implement TaskService, AuthService and ports (green)
test(infrastructure): ... (red)
feat(infrastructure): ... (green)
test(api): ... (red)
feat(api): ... (green)
```

Test pyramid: fast domain and service unit tests (mocks only at the ports), API tests through the real HTTP pipeline with in-memory fakes, and repository tests against a **real** disposable SQL Server database, because mocking ADO.NET proves nothing about SQL.

## 7. GenAI (2 min)

See [GENAI.md](GENAI.md). Talking points:

- The prompt is structured like a ticket: constraints, business rules, security, tests-first, output format. Persistent rules live in `AGENTS.md`.
- I worked in small red/green steps and never accepted a large diff I couldn't verify.
- What I caught: a real UI bug found by the headless browser run (Cancel showed "Task undefined created"), a **wrong explanation** in an AI-written comment about time zones, a build budget overrun, and ChatGPT advice I rejected (a public task list, PostgreSQL).

## 8. Trade-offs and next steps (30 s)

- Token in localStorage, kept simple for a stateless demo. Next: short-lived access token plus refresh token in an httpOnly cookie.
- No pagination yet; the list is small per user. Next: `OFFSET/FETCH` with a total count.
- Last write wins on concurrent edits. Next: `rowversion` with `If-Match` / ETag and a 409 response.
- Schema scripts are applied at startup for demo convenience. Next: DbUp or grate in CI/CD, plus GitHub Actions running both test suites and Docker images.

---

## Likely questions, prepared answers

**Why not put validation in the controller with DataAnnotations?**
Because the rules belong to the business, not to HTTP. The domain enforces them no matter who calls it (API, a background job, tests), and it returns every error at once. DataAnnotations would duplicate the rules and couldn't express "the due date may stay in the past only if it didn't change".

**How do you prevent SQL injection without an ORM?**
Every value goes through `SqlParameter` with an explicit type and size; there's no string concatenation anywhere. The only dynamic SQL is `CREATE DATABASE`, where identifiers can't be parameterized, so it uses `QUOTENAME`. Tests send injection payloads.

**Why is `GET /api/tasks/{otherUsersId}` a 404 and not a 403?**
A 403 confirms the id exists. Ownership is part of the query (`WHERE UserId = @UserId`), so for that user the task simply doesn't exist.

**What happens if two people register the same username at the same moment?**
Both pass the pre-check, but the unique constraint rejects the second insert. The repository maps SQL error 2627/2601 to a `ConflictException`, which becomes a 409. The pre-check is for UX; the constraint is the guarantee.

**Why GUIDs?**
They can be generated in the domain without a database round trip, and they don't leak record counts or allow enumeration. The downside (index fragmentation) is mitigated with a non-clustered PK.

**How would you scale this?**
The API is stateless (JWT), so it scales horizontally behind a load balancer. Connection pooling is already in place. Next would be pagination, caching read-heavy queries, and moving the rate limiter to a distributed store such as Redis.

**Why is the time-zone tolerance one day?**
The server only knows UTC. A user in UTC-6 at 7 pm is already on the next day in UTC, so a strict "today in UTC" rule would reject their local "today". One day of slack covers every time zone; the UI enforces the user's local today. The precise fix would be sending the client's time zone.

**What would you test next?**
Playwright E2E in CI (the script used during development is a starting point), plus load tests for the list endpoint.
