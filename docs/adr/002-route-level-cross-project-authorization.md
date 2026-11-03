# 002 — Route-level cross-project authorization

**Date:** 2026-05-19
**Status:** Accepted

## Context

The backend authorizes resources by ID alone. The URL path (e.g. `/projects/{projectId}/work-items/{workItemId}`) is advisory — nothing prevents a user from requesting a valid `workItemId` under a `projectId` they also have access to but that doesn't own the work item. Until now, guard checks were added per page on the frontend (`if (workItem.projectId !== projectId) notFound()`). This is brittle: every new nested-resource page is a fresh opportunity to forget the guard. Three pages were missed in PR #10 (work-item edit, requirement detail, requirement edit).

## Decision

**Option B:** The backend validates `(parentId, childId)` pairs on every nested route and rejects mismatches at the source.

For example, `GET /projects/{projectId}/work-items/{workItemId}` must verify that `workItem.projectId == projectId` and return 404 if not, rather than returning the item with the frontend expected to guard. The same applies to all nested resource paths.

Frontend per-page guards are removed as the enforcement is moved to the API.

## Alternatives Considered

**Option A — Per-page frontend guards:** Copy the two-line guard pattern to every affected page. Fast to implement but requires every new page to remember the convention. Enforced by code review, not by the compiler or the API.

## Consequences

- Eliminates the cross-project URL bypass class permanently.
- Backend routes become the canonical scope boundary — the source of truth is in one place.
- Requires a backend route-level change for all nested resource endpoints.
- Frontend becomes simpler — `notFound()` is only needed on genuine "resource doesn't exist" paths.
- OpenAPI spec must document that mismatched parent/child IDs return 404.
- Write an integration test for each affected endpoint asserting the mismatch → 404 behaviour.
