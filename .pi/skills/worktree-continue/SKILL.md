---
name: worktree-continue
description: Use to resume an existing feature worktree.
---

# Worktree Continue

Use this skill when the user wants to continue work on an existing git worktree/feature branch created for a specific plan.

Keep the workflow simple: enter the worktree, read context, summarize state, then either refine the plan or implement.

## Locate the Worktree

If the user provides a path, use it. Otherwise inspect repo-local worktrees first, then the full git worktree list:

```bash
find .pi/worktrees .agents/worktrees -mindepth 1 -maxdepth 1 -type d 2>/dev/null
git worktree list
```

Prefer worktrees under `.pi/worktrees/<slug>` when present. Treat `.agents/worktrees/<slug>` as a legacy location. If needed, ask which worktree/branch to continue.

Once selected, work from that worktree root.

## Read Context First

Before changing files, read relevant instructions and context:

1. Repo instructions, if present:
   - `AGENTS.md`
   - `.pi/AGENTS.md`
   - `.agents/AGENTS.md`
   - relevant README/docs files
2. Git state:
   ```bash
   git status --short
   git branch --show-current
   git log --oneline -5
   ```
3. Feature plan:
   - Prefer `docs/<slug>-plan.md` matching the branch/worktree name.
   - If multiple plan files exist, ask which one applies.
4. Related context/memory files if present and clearly relevant, for example:
   - `docs/*context*.md`
   - `docs/*memory*.md`
   - `.pi/*`
   - `.agents/*`

## Resume Workflow

After reading context, summarize briefly:

- branch/worktree
- plan file
- current status
- next likely step
- any unresolved questions

Then ask whether the user wants to:

1. refine the plan
2. implement the next step
3. review current changes
4. prepare for merge

Do not start broad implementation without user confirmation unless the user explicitly requested it.

## During Implementation

Follow the plan, but keep judgment:

- challenge stale or weak assumptions
- update the plan if the approach changes materially
- keep commits focused
- run repo-defined checks when appropriate

## Merge Preparation Reminder

If preparing for merge into main, ensure the plan file is moved to the archive path named in its merge notice, typically:

```text
docs/archive/plans/<slug>-plan.md
```

Then commit that move before merge if appropriate.

## Final Response

Report concisely:

- worktree path
- branch
- plan file read
- what was changed or decided
- tests/checks run
- next step
