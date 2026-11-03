# Grundet — AGENTS.md

## Project

Grundet is a versioned intent layer for modern software teams. One-liner: **versioned product truth for humans and agents.**

Core thesis: as AI makes implementation cheaper, trustworthy product intent becomes the scarce resource. Grundet preserves that intent: structured enough to coordinate work, lightweight enough to stay agile, and readable by humans and agents.

Structure: `Team → Project → Feature → Requirement`. In the UI, requirements may be framed as **Intent** and work items as **Work**. Internally, keep the domain model names unless there is a deliberate migration. Every Requirement mutation produces a new immutable version row — no in-place updates. The version history is the changelog.

| Layer | Technology |
|---|---|
| Backend | C# .NET 10 Minimal API, PostgreSQL + pgvector |
| Frontend | Next.js 16 (App Router), shadcn/ui, Tailwind CSS v4 |
| Infra | Hetzner VPS + Coolify + Docker, GitHub Actions |

Data model: `docs/architecture.md`
Docs index: `docs/README.md`

---

## Before starting work

Always run the appropriate briefing skill before touching any area:

| Area | Skill |
|---|---|
| `src/Grundet.Api/**` (backend) | `before-backend` |
| `src/Grundet.Web/**` (frontend) | `before-frontend` |
| Design / UI decisions / `docs/design/**` | `before-design` |

For current project state: `docs/work-state.md`
For locating code (slice → files → routes, URL → page, flow paths): `docs/codemap/` — load only the part you need (`backend.md`, `frontend.md`, `contract.md`, `repo.md`), on demand, not at session start
For locked decisions and patterns: `docs/memory.md`
For capability snapshot: `docs/feature.md`
For architecture snapshot: `docs/architecture.md`
For product direction, target audience, and scope decisions when relevant: `docs/product-direction.md`
For ADR index and decision rationale: `docs/adr/README.md`
For session history: `docs/sessions/README.md` (index only; load a specific session only when history matters)

---

## After every implementation

Use two verification levels:

- **Fast checks, run often while working:** use `/check backend`, `/check frontend`, or `run_check` (`.pi/checks/*`). These are the agent's quick LSP-ish loop: changed-file backend formatting/analyzer checks plus frontend formatting/linting, no builds/tests.
- **Mid-strength backend check when useful:** `/check backend-build` runs C# builds with `-warnaserror`, without tests.
- **Full slice checks, run before handoff after implementation slices or major changes:** run `format-lint-test` skill. All checks must pass before the task is complete.

Verification scripts (invoked by `format-lint-test`):
- `scripts/verify-backend.sh` — format, build (`-warnaserror`), tests; Docker auto-detected (Testcontainers skipped when unavailable)
- `scripts/verify-frontend.sh` — tsc, Prettier, ESLint, Vitest, Next.js build

Full CI coverage runs in `.github/workflows/`.

At session end, run `after-work` skill to update `docs/work-state.md` and write a session log.

When work changes capability scope or architecture assumptions, update `docs/feature.md` and/or `docs/architecture.md` in the same session.

---

## ADR guidance

Use ADRs for significant and hard-to-reverse architecture/design decisions (see `docs/adr/README.md`). Do not create ADRs for trivial choices; avoid ADR inflation.

## Product doctrine

- Grundet is **not** a traditional Requirements Engineering tool and **not** a Jira clone.
- Intent is product truth: what should be true, why it matters, and how the team knows it is done.
- Work is execution ownership: who is making some slice of intent true.
- Capture product intent at the smallest useful level. Do not force academic atomicity when a leaner team model is clearer.
- Structure should clarify, not slow the team down.
- Prefer features that strengthen shared product truth, immutable history, linking, search, and agent-readable context.
- Avoid workflow/process expansion unless directly validated. Labels are the preferred escape hatch for team-specific structure.
- Built for humans and agents, without making MCP or any single AI protocol the product identity.

## Non-negotiable invariants

**Versioning is sacred.** Every mutation to `Requirement`, `WorkItem`, or `WorkItemLink` must insert a new version row. No in-place updates. No exceptions. No code path may bypass this. In cascade-delete paths, copy all fields from the source entity into the new version row — a partial initializer will throw a DB constraint at runtime.

**Tenant isolation.** Every request must pass through `IResourceAuthorizationService`. Services accept `AuthorizedId` structs as compile-time proof — never raw `Guid`. Filter query parameters (e.g. `?featureId=`) are also a cross-tenant attack surface: authorize them at the endpoint layer. Never call a store directly from an endpoint.

**Three-layer contract.** Endpoint (HTTP, auth, validate) → Service (business logic, AuthorizedId only) → Store (DB access only). Do not collapse layers.

**Resource-sensible data access.** No in-memory full-table scans before pagination or filtering. Push all filter predicates to the DB. If EF Core can't translate a predicate, use `FromSqlRaw` with parameterized queries. Count + data in one round-trip (`COUNT(*) OVER()`). Two separate count/fetch queries are prohibited on paginated endpoints. See `before-backend`.

**API contract chain.** Backend typed DTO → OpenAPI spec → generated TypeScript client → frontend. Endpoint request bodies must be typed `record` DTOs and query params must be OpenAPI-visible. No `JsonElement` on endpoint request bodies. No raw `fetch()`, no generated-client bypasses, and no frontend hand-patched request bodies or `as unknown` casts. If the generated client can't express a call or emits `unknown`, fix the backend first, regenerate, then adjust the frontend. See ADR-002, ADR-003, ADR-004 and `before-backend`, `before-frontend`.

---

## Guardrails

- **No invented facts.** Say "I'm not sure" or ask.
- **95% clarity before acting.** Ask targeted questions first.
- **Scope discipline.** Do only what was asked. Mention adjacent findings; don't build them uninvited.
- **No new dependencies** without explicit discussion.
- **Prefer simple. Resist over-engineering.** The default failure mode of AI-generated code is solutions that are larger, more abstract, or more generalised than the problem requires. Choose the simplest implementation that correctly satisfies the spec. Only introduce complexity when the problem genuinely forces it or the spec explicitly requires it — and even then, debate whether it's necessary.
- **Honest opinions.** When reviewing: distinguish preference / concern / error. Be specific; quote code.
