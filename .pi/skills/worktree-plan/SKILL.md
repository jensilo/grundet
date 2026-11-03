---
name: worktree-plan
description: Use to plan a new feature in a dedicated git worktree.
---

# Worktree Plan

Use this skill when the user wants to start a new feature, idea, experiment, or larger change in a dedicated git worktree before implementation.

Keep the workflow lightweight: normal git worktrees, one plan file, one plan commit. Avoid hidden state, databases, or custom orchestration unless the user explicitly asks.

## Preconditions

1. Confirm the current directory is inside a git repository:
   ```bash
   git rev-parse --show-toplevel
   git status --short
   git branch --show-current
   git worktree list
   ```
2. If the repo has uncommitted changes in the current worktree, stop and ask before proceeding.
3. Read project instructions if present, for example:
   - `AGENTS.md`
   - `.pi/AGENTS.md`
   - `.agents/AGENTS.md`
   - relevant README/docs files

## Plan With User First

Before creating the worktree, discuss the idea enough to determine:

- Problem / goal
- Non-goals
- Constraints
- Likely files or systems touched
- Acceptance criteria
- Risks / open questions

Then propose and confirm:

- Feature slug: kebab-case, short but specific, e.g. `semantic-session-search`
- Branch name: default `feature/<slug>`
- Worktree path: default `.pi/worktrees/<slug>` under the current repo root (create directories if needed)
- Plan review: use an independent clean-context `smart:high` review pass before finalizing the plan

## Create Worktree

From the original repo root, create worktrees inside the repo-local `.pi/worktrees/` directory so Pi-specific project state stays under `.pi`:

```bash
mkdir -p .pi/worktrees
git worktree add .pi/worktrees/<slug> -b feature/<slug>
```

If the branch already exists, ask whether to reuse it:

```bash
mkdir -p .pi/worktrees
git worktree add .pi/worktrees/<slug> feature/<slug>
```

After creating it, perform all further file work inside the new worktree.

## Copy Untracked Pi Config

`git worktree add` only populates tracked files. If the source repo has a
`.pi/` check config that is untracked (kept local, not committed to spare
colleagues the setup), the new worktree won't have it and the pi `check` tool
will silently do nothing there. Copy it over, excluding the worktrees dir
itself:

```bash
# from the source repo root, after creating the worktree
if [ -e .pi/check.json ] && ! git ls-files --error-unmatch .pi/check.json >/dev/null 2>&1; then
  rsync -a --exclude 'worktrees' .pi/ .pi/worktrees/<slug>/.pi/
fi
```

Skip silently if there is no untracked `.pi/check.json` (nothing to copy, or it
is already tracked and thus already in the worktree).

## Write the Plan File

Create:

```text
docs/<slug>-plan.md
```

Create `docs/` first if needed.

The plan must include this notice near the top, exactly adapted to the slug:

```md
> Merge notice: Before merging this branch into main, move this plan file to `docs/archive/plans/<slug>-plan.md`.
```

Recommended structure:

```md
# <Human Feature Title> Plan

> Merge notice: Before merging this branch into main, move this plan file to `docs/archive/plans/<slug>-plan.md`.

## Summary

## Goals

## Non-goals

## Current Context

## Proposed Approach

## Implementation Steps

## Acceptance Criteria

## Risks and Open Questions

## Review Notes
```

## Review the Plan

Before finalizing the plan, prefer an independent clean-context `smart:high` review subagent and incorporate relevant feedback.

Give the review this context:

```text
You are reviewing a feature plan before implementation.

Repository/worktree: <absolute worktree path>
Plan file: docs/<slug>-plan.md

Read the plan and provide concise feedback on:
- unclear assumptions
- missing implementation steps
- risks and edge cases
- testing gaps
- simpler alternatives
- anything that should be decided before coding

Do not edit files. Return actionable review notes only.
```

After the review responds, triage its feedback yourself:

- accept useful feedback into the plan
- reject over-engineering or irrelevant suggestions
- add a short summary under `## Review Notes`

## Before Commit

Before committing, do lightweight repo-specific chores if they are clearly defined and applicable, for example:

- formatting/linting commands documented in the repo
- tests only if relevant and cheap for a plan-only change
- after-work skills explicitly applicable to the repo/task

If no chores are defined, do not invent any. A plan-only commit is enough.

Check:

```bash
git status --short
```

Commit only the plan file and any required directory placeholder if necessary:

```bash
git add docs/<slug>-plan.md
git commit -m "Add <slug> plan"
```

## Final Response

Report concisely:

- worktree path
- branch name
- plan file path
- review tier used (if delegated)
- commit hash if committed
- next suggested command/session prompt for continuing work
