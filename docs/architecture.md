# Architecture

## System Shape

Grundet is a versioned requirements platform with a strict hierarchy:

`Team → Project → Feature → Requirement`, with `WorkItem` as a parallel execution layer linked to requirements.

Core invariant: requirement/work-item/link mutations are **append-only version inserts**, never in-place updates.

## Data Model (Current)

```txt
Team                 Id, Name, Slug, Region, CreatedAt
User                 Id, Email, DisplayName, CreatedAt
TeamMember           TeamId, UserId, Role (Lead | Contributor)
TeamInvite           Id, TeamId, Email, Role, TokenHash, ExpiresAt, AcceptedAt, RevokedAt,
                     RevokedByUserId, CreatedAt
Project              Id, TeamId, Name, Description, CreatedAt, DeletedAt
ProjectEntityCounter ProjectId (PK, FK → Project), NextId (default 1)
Feature              Id, ProjectId, Name, Description, SearchVector, CreatedAt, DeletedAt

Requirement          Id (version PK), RequirementId (logical), ProjectId, FeatureId, SequenceNumber,
                     Text, Status (Draft|Active|Archived|WontDo),
                     Priority (Must|Should|Could|Wont), Labels[], AcceptanceCriteria[],
                     Rationale (durable product truth — "why this matters"; in search vector at weight B),
                     Reason (per-version changelog comment; not in search vector),
                     AttachmentIds Guid[] (image attachments referenced by id, copied forward every version),
                     ChangedAt, ChangedByUserId, ChangeType (Created|Updated|Deleted), SearchVector
                     - current = latest per RequirementId where ChangeType != Deleted
                     - reference format at API layer: IN-{SequenceNumber}

WorkItem             Id (version PK), WorkItemId (logical), ProjectId, SequenceNumber,
                     Title, Description, Status (Todo|InProgress|Done|WontDo),
                     Priority (Low|Medium|High), Type (Feature|Bug|Task),
                     AssignedToUserId, BlockedReason, Labels[], Reason,
                     AttachmentIds Guid[] (image attachments referenced by id, copied forward every version),
                     ChangedAt, ChangedByUserId, ChangeType (Created|Updated|Deleted), SearchVector
                     - current = latest per WorkItemId where ChangeType != Deleted
                     - reference format at API layer: WO-{SequenceNumber}

WorkItemLink         Id (version PK), WorkItemLinkId (logical), WorkItemId, RequirementId,
                     ChangedAt, ChangedByUserId,
                     ChangeType (Linked|Unlinked)
                     - current = latest per WorkItemLinkId where ChangeType != Unlinked
                     - links carry no intent-version snapshot; reads always reflect the latest intent

Attachment           Id, ProjectId, Sha256, OriginalFileName, ContentType, StoragePath,
                     OriginalSizeBytes, StoredSizeBytes, Width, Height, CreatedAt
                     - immutable image blob metadata, scoped to a project and shared across owners
                     - Requirement/WorkItem reference it by id via AttachmentIds — never a FK to a version row
                     - unique (ProjectId, Sha256) dedups identical uploads within a project
                     - append-only: never updated or deleted by the API

Draft                Id, UserId, Kind (Requirement|WorkItem), ScopeId (FeatureId or ProjectId),
                     EntityId (nullable; null = new/create draft), DraftContent (opaque jsonb), UpdatedAt
                     - generic in-progress composer state, generalized over Requirement and WorkItem drafts
                     - DraftContent is owned/interpreted by the frontend only; backend never validates its shape

JiraConnection       Id, TeamId, CloudId, AccessToken, RefreshToken, TokenExpiresAt, JiraProjectKey, CreatedAt
JiraProjectMapping   Id, ProjectId, JiraProjectKey, CreatedAt, UpdatedAt
MagicLinkToken       Id, UserId (nullable), Email, TokenHash, ExpiresAt, UsedAt
TeamApiKey           Id, TeamId, CreatedByUserId, Role (snapshot, metadata-only), Name, KeyHash (unique),
                     Prefix (display-only), ExpiresAt, LastUsedAt, CreatedAt, RevokedAt, RevokedByUserId
                     - mutable credential row, NOT a versioned entity (outside the append-only invariant)
                     - only SHA-256(rawKey) stored; raw key shown once on creation

FeedbackSubmission   Id, UserId, Kind (Feedback|Bug), Message, PagePath, UserAgent, ViewportWidth,
                     ViewportHeight, Locale, TimeZone, CreatedAt, ReadAt, ReadByOperator,
                     DeletedAt, DeletedByOperator
                     - in-app feedback/bug widget submissions; operator triage fields, no versioning
FeedbackAttachment   Id, FeedbackSubmissionId, OriginalFileName, ContentType, StoragePath,
                     OriginalSizeBytes, StoredSizeBytes, Width, Height, CreatedAt
                     - image attachment on a FeedbackSubmission
AlphaWaitlistEntry   Id, EmailOriginal, EmailNormalized, Name, AccountType, TeamSize, LineOfWork,
                     OrganizationName, OrganizationCountry, Reason, Status, CreatedAt, UpdatedAt,
                     ApprovedAt, ApprovedBy, DeclinedAt, DeclinedBy
                     - public alpha-access waitlist signups and operator approve/decline workflow
TeamUsageSnapshot    TeamId (PK), Plan, ProjectCount, MemberCount, RequirementCount, WorkItemCount,
                     RequirementWorkItemStatus, OverLimitSince, EnforcementStartsAt, CalculatedAt
                     - recalculated billing/quota snapshot per team (see Billing and Quota Recalculation below)
```

## Authentication and API Keys

Two credential types, both resolving to the same `"sub"` user identity downstream:

- **Cookie + magic link** — the browser session. The only credential accepted on the general HTTP API.
- **Per-Team API key** (`TeamApiKey`, see ADR-008) — a `Authorization: Bearer grundet_…` credential that
  **authenticates as its issuing user** (`CreatedByUserId`) and is **scoped to one Team** (`TeamId`). In alpha
  it is accepted **only on the `/mcp` endpoint** and is read-only. The `ApiKey` auth handler populates the same
  `"sub"` claim + `RlsContext.UserId` a cookie would, so `IResourceAuthorizationService`, `AuthorizedId`, and
  RLS policies apply **unchanged**. The key's `TeamId` is an *additional* constraint enforced by a team-scope
  guard — never a replacement for resource authorization. The `Role` snapshot is metadata-only: authorization
  uses the issuer's **live** `TeamMembers` role, so removing/downgrading the member revokes key access with no
  extra wiring. Every auth failure (missing / malformed / unknown / expired / revoked) returns one generic 401.

`TeamApiKeys` has its own RLS policies: a GUC-based (`app.api_key_hash`) lookup path for the pre-authentication
handler, plus `rls_is_team_member` for the cookie-authenticated Lead-only management endpoints — mirroring how
`MagicLinkTokens` permits a pre-auth token lookup.

## MCP Server (`/mcp`)

An in-process C# MCP server (`ModelContextProtocol.AspNetCore`, Streamable HTTP, **stateless**) hosted inside
`Grundet.Api` exposes read-only intent tools to agents. It is a thin transport over the **same Service layer** as
the HTTP API — no business-logic duplication. Tools (`Features/Mcp/`):

- `list_projects` — every project in the API key's team, newest first (id, name, description, createdAt); resolves a human project name to the GUID the intent tools require.
- `list_intent` — current requirements for a project (optionally one feature), paginated.
- `get_intent` — a single current requirement by `IN-{n}` within a project.
- `get_intent_history` — full version history (each entry a complete requirement snapshot).

Request path: the `McpApiKeyAuthenticationMiddleware` authenticates the ApiKey scheme for `/mcp` **before**
`RlsMiddleware`, so `app.current_user_id` is the key's issuing user during tool execution. Every tool runs the
`McpTeamScope` guard first, which proves two invariants through the `AuthorizedId` chain: `RequireTeamAccessAsync`
binds the issuer to the key's Team (catching an issuer removed from the team), then `ProjectService.GetProjectAsync`
binds the requested project to that same Team (a project in any other team — even one the issuer also belongs to —
throws not-found, no cross-team signal). The tool then authorizes the specific resource via
`IResourceAuthorizationService` and calls the existing service. Statelessness keeps consecutive requests isolated; this is integration-tested through the real `/mcp`
endpoint (isolation, revocation, RLS-set-during-tool-call, no cross-request context bleed). MCP tool I/O is **not**
part of the generated-TS-client contract chain. The user-facing `read-intent` agent skill that teaches an MCP
client to connect and read intent lives in `src/Grundet.Skills/` (a publishable product artifact, distinct from
the repo's internal dev skills under `.agents/skills/`).

## Billing and Quota Recalculation

Team usage snapshots are recalculated by a hosted API background service. The current PR #14 implementation preserves RLS correctness by setting `app.current_user_id` to a team member while recalculating each team's protected usage counts. This is correct for the current slice, but intentionally not the long-term scalable architecture: the follow-up direction is a narrowly scoped maintenance role/context with batched usage aggregation for this job.

Signup creates a personal Solo workspace named `My work`. Manual team creation is backend-controlled: normal/default mode creates `TeamS`; private alpha deployments may enable `Billing:CreateTeamAlphaWorkspaces` to create `TeamAlpha` teams without exposing that plan as client-selectable. Solo-to-team upgrade semantics are intentionally deferred until product and billing behavior is designed.

## Layer Contract

Three-layer contract is mandatory:

1. **Endpoint**: HTTP/auth/validation and parameter authorization
2. **Service**: business logic on authorized IDs
3. **Store**: persistence only

Optional refinement: Service internals may use **Command/Query separation** when it materially helps (for example testability, caching, or clearer read/write boundaries).

Rules for that refinement:
- Commands/queries stay behind the service boundary (endpoints still call services, not stores directly).
- Use it selectively, not by default; prefer simpler service methods when separation adds no clear value.

## Current Capability Scope

For a human-readable feature snapshot, see [`docs/feature.md`](feature.md).
