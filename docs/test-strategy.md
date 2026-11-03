# Test Strategy

**Status:** Active — MVP phase  
**Locked:** 2026-05-19

## Philosophy

Proof over vibes. Every path a user can take that can break must have a test. Not 100% line coverage — pragmatic, realistic coverage of user-facing behaviour, with emphasis on the paths most likely to regress under AI-assisted development.

Trivial wiring (DI registration, trivial getters, app bootstrap) is not tested. Business logic, authorization boundaries, data integrity invariants, and user-visible flows are.

## Done includes tests

A feature, fix, or significant refactor is not "done" until the breakage paths it introduces or touches are covered by tests, committed in the same change. No "tests later" — later does not come. This is the working convention every session follows; new untested breakage paths are tracked as debt in `docs/work-state.md` and paid down in priority order (see "Adding tests for existing pages").

## Current state

The backend integration test suite is MVP-only. The frontend has zero tests. This is accepted as a starting point, not a target state. Every new feature or significant refactor must add tests before it is considered complete.

## Backend

### What must always be tested

- **Tenant isolation:** Every new endpoint must have an integration test asserting that a user from tenant B cannot access tenant A's data (returns 403 or 404, not the resource).
- **Versioning invariant:** Mutation paths (create, edit, delete) must assert that a new version row is inserted and the previous row is not modified.
- **Pagination correctness:** Paginated endpoints must test: first page, last page, boundary conditions, filter combinations, and — critically — that count and items are consistent (same snapshot).
- **Input validation:** Required fields missing → 400. Out-of-range values → 400. Invalid types → 400.
- **Authorization levels:** Endpoints requiring Lead role must assert that a Contributor cannot call them.

### What is acceptable to skip

- One-line helper methods with no branching.
- EF Core migration correctness (covered by the migration pipeline itself).
- Framework plumbing (middleware registration, DI setup).

### Tooling

- `Testcontainers` (PostgreSQL, real schema). No in-memory EF Core — the tenant isolation and versioning invariants depend on real DB behaviour.
- `WebApplicationFactory` for endpoint-level tests.
- Tests live in `src/Grundet.Api.Tests/`.

## Frontend

### What must always be tested

- **Forms:** Every create/edit/delete form must have a test covering the happy path, a validation error state, and a server error state.
- **Authorization-gated views:** Pages that should show `notFound()` for cross-project or unauthorized access must assert that path.
- **Data display:** List pages must test that empty state, loading state, and populated state render correctly.
- **Label and filter interactions:** Filter changes must be tested — incorrect filter wiring is a class of silent regression.

### What is acceptable to skip

- Pure presentational components with no logic (e.g. a badge that just applies a colour based on a string prop).
- Tailwind class output.
- Trivial wrappers over shadcn primitives.

### Tooling

- Vitest + `@testing-library/react` (already wired in `verify-frontend.sh`).
- `@testing-library/user-event` for interactions — never `fireEvent`.
- RSC pages: test as plain async functions.
- Client components: render in jsdom.
- No Playwright at MVP. Add E2E smoke tests before the first paying customer.

## Adding tests for existing pages (debt paydown)

The ~25 pages added in PR #10 have zero tests. Debt paydown is required before this surface area grows further. Priority order:

1. Forms with mutations (requirement create/edit, work-item create/edit).
2. Cross-project guard paths (the M3/M4 class).
3. Pagination controls (the requirements list page).
4. Remaining list pages and detail pages.

Debt paydown tasks belong in `docs/work-state.md` as explicit work items, not in this file.

## CI gate

`scripts/verify-backend.sh` and `scripts/verify-frontend.sh` run all tests in CI on every PR and are expected to pass before merge. `main` has branch protection, but as sole maintainer the repo owner can bypass it — treat a bypass as a deliberate, logged exception, not the default path. Test failures should not be waived without a documented reason.
