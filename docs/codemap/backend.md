# Code Map — Backend (`src/Grundet.Api`)

**Last verified:** 2026-07-11 · Refresh commands: [Maintenance](#maintenance)

## Slice anatomy

Every area under `Features/<Area>/` follows the same four-file pattern:

- `<X>Endpoints.cs` — route handlers, request DTO records (nested at file bottom), endpoint-layer authorization
- `<X>Service.cs` — business logic; command/response records declared at file bottom
- `I<X>Store.cs` + `<X>Store.cs` — EF Core / raw SQL only
- Some slices add a `<X>Models.cs` (Auth, AlphaWaitlist, Feedback) or query records elsewhere (`Search/SearchQueries.cs`, `IWorkItemStore.cs`)

DI registration for every slice and all middleware wiring: `Program.cs`. All REST routes mount under `/api/v1` there.

Layer contract (non-negotiable, see `before-backend`): Endpoint → Service (accepts `AuthorizedId` structs only, never raw `Guid`) → Store. Endpoints never call stores or `DbContext` directly.

## Slice → routes map

"Touching X, start here." Routes are under `/api/v1` unless noted.

| `Features/` dir | Routes owned |
|---|---|
| `AlphaWaitlist/` | `POST /alpha-waitlist` (public, rate-limited) |
| `ApiKeys/` | `/teams/{teamId}/api-keys` — POST, GET, `DELETE /{keyId}`, `DELETE /{keyId}/permanent` (Lead-only, MCP credentials) |
| `Attachments/` | `POST /projects/{projectId}/attachments` (multipart), `GET …/attachments/{attachmentId}/content?variant=full\|thumb` |
| `Auth/` | `/auth/magic-link`, `/auth/verify`, `/auth/logout`; `POST /invite/accept` (mounted at the `/api/v1` root, **not** under `/auth`). Also hosts `MagicLinkTokenCleanupService` |
| `Billing/` | No routes of its own (`GET /teams/{id}/limits` lives in Teams). Quota gates (`ITeamQuotaService`) called by other services; `TeamUsageRecalculationHostedService` |
| `Changelog/` | `GET /projects/{projectId}/changelog`, `…/changelog/authors` |
| `DomainFeatures/` | `/projects/{projectId}/features` CRUD — the domain entity `Feature` (dir name avoids clashing with the `Features/` root) |
| `Drafts/` | `PUT/GET/DELETE` on `/features/{id}/draft`, `/requirements/{id}/draft`, `/projects/{id}/work-draft`, `/work-items/{id}/draft`. Also `DraftCleanupService` |
| `Feedback/` | `POST /feedback` (+ screenshot upload) |
| `Health/` | `GET /health` — mapped at app root, **not** under `/api/v1` |
| `Jira/` | `/teams/{teamId}/jira/*` (connect, connection, projects, project, project-mappings), `GET /jira/callback`, `/projects/{projectId}/jira/project`, `POST /work-items/{id}/jira/export` |
| `Labels/` | `GET /projects/{projectId}/labels` (distinct label list for suggestions) |
| `Mcp/` | `/mcp` — in-process MCP server, not REST. Tools: `ProjectTools.cs` (`list_projects`), `IntentTools.cs` (`list_intent`, `get_intent`, `get_intent_history`). Guard: `McpTeamScope.cs`; auth: `McpApiKeyAuthenticationMiddleware.cs` |
| `Permissions/` | No routes (`GET /teams/{id}/permissions` lives in Teams). `TeamPermissionPolicy.cs` computes the frontend `Permission[]` hint — never the security boundary (ADR-009) |
| `Projects/` | `/teams/{teamId}/projects` CRUD, `GET /projects/{projectId}`, `GET /projects/{projectId}/resolve?ref=` (IN-/WO-/#N reference resolution) |
| `Requirements/` | `/features/{featureId}/requirements` (create/list), `/projects/{projectId}/requirements` (browse, get, update, delete, `…/{id}/attachments`, `POST …/batch-save`), `/requirements/{id}` (+ `/history`, `/versions`, `/work-items`) — attachments hydration is project-scoped only |
| `Search/` | `GET /search` (workspace-wide), `GET /projects/{id}/search`, plus `…/requirements/search`, `…/work-items/search`, `…/features/search` |
| `Teams/` | `/teams` CRUD + `/{teamId}/limits`, `/permissions`, `/members`, `/invites` (+ resend/revoke), member role update |
| `Users/` | `GET/PUT /users/me`, `POST /users/me/onboarding/complete` |
| `WorkItems/` | `/projects/{projectId}/work-items` (create/browse/get/update/delete, `…/{id}/attachments`), `/work-items/{id}` (+ `/history`, `/links`, link history). Caveat: the versions route is `GET /workitems/{id}/versions` — no hyphen; rename tracked in `docs/chore.md` |

## Cross-cutting — `Common/` and `Middleware/`

| Path | Role |
|---|---|
| `Common/Auth/AuthorizedIds.cs` | The 7 `Authorized*Id` structs (Team, TeamMemberUser, Project, Feature, Requirement, WorkItem, WorkItemLink) — `internal` ctors |
| `Common/Auth/ResourceAuthorizationService.cs` | The only constructor of those structs; `Require<X>AccessAsync(...)` walks the ownership chain, throws 403/404 |
| `Common/Auth/RlsContext.cs` | Scoped holder of the caller's user id; `ApplyAsync()` runs `set_config('app.current_user_id', …)` on the DB connection |
| `Common/Auth/ApiKeyAuthenticationHandler.cs`, `CookieAuthenticationExtensions.cs` | The two credential types (see `docs/architecture.md` → Authentication) |
| `Common/Errors/ProblemTypes.cs` | Canonical error `type` URIs — the only place they're defined; mirrored in frontend `lib/api/mutation-error.ts` |
| `Common/Errors/HttpProblemException.cs` (+ handler) | Throw this with a `ProblemTypes` URI; handler renders RFC 9457 ProblemDetails |
| `Common/Validation/TextInput.cs` | Free-text length caps (ADR-007); frontend mirror: `lib/text-limits.ts` |
| `Common/PagedResponse.cs` | The pagination envelope (ADR-004) |
| `Common/Diff/FieldDiff.cs`, `DiffTypes.cs` | Field-level version diffs for history/changelog responses |
| `Common/Email/` | `EmailService`, `EmailTemplates`, `EmailNormalizer` (canonical normalization rule; AdminCli mirrors it in `Core/Email.cs` — keep in sync) |
| `Common/Images/SkiaImageAttachmentStorage.cs` | JPEG re-encode + thumbnail for attachments/feedback |
| `Common/Persistence/StoreQueryHelpers.cs` | Shared store query helpers |
| `Common/OpenApi/BinaryResponseOperationTransformer.cs` | OpenAPI shaping for binary (attachment content) responses |
| `Middleware/RlsMiddleware.cs` | Calls `RlsContext.ApplyAsync()` before route handlers |
| `Middleware/OnboardingGuardMiddleware.cs` (+ `SkipOnboardingGuardMetadata.cs`) | Blocks API use until onboarding completes |
| `Middleware/MagicLinkRequestEnricherMiddleware.cs` | Enriches magic-link requests (rate-limit partitioning) |

## Data layer — `Data/`

- `AppDbContext.cs` — all entity configuration. `SaveChangesAsync` override enforces the append-only invariant: any `Requirement`/`WorkItem`/`WorkItemLink` in `Modified`/`Deleted` state throws. **Trap:** `ExecuteUpdateAsync`/`ExecuteDeleteAsync` bypass the ChangeTracker and this guard — never use them on versioned tables. Also normalizes all DateTimes to UTC.
- `Entities/` — one file per entity; shared enums in `Enums.cs`. Field reference: `docs/architecture.md` → Data Model.
- `Views/` — keyless EF entities over DB views `CurrentRequirements`, `CurrentWorkItems`, `CurrentWorkItemLinks`. All current-state reads go through these.
- `Migrations/` — squashed to `20260705111304_InitialSchema`; migrations are immutable from now on (`docs/memory.md`). View SQL lives here.
- Sequence numbers (`IN-{n}`/`WO-{n}`): one shared per-project counter in `ProjectEntityCounter`, allocated atomically in `RequirementStore`/`WorkItemStore` via `UPDATE … RETURNING`.

## Request pipeline (order matters — `Program.cs`)

```
ForwardedHeaders → ExceptionHandler → CORS → MagicLinkRequestEnricher → RateLimiter
→ Authentication → McpApiKeyAuthentication (promotes ApiKey principal on /mcp)
→ RlsContext → OnboardingGuard → Authorization → endpoints
```

OpenAPI (`/openapi/v1.json`) + Scalar UI are mapped only outside Production. Hosted background services: `MagicLinkTokenCleanupService`, `TeamUsageRecalculationHostedService`, `DraftCleanupService` (`docs/infra/scheduled-jobs.md`).

## Flow: a versioned write (the flow that matters most)

Update requirement, representative of all Requirement/WorkItem/WorkItemLink mutations:

1. `RequirementEndpoints.cs` — validate DTO, `RequireProjectAccessAsync`/`RequireRequirementAccessAsync` → typed IDs. Filter/query params (`?featureId=` etc.) are cross-tenant attack surface and are authorized here too
2. `RequirementService.cs` — load latest version via the `CurrentRequirements` view; `EnsureExpectedVersion` (optimistic concurrency → 409); build a **complete** new `Requirement` row copying every field, new values applied, `ChangeType=Updated`
3. `RequirementStore.cs` — `INSERT` only (`EntityState.Added`); `AppDbContext` guard throws on anything else
4. Response includes the new version + `workItemNotices` (affected linked work)

Cascade delete (`ProjectService`/`FeatureService`): one transaction — soft-delete Feature rows (`DeletedAt`), then insert `Deleted` version rows for every active requirement/work item and `Unlinked` rows for their links. Every inserted row must carry **all** fields; a partial initializer is a runtime DB-constraint error.

## Tests

`src/Grundet.Api.Tests/`, flat files named `<Concern>IntegrationTests.cs`; find a slice's tests by name match. Infrastructure: `Infrastructure/ApiIntegrationFixture.cs` (Testcontainers PostgreSQL + `GrundetApiFactory`), `Infrastructure/TestApi.cs` (static HTTP helpers used by every test). Docker required; `scripts/verify-backend.sh` skips Testcontainers suites when unavailable. Invariant suites to know: `VersioningInvariantIntegrationTests`, `AppendOnlyEnforcementIntegrationTests`, `AuthorizationIntegrationTests`, `RlsIntegrationTests`, `CascadeDeleteIntegrationTests`, `SequenceNumberIntegrationTests`, `TextInputLimitsIntegrationTests`, `OpenApiSchemaIntegrationTests`. Strategy: `docs/test-strategy.md`.

## Maintenance

Update when a slice or route group is added/removed. Verify against reality:

```bash
ls src/Grundet.Api/Features                                              # slice list
rg -n 'Map(Get|Post|Put|Patch|Delete)\("[^"]*"' src/Grundet.Api/Features -o   # full route inventory
rg -n 'MapGroup\("[^"]*"' src/Grundet.Api/Features -o                     # group prefixes
rg -n '^\s*(\w+)\.Map(Get|Post|Put|Patch|Delete)\(' src/Grundet.Api/Features -o -r '$1'  # receiver per route — a file mixing receivers mounts routes at different prefixes (e.g. /invite/accept)
```
