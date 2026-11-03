# Architecture Decision Records

An ADR documents a significant architectural or technical decision: what was decided, why, and what alternatives were considered.

## When to write an ADR

Write one when:
- You're making a choice that's hard to reverse
- You're choosing between two or more real options
- A future agent or team member would otherwise wonder "why did they do it this way?"

Do not write one for obvious or trivial choices.

## Format

File name: `NNN-short-title.md` (e.g. `001-use-postgresql.md`)

```markdown
# NNN — Title

**Date:** YYYY-MM-DD  
**Status:** Proposed | Accepted | Deprecated | Superseded by [ADR-NNN]

## Context

What is the problem or situation requiring a decision?

## Decision

What was decided?

## Alternatives Considered

What else was evaluated and why was it rejected?

## Consequences

What are the trade-offs or implications of this decision?
```

## Index

- `001-jira-project-routing-per-project.md` — per-project Jira routing with team default fallback for multi-tenant teams.
- `002-route-level-cross-project-authorization.md` — backend validates `(parentId, childId)` pairs; mismatches return 404. No per-page frontend guards.
- `003-typed-request-dtos.md` — all endpoint request bodies use typed `record` DTOs; `JsonElement` is prohibited.
- `004-page-number-pagination.md` — page-number UX backed by keyset navigation; `PagedResponse<T>` extended with `hasMore`, `totalPages`, `lastSeenId`; single round-trip via `COUNT(*) OVER()`; index requirements per entity.
- `005-team-billing-anchor-and-free-team-lifecycle.md` — billing attaches to Team (not User); "My work" auto-created on signup; max 1 free Basic team per user; no silent second auto-create; `ITeamQuotaService` stub in MVP, Stripe post-MVP.
- `006-intent-rationale-vs-change-reason.md` — split durable `Rationale` (product truth, "why this matters", searchable, optional-but-prominent) from per-version `Reason` (edit-only change comment); moving intent to another feature is a history-preserving version row (same logical id), within-project only, not copy+remove.
- `007-input-length-limits.md` — generous per-field character limits for free-text fields, enforced server-side via `Common/Validation/TextInput` (names/titles 200, requirement text 1000, descriptions/rationale 4000, criteria/reasons 500); mirrored as frontend `maxLength`; application-level, no DB migration.
- `008-team-api-keys-mcp-only-acts-as-user.md` — per-Team API key authenticates as the issuing user (acts-as, not a new identity), scoped to one Team, accepted only on `/mcp`, read-only in alpha; role is metadata-only (live `TeamMembers` role enforced); SHA-256 hash storage; native `Actor` model deferred post-alpha.
- `009-server-computed-viewer-permissions.md` — backend computes the viewer's `Permission[]` from a pure `(TeamRole, TeamPlan)` policy and serves it via a dedicated `GET /teams/{teamId}/permissions` endpoint; the frontend renders UI gating from this set instead of re-deriving role/plan client-side; UI hint only, never the security boundary (enforcement parity is tested).
- `010-requirement-image-attachments.md` — requirement images via upload-then-reference: a multipart upload endpoint (owner = project) persists append-only `RequirementAttachment` blobs; create/edit carry an ordered `AttachmentIds[]` copied into every version row (incl. cascade tombstones + `CurrentRequirements` view); bytes served through the generated client as a blob (object URL); re-encode caps raised additively (2560/85); orphans accepted (no GC). **Superseded by 011.**
- `011-unified-project-attachment-storage.md` — unifies attachment storage into one project-scoped `Attachment` table (renamed from `RequirementAttachment`) shared by requirements and work items via owner-agnostic `POST/GET /projects/{projectId}/attachments`; adds project-scoped content dedup (server-computed `Sha256` of original bytes, unique `(ProjectId, Sha256)`, never global) and `WorkItems.AttachmentIds`; single edited migration, no backfill (DBs recreated); append-only/orphan behaviour unchanged.
