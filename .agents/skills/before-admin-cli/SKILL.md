---
name: before-admin-cli
description: Briefing skill for Grundet admin CLI work. Covers direct-DB v1 architecture, dry-run-first mutations, transaction/audit requirements, production safety checks, and future migration to protected admin endpoints. Run before touching src/Grundet.AdminCli/ or admin CLI docs/tests.
---

# Admin CLI Briefing — Grundet

Read this before touching `src/Grundet.AdminCli/` or admin CLI docs/tests.

When changing admin CLI behavior, always update the matching docs under `docs/admin-cli/` in the same change. If a new command namespace is added, add a new docs page and link it from `docs/admin-cli/README.md`.

## Direction

`grundet-admin` is the operational admin surface for founder/operator tasks. It avoids building low-value SaaS admin dashboards while keeping production operations explicit, scriptable, and safe.

Initial scope:

```bash
grundet-admin waitlist list
grundet-admin waitlist show email@example.com
grundet-admin waitlist approve email@example.com
grundet-admin waitlist decline email@example.com
```

Possible later scopes:

```bash
grundet-admin users ...
grundet-admin billing ...
grundet-admin health ...
```

## Access model

- v1 uses direct DB access.
- The API service must not need to stop while the CLI runs.
- Prefer running on the server/container with explicit connection-string configuration.
- Do not expose admin HTTP endpoints in v1.
- Keep code narrow enough that later migration to protected admin endpoints is possible.

## Safety rules — non-negotiable

- Read-only commands may execute directly.
- Mutating commands are dry-run by default.
- Mutating commands require explicit `--execute` to write.
- Mutating commands require explicit operator identity, e.g. `--operator` or a clearly named environment variable.
- Mutating commands must print the target DB host/database before writing.
- Never print full connection strings or secrets.
- Mutating commands must run inside a DB transaction.
- Mutating commands must update by normalized email and expected current status.
- Mutating commands must print:
  - target email
  - normalized email
  - current status
  - proposed new status
  - operator
  - affected row count
- Fail closed on ambiguity:
  - no matching row: no-op, non-zero exit
  - multiple matching rows: abort, non-zero exit
  - unexpected current status: abort unless explicitly treated as idempotent
- No background automation in v1.
- No bulk mutation in v1 unless separately planned.

## Listing rules

- Bounded by default, e.g. default limit 50.
- Prefer explicit filters such as `--status pending`.
- No unbounded full-table dumps.
- Do not print long free-text fields unless explicitly requested.

## Domain invariant boundary

Waitlist approval is outside Grundet's versioned Requirement/WorkItem domain.

The admin CLI must not directly mutate:

- `Requirement`
- `RequirementVersion`
- `WorkItem`
- `WorkItemVersion`
- `WorkItemLink`
- `WorkItemLinkVersion`

If admin operations ever need to touch versioned product/work data, they must go through proper services or an equally strict invariant-preserving path. Versioning remains sacred.

## Email normalization

Use the same normalization behavior as auth/waitlist code. Do not implement a subtly different CLI-only normalization rule.

## Documentation required

- Keep `docs/admin-cli/README.md` as the operator-facing index.
- Keep command-specific pages such as `docs/admin-cli/waitlist.md` current with real command names, flags, dry-run behavior, write behavior, exit/failure expectations, and safety constraints.
- Do not document secrets or full production connection strings.

## Tests expected for mutating commands

- Dry-run leaves data unchanged.
- `--execute` writes expected status and audit fields.
- Mutation is atomic and rolls back on failure.
- No-match exits non-zero.
- Unexpected current status fails closed or follows an explicitly tested idempotency rule.
