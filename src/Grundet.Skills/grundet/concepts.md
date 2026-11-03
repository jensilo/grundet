# Grundet concepts

The mental model behind everything the Grundet MCP returns. Read this when a term
(`IN-`, "intent", "version", "feature") is unclear, or before interpreting a tool result.

## Contents
- The hierarchy
- Intent vs Work
- References (`IN-` / `WO-`)
- Immutable versioning (why history is the changelog)

## The hierarchy

Grundet is **versioned product truth for humans and agents**. Its structure is:

**Team → Project → Feature → Requirement**

- A **Team** owns everything and is the unit an API key is scoped to.
- A **Project** groups the work for one product or initiative.
- A **Feature** groups related requirements within a project.
- A **Requirement** is the smallest unit of product truth.

## Intent vs Work

Grundet separates *what should be true* from *who is making it true*:

- **Intent** = a **Requirement**. Product truth: the desired outcome, why it matters
  (`rationale`), and how the team knows it is done (`acceptanceCriteria`). Referenced as
  **`IN-{n}`** (e.g. `IN-7`).
- **Work** = a **WorkItem**: execution ownership — who is making some slice of intent true.
  Referenced as **`WO-{n}`** (e.g. `WO-12`).

The MCP exposes **Intent only** today. Work items, links, and search are future increments.

## References (`IN-` / `WO-`)

References are **per project** — `IN-7` in one project is unrelated to `IN-7` in another.
Requirements and work items share one per-project sequence counter, so a bare number (`7` or
`#7`) resolves to whichever entity owns it. Every read is scoped to one project, so you always
pass a project id alongside a reference.

## Immutable versioning

Every change to a requirement inserts a **new immutable version row** — Grundet never edits in
place. The version history **is** the changelog. This is Grundet's differentiator: reading an
item's history shows exactly how its wording, acceptance criteria, priority, and status evolved,
and — through each version's `reason` — *why*. When intent is ambiguous, the history is where the
answer lives.
