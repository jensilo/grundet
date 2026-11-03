# Development database seeding

Grundet uses small SQL scripts to prime local development databases for smoke testing.

This is intentionally not a framework. Schema changes still belong in EF migrations. Seed scripts are dev/test data fixtures only.

## Base seed

`make dev` and `make dev-fresh` always run:

```bash
scripts/seed-dev.sql
```

Today this approves alpha access for `alex@grundet.test`, so local magic-link login can create a local alpha workspace. Override with `DEV_EMAIL` and `DEV_NAME` when needed:

```bash
make seed-dev DEV_EMAIL=you@example.com DEV_NAME="Your Name"
```

## Scenario scripts

Optional scenario scripts live under:

```text
scripts/db/scenarios/
```

Run one or more scenarios with `SCENARIOS`:

```bash
make dev-fresh SCENARIOS="billing/no-usage-snapshot billing/near-requirement-limit"
```

or apply scenarios to an already running local DB:

```bash
make db-scenarios SCENARIOS="billing/enforcement-active"
```

A scenario name maps directly to a SQL file:

```text
SCENARIOS="billing/near-requirement-limit"
→ scripts/db/scenarios/billing/near-requirement-limit.sql
```

## Rules

- Dev/test only. Never use these scripts for production data changes.
- Keep scripts explicit and boring.
- Prefer idempotent scripts where practical.
- Layer scenarios on top of the base seed instead of making each scenario recreate everything.
- Do not hide business logic in SQL fixtures. If a scenario needs application invariants, document what it assumes.
- Do not create a custom runner or fixture framework unless the simple Make target becomes genuinely insufficient.

## Billing/quota scenario

Run the bundled billing smoke scenario:

```bash
make dev-fresh SCENARIOS="billing/usage-states"
```

It creates these local users and workspaces:

| Email | State |
|---|---|
| `quota-no-snapshot@grundet.test` | No usage snapshot yet |
| `quota-near-limit@grundet.test` | Requirement/work-item usage approaching limit |
| `quota-grace@grundet.test` | Requirement/work-item usage over limit, inside grace |
| `quota-enforced@grundet.test` | Requirement/work-item enforcement active |
| `quota-projects-full@grundet.test` | Project hard limit reached |

Request a magic link for the email matching the state you want to inspect.

Requirement/work-item states are seeded by writing `TeamUsageSnapshots` directly. This is deliberate: those limits are enforced from cached usage snapshots, and inserting thousands of requirements/work items would make local smoke tests slow and noisy. Project hard-limit testing uses real projects because project limits are synchronous gates.

## Changelog attribution scenario

Layers recent changes onto the school-board demo to visually check how the changelog
attributes authors. Run it *after* `demo/school-board`:

```bash
make db-scenarios SCENARIOS="demo/school-board demo/changelog-attribution"
```

It inserts recent version rows that sort to the top of the project timeline, one per
author state:

| Author | State | Renders as |
|---|---|---|
| Robin Lane (`robin@grundet.test`) | In `Users`, no `TeamMembers` row | name + "former member" marker |
| Deleted account | No `Users` row at all | "Unknown member", no marker |
| Sam Rivera | Current team member | name, no marker |

The unknown-member row is structurally impossible in app code (the `ChangedByUserId`
FK is `DeleteBehavior.Restrict`); the scenario forces it by temporarily disabling
triggers. Dev fixtures only.

Future useful scenarios:

- many members or pending invites

Keep each scenario focused enough that smoke-test intent is obvious.
