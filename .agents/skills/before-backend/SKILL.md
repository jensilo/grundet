---
name: before-backend
description: Briefing skill for backend work. Loads the full C#/.NET layer contract, tenant isolation rules, versioning rules, naming conventions, error handling, and EF Core patterns. Run before touching any file in src/Grundet.Api/.
---

# Backend Briefing — Grundet API (C# .NET 10)

Read this before touching any file in `src/Grundet.Api/`.

**File-level lookup, on demand** — which slice owns a route, where cross-cutting helpers live, the versioned-write flow, test locations: `docs/codemap/backend.md`. Load it when you need to locate code, not as part of this briefing; skip it when you already know your target files.

## Project structure

Feature-slice layout — not Clean Architecture:

```
src/Grundet.Api/
  Program.cs
  Data/
    AppDbContext.cs
    Entities/           one file per entity
    Views/              EF Core keyless entities for DB views
    Migrations/
  Features/
    Auth/               AuthEndpoints, AuthService, IAuthStore, AuthStore
    Teams/              TeamEndpoints, TeamService, ITeamStore, TeamStore
    Projects/           ProjectEndpoints, ProjectService, IProjectStore, ProjectStore
    DomainFeatures/     FeatureEndpoints, FeatureService, IFeatureStore, FeatureStore
    Requirements/       RequirementEndpoints, RequirementService, IRequirementStore, RequirementStore
    WorkItems/          WorkItemEndpoints, WorkItemService, IWorkItemStore, WorkItemStore
    Search/             SearchEndpoints, SearchService, ISearchStore, SearchStore
    Jira/               JiraEndpoints, JiraService, IJiraStore, JiraStore
    ...                 (run `ls src/Grundet.Api/Features` for the full list — same
                        Endpoints/Service/Store slice pattern throughout)
  Common/
    Auth/               ResourceAuthorizationService, AuthorizedIds, RlsContext
    Email/              EmailService
    Errors/             HttpProblemException, ProblemTypes
    Diff/               FieldDiff, DiffTypes
```

## Layer contract — non-negotiable

**Endpoint (`*Endpoints.cs`)**
- Parse and validate the HTTP request (required fields, format, bounds).
- Call `IResourceAuthorizationService` to obtain typed `AuthorizedId` structs.
- Call the service with those typed IDs and validated command/query objects.
- Map results to HTTP responses.
- No business logic. No direct store or `DbContext` access. No raw `Guid` to a service.

**Service (`*Service.cs`)**
- Business rules and orchestration.
- Accepts only `AuthorizedId` structs — never raw `Guid`. Compile-time cross-tenant safety.
- Returns domain results, never `IActionResult`.

**Store (`*Store.cs` / `I*Store.cs`)**
- DB access only. No business logic, no authorization.
- Accepts raw `Guid` — stores are internal, called only by services.
- Focused primitives: Get, Insert, Update, List.

## Tenant safety — non-negotiable

`AuthorizedId` structs (`Common/Auth/AuthorizedIds.cs`) have `internal` constructors — only `ResourceAuthorizationService` can construct them. Raw `Guid` to a service is a compile error.

`IResourceAuthorizationService` traverses the ownership chain (e.g. Feature → Project → Team → TeamMember), verifies role, returns the typed ID or throws 403.

**Filter parameters** (e.g. `?featureId=`) are also cross-tenant attack surface. Authorize at the endpoint layer via `RequireXAccessAsync` before passing downstream.

## Versioning — non-negotiable

Every mutation to `Requirement`, `WorkItem`, or `WorkItemLink` inserts a new version row. No in-place updates.

In cascade-delete paths: copy **all** fields from the source entity into the new row — including `ProjectId`, `SequenceNumber`, `Labels`, and any other non-null columns. A partial initializer throws a DB constraint at runtime.

## RLS context

`RlsContext` (`Common/Auth/RlsContext.cs`) is a scoped service. Middleware calls `ApplyAsync()` before route handlers execute. `SearchStore.CreateContextAsync()` also calls it per new connection — always apply RLS context before any query.

## Simplicity — default to less

The default failure mode of AI-generated code is over-engineering: unnecessary abstractions, premature generalisation, helpers that serve one caller, base classes with one subclass. Before adding any new type, method, or abstraction, ask: does the problem force this, or does the spec explicitly require it? If neither, don't build it. Complexity requires justification; simplicity does not. If you find yourself reaching for a pattern because it feels right, stop and reconsider.

## Code style — enforced as build errors

`EnforceCodeStyleInBuild = true` in `Directory.Build.props`. All `:error` editorconfig rules are **hard build failures**, not IDE hints. `dotnet format --verify-no-changes` runs before the build in CI.

**`var` usage** — two rules, opposite directions, both `:error`:
- Built-in types (`int`, `string`, `bool`, `Guid`, etc.): explicit type, never `var`. Write `int count = 0;` not `var count = 0;`.
- Type apparent from the RHS: always `var`. Write `var user = new User();`, `var result = await store.GetAsync(...);`.

**Expression-bodied properties and accessors** — `:error`. Write `public string Name => _name;`, never `{ get { return _name; } }`.

**Simple `using` statement** — `:error`. Write `using var conn = ...;`, never `using (var conn = ...) { }`.

**Pattern matching over cast-check** — `:error`. Write `if (x is Foo f)`, never `if (x is Foo) { var f = (Foo)x; }`.

**Always braces** — `:error`. No braceless `if`/`else`/`for`/`foreach`, even for single-line bodies.

**No `this.` qualification** — `:error`. `this.field`, `this.Property`, `this.Method()` are compile errors. Omit `this.` everywhere.

**Null patterns** — `:error`. Use `?.`, `??`, and `is null` / `is not null`. Never `== null` reference equality, never null-check then cast.

## Naming

- PascalCase types/methods/properties. camelCase locals and parameters.
- Private instance fields: `_camelCase` (underscore prefix). Private static and const fields: `PascalCase`.
- DTOs: `CreateRequirementRequest`, `RequirementResponse`.
- Commands: `CreateRequirementCommand`. Queries: `RequirementSearchQuery`.
- Interfaces: `I`-prefix.

## Minimal API

Every endpoint: `.WithName()`, `.WithSummary()`, `.Produces<T>()`, `.ProducesProblem()`.

## Types

- `record` for DTOs, commands, query objects.
- Nullable reference types enabled. Treat warnings as errors in CI.

## Async

- `async/await` end-to-end. Never `.Result` or `.Wait()`.
- `CancellationToken` on all async I/O.

## EF Core

- `AsNoTracking()` on read-only queries.
- Migrations only — never `EnsureCreated()`. Never modify schema manually.
- `CurrentRequirements`, `CurrentWorkItems`, `CurrentWorkItemLinks` views are keyless entities — always query through them for current-row reads.

## Data access — resource-sensible activation

Never call `ToListAsync()` over a full (or near-full) result set and then filter/paginate in C#. Push all filter predicates to the DB before materialising rows.

**If EF Core cannot translate a predicate** (e.g. PostgreSQL array containment `@>`): use `FromSqlRaw` with `@p0`-style parameters. Never fall back to loading all rows into memory. The query must be covered by an integration test that hits a real PostgreSQL instance (Testcontainers).

**Paginated endpoints:** count and data must be a single DB round-trip using a `COUNT(*) OVER()` window function. Pattern:
```csharp
var rows = await db.Set<T>()
    .Where(filter)
    .OrderBy(sort)
    .Select(r => new { r, Total = EF.Functions.RowNumber(...) }) // or raw SQL with COUNT(*) OVER()
    ...
```
Two separate `CountAsync` + `ToListAsync` calls are prohibited — they race under concurrent writes.

## Request body DTOs

Endpoint request bodies must be typed `record` DTOs. Never use `JsonElement` as a request body parameter — it produces an untyped OpenAPI schema, an untyped generated TypeScript client, and enables silent data-loss bugs. For partial-update semantics, use nullable fields: `null` means "not provided / keep existing value." Document this convention in the DTO record.

Endpoint inputs must be shaped so OpenAPI can expose concrete body schemas and query parameters on the first pass. If an endpoint would force the generated TypeScript client to emit `unknown`, or would hide a caller-visible input behind manual `HttpRequest` parsing, the backend contract is wrong and must be fixed before frontend work continues. The contract chain is backend typed DTO → OpenAPI → generated client → frontend.

When changing an endpoint consumed by the frontend, regenerate the client with `make generate-api`, not ad-hoc `openapi-ts` commands. Ensure the backend serving the current worktree is running first (`make backend` or `make dev`). If using a temporary backend URL, run `OPENAPI_SPEC_URL=http://host:port/openapi/v1.json make generate-api`.

Public API enum contracts are name-based only. JSON enum request bodies must use `JsonStringEnumConverter(allowIntegerValues: false)`. Manual query parsing with `Enum.TryParse` must reject numeric-looking tokens before parsing, because `Enum.TryParse<T>("0", ...)` accepts defined numeric values.

## Error handling

- `HttpProblemException` with typed `ProblemTypes` URIs. No ad-hoc strings.
- RFC 9457 ProblemDetails format. Consistent codes: 400 / 403 / 404 / 409 / 500.
- 409 Conflict for business-rule violations (duplicate, state conflict). 400 for malformed input.
- Backend errors are product contract, not log messages. `type` must be canonical and stable; `detail` must be specific, user-safe, and actionable enough for the frontend to explain what happened. For non-retryable business rules, never emit generic "try again" semantics.
- Caller-visible errors must be represented in OpenAPI with `.ProducesProblem(...)`; if a client needs to distinguish cases, use a canonical `ProblemTypes` URI rather than parsing incidental text.
- Don't swallow exceptions.
