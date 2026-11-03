# Check extension

Inactive, copy-ready project extension that adds `/check` and the `run_check` tool. It is not loaded globally.

Install it in a trusted project:

```bash
mkdir -p .pi/extensions
cp -R ~/.pi/shelf/extensions/check .pi/extensions/check
```

Reload Pi after copying. The extension is then available only in that project.

## Quick setup

1. Copy this directory into `.pi/extensions/check`.
2. Create `.pi/check.json` that maps check names to commands.
3. Put non-trivial commands in executable scripts under `.pi/checks/`.

After the project is trusted and Pi reloads, `/check` and `run_check` discover `.pi/check.json` automatically. Tell a coding agent to install this extension and add the project checks; the schema and script pattern below are the setup it needs.

This extension is intended for both humans and coding agents. Agents should use `run_check` after making changes, before final handoff, whenever a repo provides `.pi/check.json`. It is the project-local verification entry point for formatting, linting, tests, or other checks the repo owner wants agents to run.

Project config lives at:

```text
.pi/check.json
```

Schema:

```json
{
  "default": ["frontend", "backend"],
  "checks": {
    "frontend": ".pi/checks/frontend.sh",
    "backend": ".pi/checks/backend.sh"
  }
}
```

- `checks` maps check names to shell commands.
- A check value may be a string or an array of commands.
- `default` is optional.
  - If omitted, `/check` or `run_check` with no name runs all named checks.
  - If set to a string or array, only those checks/commands run by default.
- `/check frontend` runs one named check.
- Agents should call `run_check` with no name for the default checks, or with `{ "name": "frontend" }` for a named check.

Prefer putting non-trivial logic in scripts under `.pi/checks/`.
Keep checks local, predictable, and safe.

For agent workflows, checks should usually target only files changed by the user or agent. In git repos, formatting can often run in write mode on changed files only; this reduces formatting loops without reformatting the whole codebase. Linting and non-format rules should report issues for the agent to fix.

## Output rendering: collapsed vs expanded

`/check` renders two views of the same run:

- **Collapsed** (default): one tight block — each step's header plus its summary line(s).
- **Expanded** (`ctrl+o`, toggles all tool/check output): the full, line-by-line output of every step, grouped under the same headers.

The view is built from two kinds of lines your script prints:

- **Header lines** — any line starting with `$ ` or `> `. These appear in _both_ views (dimmed) and act as the group header for everything that follows. Print one before each step, e.g. `$ prettier --write …` or `> typecheck`.
- **Summary lines** — lines of the form `[/sum:]<text>`. The `<text>` is lifted into the collapsed view (colored green on success, red on failure) and slotted directly under the header it followed. The marker line itself is stripped from the expanded output, so it never shows up twice.

Everything else your script prints (file lists, tool noise) shows up only in the expanded view, under its header.

So a script that prints:

```text
$ prettier --write "src/**/*.ts"
src/a.ts 8ms
src/b.ts 3ms
[/sum:]prettier: 2 changed, 30 unchanged
$ eslint "src/**/*.ts"
[/sum:]eslint: passed
```

renders collapsed as:

```text
$ prettier --write "src/**/*.ts"
prettier: 2 changed, 30 unchanged
$ eslint "src/**/*.ts"
eslint: passed
```

and expanded (`ctrl+o`) as the full block above with the `[/sum:]` lines removed.

### How to write a check script

For each step: print a `$ <command>` (or `> <label>`) header, run the step letting its real output flow to stdout/stderr, then print one `[/sum:]<summary>` line. Keep summaries short and stable — one line per step is plenty; resist making the collapsed view verbose.

```bash
echo '$ prettier --write "src/**/*.ts"'
npm exec -- prettier --write "src/**/*.ts"
echo "[/sum:]prettier: done"
```

Notes:

- `[/sum:]` is reserved for `/check` UI metadata — don't emit it as normal tool output.
- Older scripts that print a `prettier: …` line (with no `[/sum:]` anywhere) still work via a fallback, but new scripts should use `[/sum:]`.
- `/check` already prepends a `$ <configured command>` header for each entry in `.pi/check.json`, so the headers your script prints nest under it.

## Example only: .NET backend + Node frontend

This is only a starting point. Copy and adapt; do not apply blindly. First inspect the repo's existing config and commands: `package.json`, lockfiles, `.editorconfig`, `*.sln`, `*.csproj`, and formatter/linter config.

`.pi/check.json`:

```json
{
  "default": ["backend", "frontend"],
  "checks": {
    "backend": ".pi/checks/backend.sh",
    "frontend": ".pi/checks/frontend.sh"
  }
}
```

Shared changed-file helper:

```bash
#!/usr/bin/env bash
set -euo pipefail
{
  git diff --name-only --diff-filter=ACMR HEAD --
  git ls-files --others --exclude-standard
} | sort -u
```

Backend example, if the repo already uses `dotnet format`:

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

files_file="$(mktemp)"
trap 'rm -f "$files_file"' EXIT
.pi/checks/changed-files.sh | awk '/\.cs$/' > "$files_file"
[[ ! -s "$files_file" ]] && { echo "[/sum:]no changed C# files"; exit 0; }

file_count="$(wc -l < "$files_file" | tr -d ' ')"
# Adapt project/solution selection to the repo. Keep --include limited to changed files.
xargs dotnet format path/to/Backend.sln --include < "$files_file"
echo "[/sum:]checked $file_count changed C# file(s)"
```

Frontend example, if the repo already uses a Node formatter/linter such as Biome:

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

files_file="$(mktemp)"
trap 'rm -f "$files_file"' EXIT
.pi/checks/changed-files.sh \
  | awk '/^path\/to\/frontend\// && /\.(js|jsx|ts|tsx|css|json)$/' \
  | sed 's#^path/to/frontend/##' > "$files_file"
[[ ! -s "$files_file" ]] && { echo "[/sum:]no changed frontend files"; exit 0; }

file_count="$(wc -l < "$files_file" | tr -d ' ')"
cd path/to/frontend # adapt this path
# Adapt npm to pnpm/yarn/bun and use the repo's existing scripts/config.
xargs npm exec -- biome check --write < "$files_file"
echo "[/sum:]checked $file_count changed frontend file(s)"
```

Prefer existing repo commands over inventing new tooling. Write-mode formatting should stay limited to changed/touched files.
