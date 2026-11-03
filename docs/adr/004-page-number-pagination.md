# 004 — Page-number pagination with keyset navigation and PagedResponse envelope

**Date:** 2026-05-19
**Status:** Accepted

## Context

PR #10 introduced `PagedResponse<T> { items, page, pageSize, total }` on Requirements. NbJa3 extended the same shape to Projects, Features, and WorkItems. The shape was locked before deciding whether page-number or cursor pagination is the long-term strategy.

Pure `OFFSET/LIMIT` pagination has two known failure modes at scale:
1. **Unbounded skip cost.** `OFFSET n` forces PostgreSQL to scan and discard the first `n` rows regardless of indexes. Performance degrades linearly with page depth.
2. **Count/data race.** Two separate round-trips (`CountAsync` + `ToListAsync`) are not snapshot-isolated — concurrent inserts/deletes produce a `total` that disagrees with the returned page.

Cursor-only pagination (last-seen ID as the cursor) fixes both but loses the "page X of Y" and URL-addressable-page UX that is central to the requirement review workflow.

## Decision

**Hybrid: page-number UX backed by keyset navigation.**

The API exposes page-number semantics (`page`, `pageSize`, `total`, `hasMore`, `totalPages`) so the frontend and URLs stay simple. Internally, the query uses a keyset boundary — not `OFFSET` — to fetch the page. The response includes `lastSeenId` (the stable logical ID of the last item on the page) so the client can pass it back as an optional hint on the next request, bypassing the OFFSET entirely.

**Request:** `?page=3&pageSize=20&lastSeenId=<uuid>` (client passes `lastSeenId` from the previous response; omitted on the first page or any direct-URL navigation)

**Server behaviour:**
- If `lastSeenId` is provided: do a single indexed PK lookup to fetch the sort value for that row, then issue `WHERE (sort_col, logical_id) > (:last_sort_val, :last_id)` — no skip cost. The client only ever sends a UUID; the server resolves the sort boundary internally.
- If `lastSeenId` is absent (first page, direct URL navigation, bookmark) or stale (item deleted between pages): fall back to `OFFSET` — correctness preserved, performance degrades only in this case.
- If the sort order or active filters differ from when `lastSeenId` was issued (e.g. user changed sort column), `lastSeenId` is ignored and OFFSET is used.

**Count:** always via `COUNT(*) OVER()` window function in the same query as the data fetch — single round-trip, no race.

**`PagedResponse<T>` envelope shape:**

```csharp
record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    int TotalPages,
    Guid? LastSeenId        // logical ID of the last item; null if Items is empty
);
```

`HasMore` = `Page * PageSize < Total`. `TotalPages` = `(int)Math.Ceiling((double)Total / PageSize)`. These are computed on the backend — the frontend does not duplicate the arithmetic.

## Index requirements

The keyset query is only efficient if the active sort column and logical ID are indexed together, scoped under the parent ID. Every paginated endpoint must have a composite index that matches its `ORDER BY` clause exactly.

**Default sort indexes (required before keyset ships):**

| Entity | Default sort | Index |
|---|---|---|
| Requirements | `ChangedAt DESC` | `idx_req_feature_sort (FeatureId, ChangedAt DESC, RequirementId DESC)` |
| WorkItems | `ChangedAt DESC` | `idx_wi_project_sort (ProjectId, ChangedAt DESC, WorkItemId DESC)` |
| Features | `CreatedAt DESC` | `idx_feat_project_sort (ProjectId, CreatedAt DESC, FeatureId DESC)` |
| Projects | `CreatedAt DESC` | `idx_proj_team_sort (TeamId, CreatedAt DESC, ProjectId DESC)` |

Add these in EF Core migrations before the keyset path is enabled. The OFFSET fallback also benefits from them (same `ORDER BY` clause).

**Additional sort options (e.g. alphabetical):**

Each supported sort column requires its own composite index. Add the index in the same migration that enables the sort option — never speculatively. Example for alphabetical requirement sort:

```
idx_req_feature_alpha (FeatureId, Text ASC, RequirementId ASC)
```

The `lastSeenId` server-side lookup fetches the relevant sort value from whichever index matches the active sort — the client interface (`lastSeenId` UUID) does not change.

**Filter dimensions:**

Filtered keyset queries are compatible — filter predicates compose with the keyset boundary in the same `WHERE` clause. The index must lead with the filter column to avoid a full parent-scoped scan:

```
idx_req_feature_status_sort (FeatureId, Status, ChangedAt DESC, RequirementId DESC)
```

Add filter indexes when the filter is implemented, not before. Do not add speculative filter indexes.

## Alternatives Considered

**Pure `OFFSET/LIMIT`:** Simple, but skip cost degrades at depth and two-query race is real. Rejected.

**Pure cursor (no page numbers):** Eliminates both failure modes. Loses "page X of Y" and URL-addressable pages. Does not fit the requirement review use case. Rejected.

**Keyset-only with opaque cursor:** Encodes sort boundary in an opaque token. Hides implementation detail but loses human-readable `page` in URLs. Rejected — URL readability matters for bookmarking and sharing.

## Consequences

- All paginated endpoints use `PagedResponse<T>` with the full shape above.
- Endpoints accept an optional `?lastSeenId=` query parameter alongside `page` and `pageSize`.
- Every paginated entity requires a composite covering index (see table above). Missing indexes must be added in a migration before shipping the endpoint.
- `page` maximum must be validated against `(page - 1) * pageSize` not exceeding a safe `int` row offset (applies to the OFFSET fallback path). Cap on the frontend too.
- The keyset path must be tested: assert that `page=2&lastSeenId=<last-item-of-page-1>` returns the same items as `page=2` without `lastSeenId`, across an insert that happens between the two requests.
- Envelope shape is frozen. Any future extension requires a versioned endpoint.
- Regenerate the TypeScript client after updating the C# record.
