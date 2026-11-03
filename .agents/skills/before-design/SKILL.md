---
name: before-design
description: Briefing skill for design and UI decisions. Instructs reading docs/design/ruleset.md before any design work. Covers critical caveats about the custom colour system. Run before making any design decisions, writing UI copy, or specifying component behaviour.
---

# Design Briefing — Grundet Foundation

Before any design work, UI copy, or component specification:

**Read `docs/design/ruleset.md` in full.** It is the authoritative reference for:
- Colour tokens and semantic token layer
- Typography scale (exact Tailwind classes for each step)
- Spacing, layout, radius, shadow rules
- Component specifications (Button, Card, Table, Dialog, Nav, etc.)
- Motion principles
- Brand and logo constraints
- Marketing vs. product UI rules
- Anti-patterns (what is explicitly forbidden)

---

## Critical caveats (memorise these)

**Custom colour system.** `indigo` in this project is deep purple (`indigo-500 = #610F7F`). This is NOT standard Tailwind indigo (`#6366f1`). All tokens in `globals.css @theme` fully override Tailwind defaults. Never assume Tailwind defaults — check the token.

**Two colour layers:**
1. Palette tokens — raw: `text-slate-900`, `bg-indigo-50`, `bg-success-50`
2. Semantic tokens — mapped: `bg-background`, `text-foreground`, `bg-card`, `bg-muted`, etc.

**Rule:** Semantic tokens in all component code. Palette tokens only for fixed-colour elements (diff lines, specific badge variants, brand-locked).

**Authenticated app rule.** Marketing accent colours (Cool Sky, Bubblegum, Emerald, Espresso) are forbidden inside `/app` (authenticated routes). Only Slate + Indigo (primary) in the product UI.

**Base UI, not Radix.** All interactive components build on `@base-ui/react/` primitives. Use `data-active`, `data-open`, etc. for state-driven styles — not `data-[state=...]` (Radix pattern).

**UI copy punctuation.** Use spaced en dashes only: `some – text in the – middle`.

---

## Aesthetic intent

**App UI (authenticated, `/app`):** Calm, functional, visually quiet. The product handles complex
information — the UI should not compete with it. Favour density, predictability, and
micro-refinements over creative expression. "Easy to use" outranks "memorable."

**Marketing (`components/marketing/`, `app/(marketing)/`):** This is where bold design lives.
Commit to one clear, intentional direction and execute it precisely. Generic AI aesthetics are a
bug: overused font pairings, safe colour distributions, predictable grid layouts, cookie-cutter
section structures. If a section could have come out of any AI design tool unchanged, it is not
done yet. One strong direction executed with precision beats ten hedged ones.

**Match execution depth to the vision.** Restrained, minimal design demands precision — every
spacing and type decision counts double. Elaborate compositions need proportionally elaborate
execution. "Minimal" is never an excuse for "quick."

---

After reading `docs/design/ruleset.md`, proceed with the design work.
