# Reading Grundet intent

Read this once you are connected (see [setup.md](setup.md)) and want to read a project's
requirements. Tool names below assume your MCP server is named `grundet`; if you named it
something else in your client config, substitute that name (e.g. `myserver:list_intent`).

## Contents
- Before you start
- Resolve the project
- The three tools
- What each item contains
- Using it well

## Before you start

Every intent read is scoped to one **project** and needs that project's **GUID**. Never ask the
user for a raw GUID — resolve it from a name with `grundet:list_projects` first (see
[setup.md](setup.md) → "Pick a project"). Keep the resolved id to hand for the calls below.

If a term here is unclear (`IN-`, "version", "intent"), read [concepts.md](concepts.md).

## The three tools

All three read only within the API key's team. A project in another team — even one you also
belong to — returns not-found.

### `grundet:list_intent`
Current state of each requirement in a project, newest activity first. Start here to map a
project (or one feature).
- `projectId` (GUID, required)
- `featureId` (GUID, optional) — narrow to one feature
- `page` (default 1), `pageSize` (default 50, max 100)

### `grundet:get_intent`
A single current requirement by reference.
- `projectId` (GUID, required)
- `reference` (string, required) — e.g. `IN-7` (bare `7` or `#7` also resolve)

### `grundet:get_intent_history`
The full version history of one requirement, **oldest to newest**. Each entry is the complete
requirement state at that version — this is the changelog, and Grundet's differentiator.
- `projectId` (GUID, required)
- `reference` (string, required) — e.g. `IN-7`

## What each item contains

Every returned item is one requirement version:

`ref` (`IN-{n}`), `requirementId`, `featureId`, `sequenceNumber`, `text` (the requirement
statement), `status`, `priority`, `labels[]`, `acceptanceCriteria[]`, `rationale` (why it
matters), `reason` (why *this version* changed), `changedAt`, `changeType`.

## Using it well

- **Start broad, then drill in.** `list_intent` to map a project or feature, then `get_intent`
  on the specific `IN-` you care about.
- **Use history for the "why".** When wording is ambiguous or you must respect a past decision,
  `get_intent_history` shows how the requirement reached its current form — read `reason` and
  `acceptanceCriteria` across versions.
- **Trust intent over inference.** If the code and the intent disagree, the intent is the product
  truth to reconcile against — surface the discrepancy rather than silently following the code.
- **Quote references.** When you report back, cite `IN-{n}` so humans can find the item in
  Grundet.
- **Read-only.** There are no write tools; Grundet's append-only versioning is never touched
  through MCP.
