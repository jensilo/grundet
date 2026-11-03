---
name: after-work
description: End-of-session bookkeeping. Writes a session log, updates docs/work-state.md and docs/sessions/README.md, and updates docs/feature.md. Run at the end of every session. Replaces /conclude.
---

# After-Work — Session Bookkeeping

Use a background sub-agent for this work so it doesn't flood the conversation context.

The sub-agent should:

## 1. Write session log

Get the current timestamp:

```!
date '+%y-%m-%d_%H-%M'
```

Create `docs/sessions/<output>.md` using that exact value as the filename.

Format:
```markdown
# Session — YYYY-MM-DD

## Done
- [bullet list of what was completed]

## Decisions
- [any locked decisions made this session]

## Next
- [immediate next steps]

## Blockers
- [any blockers or open questions]
```

Keep it concise — 5–15 bullets total. No prose paragraphs.

## 2. Update docs/sessions/README.md

Add one line to the index: `- [YY-MM-DD_HH-MM.md](YY-MM-DD_HH-MM.md) — one-line summary`

## 3. Update docs/work-state.md

Update to reflect:
- Completed items from this session (remove from "What's Next", add note to status)
- New "What's Next" priority order
- Any new blockers or resolved blockers

## 4. Update docs/feature.md

The single feature inventory: a `Product Surface` status table, an `Endpoint Inventory` route table, and cross-cutting constraints. Update only rows that changed this session — flip a status, add a newly shipped area's row, or add new routes. Keep entries terse and factual; link to detail docs instead of duplicating narrative.
