# Admin CLI

Operational notes for `grundet-admin`, the direct-DB admin command-line tool.

The CLI is intentionally narrow: founder/operator tasks that are not worth a product admin UI yet, but still need explicit safety, audit output, and repeatable commands.

## Two faces, one core

`grundet-admin` has two front-ends over a single command core — same SQL, same safety rules:

- **Non-interactive (default for agents/scripts):** any subcommand runs the flag tool with its exact contract, output, and exit codes — e.g. `grundet-admin waitlist approve person@example.com --operator operator --execute`. This is unchanged and is what agents and CI must use.
- **Interactive TUI:** running `grundet-admin` with **no arguments** in an interactive terminal launches a Spectre.Console menu — pick a namespace, pick an action, fill inputs, read results in tables/panels. Destructive actions run a dry-run preview, then gate on an explicit `y`/`Y` (the interactive equivalent of dry-run + `--execute`); Enter, EOF, or anything else aborts with no write. Menus wrap around (cycle past the ends), a `← Back` entry returns to the previous menu, and an empty text prompt cancels the action.

Both faces accept a full UUID **or a git-style unique hex prefix** wherever a feedback id is taken (`feedback show`/`export`/`mark-read`/`delete`); an ambiguous prefix fails closed. Waitlist commands are keyed by email, so prefixes don't apply there.

### The TUI never traps a non-interactive caller

The menu launches only when **all** hold: no subcommand args, an interactive TTY (stdin and stdout not redirected), and no opt-out. Every other invocation routes to the flag path. Hard bypasses:

- pass any subcommand (selects the flag path directly);
- run in a pipe / redirected / no-TTY context (CI, agent shells) — prints usage and exits non-zero, never hangs;
- set `GRUNDET_ADMIN_NO_TUI=1`, or pass `--no-tui` as the sole arg;
- `--help`/`-h` prints usage and exits `0`.

## Current command areas

- [Waitlist](waitlist.md) — list, inspect, approve, and decline private-alpha waitlist entries.
- [Feedback](feedback.md) — list, inspect, export, mark read, and soft-delete app feedback submissions.

## Operating in production

- [Deployment & operation](deployment.md) — how the CLI is delivered (bundled into the API image) and run server-side via `docker exec`, the env contract, and export retrieval.

## Safety baseline

- Read-only commands may run directly.
- Mutating commands are dry-run by default and require `--execute` to write.
- Mutating commands require an operator identity for audit fields.
- Mutating commands print the target database host/database, never secrets.
- Mutating commands fail closed on missing, ambiguous, or unexpected state.
- The CLI must not directly mutate versioned product/work data (`Requirement`, `WorkItem`, `WorkItemLink`, or their version rows).
