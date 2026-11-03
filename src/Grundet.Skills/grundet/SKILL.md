---
name: grundet
description: Connect to a Grundet workspace over its MCP server and read versioned product intent (requirements). Use when working on, planning, estimating, or answering questions about a Grundet project — to set up the connection (API key + MCP), discover projects, and read the authoritative requirements, their acceptance criteria, rationale, and full version history before acting. Routes to setup, project discovery, and intent-reading guidance. Always use the Grundet MCP through this skill rather than calling its tools directly.
version: 0.0.0-dev
---

# Grundet

**Grundet is versioned product truth for humans and agents.** It holds a team's *intent* — the
requirements that say what should be true, why it matters, and how the team knows it is done — and
keeps every change as an immutable version, so the history is the changelog.

Use this skill whenever you are about to implement, estimate, review, or answer questions about a
Grundet project: read the intent first so you act on the team's actual requirements, not
assumptions. It connects you to a Grundet workspace over MCP and reads that intent.

## The model (in one breath)

**Team → Project → Feature → Requirement.** A **Requirement** is *Intent*, referenced as
**`IN-{n}`** (e.g. `IN-7`), scoped per project. Every edit creates a new immutable version — the
version history is the changelog. Full detail: [concepts.md](concepts.md).

## Hard guardrail — read the subskill first

**Only use the Grundet MCP through this skill.** Even if you believe you already know how to call
a Grundet MCP tool, read the relevant reference file below and follow it before invoking any tool.
The tools have non-obvious rules — team scoping, `IN-` reference resolution, and project ids that
must be resolved from names (project names are not unique) — and calling them blind produces wrong,
empty, or unauthorized results. Do not invent tool calls; use the flows documented here.

## Where to go

| Your task | Read |
|---|---|
| Get connected: mint an API key, configure the MCP client, resolve a project | [setup.md](setup.md) |
| Read requirements: list intent, get one by `IN-` reference, read its version history | [read-intent.md](read-intent.md) |
| Understand the terms: hierarchy, Intent vs Work, references, versioning | [concepts.md](concepts.md) |

Typical first run: **setup.md** (connect + pick a project) → **read-intent.md** (read it).

## Scope

Read-only, intent-only. The MCP exposes reading requirements and their history — there are no
write tools, so Grundet's append-only versioning is never touched through MCP. Work items (`WO-`),
links, and search are future increments.
