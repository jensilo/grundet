---
name: format-lint-test
description: Post-implementation proof step. Runs format check, build, and tests for backend and/or frontend. All checks must pass before a task is complete. Run after every implementation.
---

# Format, Lint, Test — Proof Step

This skill is the single source of truth for running verification scripts. Other skills may reference this skill, but should not duplicate script commands.

Determine which layers were modified in this session:

- Backend changed (`src/Grundet.Api/**`): run `scripts/verify-backend.sh`
- Frontend changed (`src/Grundet.Web/**`): run `scripts/verify-frontend.sh`
- Both changed: run both scripts

Execute the script(s):

```!
cd "$(git rev-parse --show-toplevel)" && bash scripts/verify-backend.sh
```

```!
cd "$(git rev-parse --show-toplevel)" && bash scripts/verify-frontend.sh
```

Report the results. If any check fails, fix the issue and re-run before declaring the task done. Do not report success until all checks pass with no errors.
