# 007 — Input length limits for stored free-text fields

**Date:** 2026-06-16
**Status:** Accepted

## Context

User-authored free-text fields (project/feature names and descriptions, requirement text,
rationale, acceptance criteria, work-item titles and descriptions, change reasons) had no length
limit. The EF Core configuration left them as unbounded PostgreSQL `text`, and the services only
trimmed and checked for emptiness. Only a handful of unrelated fields (work-item `blockedReason`,
display name, feedback) were capped.

Unbounded text is a small but real liability: accidental paste or abuse can store arbitrarily large
values, and because every requirement/work-item mutation writes a new immutable version row, an
oversized field is copied forward on every edit. We wanted bounds that stop runaway input without
constraining anyone writing normal prose.

## Decision

Enforce generous per-field character limits, server-side, as the source of truth. Limits live in
`Grundet.Api.Common.Validation.TextInput` and are applied in the services via `TextInput.Required`
/ `TextInput.Optional`. Over-length input returns `400` with the canonical `validation` ProblemType
and an actionable detail (`"<Field> must be <N> characters or fewer."`).

| Field group | Limit |
|---|---|
| Names / titles (project, feature, work-item title) | 200 |
| Requirement intent text | 1000 |
| Descriptions & rationale (multi-line prose) | 4000 |
| Acceptance criterion (each) | 500 |
| Change reason (requirement/work-item), blocked reason | 500 |

The frontend mirrors these in `lib/text-limits.ts` as `maxLength` attributes on the relevant
inputs, so the limit is felt while typing and a rejection never arrives unexpectedly on submit. The
backend remains authoritative; the frontend values are a UX convenience, not a second contract.

These are application-level validation limits, not database column constraints. No migration is
added — the columns stay `text`. Limits can be raised later without a schema change; lowering them
would not retroactively invalidate existing rows.

## Alternatives Considered

- **Database `varchar(n)` / check constraints.** Rejected: harder to evolve (requires a migration to
  change), and produces a raw DB error rather than an actionable ProblemDetails. Application-level
  validation gives better error copy and keeps the contract chain intact.
- **Tighter limits (names 120, text 500, descriptions 2000).** Rejected: more likely to interrupt
  real users writing longer prose. The goal is an abuse/runaway backstop, not a style constraint.
- **Leaving fields unbounded.** Rejected: the version-row copy-forward makes oversized values
  compounding, and unbounded user text is an avoidable abuse surface.

## Consequences

- One place (`TextInput`) owns the numbers; services call it instead of duplicating trim/length
  logic. The per-service `NormalizeRequired`/`NormalizeOptional` helpers were removed where the
  shared helper replaced them.
- Adding a new free-text field is a deliberate choice: pick a limit from `TextInput` and validate
  through it.
- The frontend `maxLength` constants must stay in step with `TextInput`. They are documented as a
  mirror; if they drift, the backend still rejects over-length input correctly.
