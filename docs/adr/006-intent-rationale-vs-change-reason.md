# 006 — Intent rationale vs. change reason; moving intent preserves history

**Date:** 2026-06-13
**Status:** Accepted

## Context

Two related issues surfaced while reviewing the Intent & Work composer in a real browser.

**1. One `Reason` field serving two different concepts.** Each `Requirement` version row carries
a single nullable `Reason` column, alongside `ChangedAt`, `ChangedByUserId`, and `ChangeType`.
Structurally it is per-version change metadata — a changelog/commit comment. But the composer
collected it at creation labelled simply "Reason", and the detail view rendered it as if it were
the intent's headline justification. This conflates two genuinely different things:

- **Why this intent matters** — durable product truth. Grundet's doctrine defines intent as
  "what should be true, *why it matters*, and how the team knows it is done." The "why it matters"
  is part of what the requirement *is*; it belongs to the intent and should be readable by humans
  and agents as product context.
- **Why this version changed** — process metadata. "Tightened the phrasing after review,"
  "corrected a typo." This is a changelog comment, meaningful only when a change happens, and
  meaningless at creation (the first version's reason is just "Created").

Collapsing both into one field invites users to write durable rationale into what is actually a
per-edit comment, and vice versa. The two are not identical in most cases, and the UI actively
encouraged the confusion.

**2. Intent sometimes belongs under a different feature.** Requirements are feature-scoped
(`Requirement.FeatureId`), but the update path pinned `FeatureId = latest.FeatureId` — there was
no way to re-home an intent. The naive fix ("copy the intent under the new feature, delete the
old one") conflicts with the platform's core invariant that version history is append-only and
immutable: copy+remove forks a new logical id with a fresh changelog and orphans the original
history.

## Decision

### Rationale and change-reason are separate fields

- **Keep `Reason` on the version row**, defined strictly as **"reason for this change"** — a
  per-version changelog comment. It is collected and shown **only in edit mode**, never at
  creation. It is process metadata and is **not** part of the search vector.
- **Add a first-class durable `Rationale` field to `Requirement`** ("why this intent matters",
  framed in the UI as "Because…"). It is product truth: shown prominently on create and in the
  detail view, part of the agent-readable context, and **included in the requirement search
  vector**. It versions like any other field — editing it produces a new version row, and the
  changelog diffs it distinctly from `Reason`.
- `Rationale` is **optional but prominent**. It is not required to create an intent (we do not
  force academic justification on fast capture — see the product doctrine on not forcing atomic
  rigour), but it is surfaced prominently rather than hidden behind progressive disclosure, to
  encourage capturing product truth.
- The domain/column/DTO name is `Rationale` (standard requirements-engineering term). UI copy may
  render it as "Because…" / "Why this matters"; the model name stays `Rationale`.
- **The detail view distinguishes them:** `Rationale` is presented as a primary product-truth
  field near the intent text; `Reason` appears only in the version/changelog context as the
  reason a given version changed — never as headline content.

### Moving an intent to another feature preserves history

- Moving an intent to a different feature is an ordinary **field change on a new version row**:
  same logical `RequirementId`, new version row with the new `FeatureId`, `ChangeType=Updated`,
  all other fields copied per the versioning invariant. History stays continuous and the move is
  visible in the changelog diff.
- The move target is restricted to **another feature within the same project**. The target
  `FeatureId` is authorized at the endpoint (Contributor) and asserted to belong to the same
  project; it is treated as cross-tenant attack surface and never trusted from the request body.
- **Cross-project move is out of scope** (deferred): it would change the per-project
  `SequenceNumber` and the `IN-`/`WO-` reference, and disturb existing links — a materially
  larger decision.
- The feature picker lives in the **edit** view only. (Work items are project-scoped, not
  feature-scoped, and relate to intent via `WorkItemLink`; "move work to a feature" does not map
  to the model and is not part of this decision.)

## Alternatives Considered

**Keep a single `Reason` field, fix only the copy.** Relabel and explain it better in the UI.
Rejected: the ambiguity is structural, not a wording problem. One field cannot be both durable
product truth (searchable, agent-facing, part of the intent) and ephemeral per-version process
metadata without one role corrupting the other.

**Drop the create-time reason entirely and let the intent text carry the "why" (no new field).**
The leanest option, zero migration. Rejected as the primary direction: it leaves "why it matters"
with no structured home, which is exactly the product truth Grundet exists to preserve and make
agent-readable. (It remains a fallback if the `Rationale` field proves unused.)

**Require `Rationale` on every intent.** Strongest product-truth stance. Rejected: it adds
friction to the fast-capture flow the composer was built for and contradicts the doctrine's
"do not force academic atomicity." Optional-but-prominent captures the intent without the tax.

**Move via copy + remove.** Create the intent anew under the target feature and delete the
original. Rejected: it forks the logical id, starts a fresh changelog, and orphans the original
history — directly contradicting the append-only versioning thesis, which is the product's core
value.

## Consequences

- New nullable `Rationale` column on the requirements (version) table; migration required. The
  `CurrentRequirements` view, create/update/response DTOs, field diff, and (for `Rationale`) the
  search vector must all include it. `Reason` is removed from the create flow.
- Create and update requirement DTOs change → OpenAPI changes → the generated TypeScript client
  must be regenerated → composer and detail view updated. The composer now shows `Rationale`
  prominently (create + edit) and `Reason` only in edit; the detail view presents them distinctly.
- The requirement update endpoint gains an authorized, optional target `FeatureId`; the service
  writes the new feature onto the version row when it differs, within-project only.
- Versioning, tenant-isolation, and three-layer/contract-chain invariants are preserved
  throughout: move and rationale edits are ordinary versioned field changes; target feature and
  assignee ids are authorized at the endpoint.
- Future work, if validated: cross-project move (sequence/reference/link handling) as a separate
  decision; possibly surfacing `Rationale` in agent/MCP context payloads explicitly.
</content>
