# Grundet Foundation — Design Ruleset

> Implementation-ready rules derived from `docs/design/FOUNDATION.md`.
> Read this file before writing any frontend code. Where this document conflicts with existing
> code, flag the drift — do not propagate it.

---

## Critical Caveats

**Custom colour system.** `indigo` in this project is a deep purple (`indigo-500 = rgb(97 15 127)`,
`#610F7F`). It is NOT standard Tailwind indigo (`#6366f1`). All colour tokens in
`globals.css @theme` fully override Tailwind defaults. Never assume Tailwind defaults; check the
token definitions.

**Semantic token layer.** The codebase has two colour layers:
1. **Palette tokens** — raw values: `text-slate-900`, `bg-indigo-50`, `bg-success-50`, etc.
2. **Semantic tokens** — context-mapped aliases: `bg-background`, `text-foreground`, `bg-card`,
   `bg-muted`, `text-muted-foreground`, `border-border`, `bg-primary`, `text-primary-foreground`,
   `ring-ring`, `bg-accent`, `text-accent-foreground`, `bg-popover`.

**Rule:** Use semantic tokens in all component code. Use palette tokens only for fixed-colour
overrides that must never change with theme (diff lines, specific badge variants, brand-locked
elements).

**Intentional exceptions.** These deviate from the general rule but are deliberate design choices — do not "fix" them.

- `app-topbar.tsx` uses `bg-white` — creates a crisp white nav strip that anchors the header and visually separates it from the slate/lavender page background
- Hero headline uses an indigo→bubblegum gradient (marketing exception, not a general pattern)

---

## Color

- Palette spine: Slate (cool-tinted neutral). Indigo (brand primary, deep purple). Marketing
  accents (Cool Sky, Bubblegum, Emerald, Espresso) — product UI only; never in the authenticated app.
- Text: `text-foreground` (slate-900) for body. `text-slate-950` for display headings/hero.
  Secondary: `text-muted-foreground` (slate-500). Dim labels: `text-slate-600`.
  Placeholder: `text-slate-400`.
- Backgrounds: `bg-background` (slate-50) for page/surface. `bg-muted` (slate-100) for elevated
  surfaces, table headers, sidebar sections. Never use `bg-white` or raw `#FFFFFF`/`#000000`.
- Borders: `border-border` (slate-200) as default. Strong/selected: `border-slate-700`.
  Focus state uses the `ring` token via `ring-ring`.
- Indigo (primary): primary buttons, active nav left-border + `bg-accent`, focus rings, required
  asterisks, selected table rows. Never as a section wash, gradient, or decorative fill.
- Functional colours: Success `bg-success-50 text-success-700` for saves/completed.
  Warning `bg-warning-50 text-warning-700` for Won't Do/deprecated.
  Destructive `bg-destructive-50 text-destructive-700` for delete/error.
- Diff view: Added `bg-diff-added-bg text-diff-added-text`. Removed `bg-diff-removed-bg
  text-diff-removed-text line-through`. Gutter `bg-diff-gutter-bg`. Always use these dedicated
  diff tokens — never hardcode hex values in diff components.
- Contrast: Body text 4.5:1 minimum (AA). Larger text 3:1 minimum.
- **Decorative identicon exception.** Seeded user avatars (boring-avatars `beam`) may draw from a
  broader, vibrant generative palette (`lib/avatar-palette.ts` → `AVATAR_PALETTE`) so different users
  get visually distinct identicons. This is the ONLY sanctioned exception to the marketing-accent /
  authenticated-app hue ban, and it is scoped to decorative identicons only. It does NOT relax the
  hue ban for any other product UI — all other authenticated surfaces remain Slate + Indigo.

---

## Typography

- Typefaces: Inter (UI/body, loaded via `next/font` → `var(--font-inter)`). DM Serif Display
  (marketing hero H1 only, never in product app). Geist Mono (requirement IDs, code, technical
  metadata, loaded via `next/font`).
- Do NOT import fonts via CSS `@import` or `<link>`. Font variables are injected on `<html>` in
  `app/layout.tsx`.
- Type scale — these are label names for the scale, not CSS utility classes. Apply the listed
  Tailwind classes directly:

  | Name | Size | Weight | Tracking | Tailwind |
  |---|---|---|---|---|
  | display-2xl | 80px | 600 | -0.03em | `text-[5rem] leading-[1.0] font-semibold tracking-[-0.03em]` |
  | display-xl  | 64px | 600 | -0.02em | `text-[4rem] leading-[1.05] font-semibold tracking-[-0.02em]` |
  | display-lg  | 48px | 600 | -0.02em | `text-5xl leading-[1.1] font-semibold tracking-[-0.02em]` |
  | display-md  | 40px | 600 | -0.01em | `text-[2.5rem] leading-[1.15] font-semibold tracking-[-0.01em]` |
  | heading-xl  | 32px | 600 | -0.01em | `text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]` |
  | heading-lg  | 24px | 600 | -0.01em | `text-2xl leading-[1.25] font-semibold tracking-[-0.01em]` |
  | heading-md  | 20px | 600 | 0       | `text-xl leading-[1.3] font-semibold` |
  | heading-sm  | 16px | 600 | 0       | `text-base leading-[1.4] font-semibold` |
  | body-lg     | 20px | 400 | 0       | `text-xl leading-relaxed` (marketing only) |
  | body-md     | 17px | 400 | 0       | `text-[1.0625rem] leading-relaxed` (product UI standard) |
  | body-sm     | 15px | 400 | 0       | `text-[0.9375rem] leading-relaxed` |
  | body-xs     | 14px | 400 | 0       | `text-sm leading-relaxed` |
  | label-md    | 14px | 500 | 0       | `text-sm font-medium` |
  | label-sm    | 12px | 500 | +0.04em | `text-xs font-medium tracking-[0.04em] uppercase` (eyebrows, table headers only) |
  | code-md     | 14px | 400 | 0       | `font-mono text-sm` |
  | code-sm     | 13px | 400 | 0       | `font-mono text-[0.8125rem]` |

- `label-sm` (all-caps) is reserved for eyebrow text and table column headers only. Never set
  other UI labels in all-caps.
- Weight 600 is the maximum for headings. Never use 700+. If you're reaching for bold, fix
  hierarchy via size or colour contrast instead.
- Italic is reserved for the diff/changelog reason field and foreign terms. Never italicise UI
  labels.
- Line length (product UI): 80–100 characters. (Marketing): 60–75 characters.
- Monospace (`font-mono`): requirement IDs, version numbers, code, diff content.

---

## Spacing & Layout

- Base unit: 4px. Primary grid: 8px. Use Tailwind's default scale directly.
  Avoid off-grid values (`p-5`, `p-7`, `p-10`) — they break layout rhythm.
- Component padding: inputs/controls `px-3 py-2`. Cards use slot-based padding (see Card component).
  Section internal padding `p-8`. Section-to-section: compact `py-16`, default `py-24`, hero `py-32`.
- Max-width containers:
  - Marketing page: base `mx-auto max-w-6xl px-6`, widening on large displays to
    `2xl:max-w-[88rem] 2xl:px-12` then `3xl:max-w-[96rem] 3xl:px-16` (custom `3xl` = 120rem / 1920px
    breakpoint, defined in `globals.css @theme`). The base (`≤1280px`) is unchanged; only the
    `2xl`/`3xl` tiers widen the shell so the page reads deliberately on presentation displays
    instead of stranding a narrow column with large dead margins. The canonical source of these
    tiers is the `MARKETING_CONTAINER` class constant in `components/marketing/layout.ts` — compose
    sections with `cn(MARKETING_CONTAINER, "…")`; do not re-hand-roll the responsive widths per
    section.
  - Product UI content: `mx-auto max-w-[1080px] px-6`
  - Long-form reading: `mx-auto max-w-[720px] px-6`
- Gaps: icon to text `gap-2`. Component internal `gap-3` or `gap-4`. Layout-level `gap-6`,
  `gap-8`, `gap-12`. Do not mix `gap` and `margin` on the same axis in the same layout context.

---

## Radius

- System default: `rounded-md` (6px / `--radius-md`). Baseline for buttons and most inputs.
- Small: `rounded-sm` (4px) for badges, tags.
- Large: `rounded-lg` (8px) for dialogs, larger popovers.
- Extra large: `rounded-xl` (12px) for cards (this is the Card component default), marketing
  preview frames.
- Avatars: `rounded-full` only.
- Never use arbitrary pixel radii outside these steps. `rounded-2xl`+ is reserved for marketing
  preview frames only.

---

## Shadow

Shadows are cool-tinted (`rgb(14 17 23 / α)`, never pure black).

- Card hover lift: `shadow-sm`
- Dropdowns/popovers: `shadow-md`
- Modals/dialogs: `shadow-lg`
- Marketing preview frames: `shadow-xl`
- Cards use `ring-1 ring-foreground/10` not a shadow (matches Card component implementation).
- Focus ring: implemented via `ring-ring` semantic token (`focus-visible:ring-3 focus-visible:ring-ring/50`
  matches Button implementation).

---

## Motion

- Motion exists only to explain state change. If it doesn't communicate something, cut it.
- Use CSS transitions, not JavaScript animation libraries. Framer Motion is not installed.
- Timing: micro-interactions 100ms. Standard state change 200ms. Surface transitions 300ms max.
- Easing: `ease-out` or `cubic-bezier(0.2, 0, 0, 1)`. No springs or overshoot.
- Entrance pattern: `opacity-0 translate-y-3` → `opacity-100 translate-y-0` over 400ms ease-out.
- Modal entrance: `opacity-0 scale-95` → `opacity-100 scale-100` over 200ms ease-out.
- Diff reveal: old text `line-through` transition 200ms linear. New text `opacity-0` →
  `opacity-100` over 300ms ease-out, 100ms delay.
- Marketing hover lift: `hover:-translate-y-1 transition-transform duration-300`.
- Toast: slide from `translate-x-6` to 0 over 200ms ease-out. Dismiss 150ms ease-in fade.
- Always wrap animations in `@media (prefers-reduced-motion: reduce) { ... }` or use the
  `motion-safe:` Tailwind variant. All animations must stop for reduced-motion users.
- Forbidden: bouncy springs, auto-advancing carousels, parallax >8px, element rotations,
  decorative animation loops.

---

## Iconography

- System: Lucide React. Default size 16px (UI), 20px (touch targets), 24px (nav items).
- Stroke width: 1.75 (Lucide default). Colour: `currentColor` (inherits text colour).
- Icon + text: `gap-2` (8px). Icon always precedes text.
- Standalone icons (no visible label): `aria-label` required.
- Custom icons: use `<StrataMark>` from `components/brand/strata-mark.tsx`. No new custom SVGs
  without explicit discussion.

---

## Component Specifications

All interactive components are built on `@base-ui/react/` primitives. When building new interactive
components, check whether a Base UI primitive covers the interaction. Do not use Radix UI.

Base UI emits `data-active`, `data-open`, `data-closed`, `data-checked`, etc. on its components.
Use these data attributes for state-driven styles, not `data-[state=...]` (Radix pattern).
`aria-invalid` is automatically managed by Base UI for form inputs.

### Button (`components/ui/button.tsx`)
- Variants: `default` (primary/indigo), `outline`, `secondary`, `ghost`, `destructive`, `link`.
  Use `variant="default"` for primary actions, not `variant="primary"`.
- Sizes: `default` (h-8), `sm` (h-7), `lg` (h-9), `xs` (h-6), `icon` (size-8),
  `icon-sm` (size-7), `icon-lg` (size-9), `icon-xs` (size-6).
- Base classes (from implementation): `inline-flex shrink-0 items-center justify-center rounded-md
  border border-transparent bg-clip-padding text-sm font-medium whitespace-nowrap transition-all`.
- Focus: `focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50`.
- Default state: `bg-primary text-primary-foreground` with hover via `primary-hover` token.
- Do not reproduce the CVA classes manually. Use `<Button variant="..." size="...">`.

### Input / Textarea (`components/ui/input.tsx`, `components/ui/textarea.tsx`)
- Base: `h-8 w-full rounded-lg border border-input bg-transparent px-3 py-1.5 text-sm`.
- Focus: `focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50`.
- Error state: `aria-invalid` automatically triggers red border+ring via Base UI.
- Placeholder: `placeholder:text-slate-400`.
- Required asterisk: `<span className="text-primary ml-0.5">*</span>` after label text.

### Card (`components/ui/card.tsx`)
- Slots: `<Card>` → `<CardHeader>` → `<CardTitle>` + `<CardDescription>` + `<CardAction>`.
  `<CardContent>`. `<CardFooter>`.
- Card: `rounded-xl bg-card ring-1 ring-foreground/10 py-4`. No shadow on cards — they use a ring.
- CardHeader: `px-4 gap-1`. CardContent: `px-4`. CardFooter: `bg-muted/50 border-t p-4`.
- Size prop: `size="sm"` for compact variants (`py-3 px-3`).
- Do not apply shadow, border, or background classes directly on `<Card>` — they are built in.

### Badge (`components/ui/badge.tsx`)
- Base class: `inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium tracking-[0.02em]`.
- Status variants (use `components/app/status-badge.tsx` for requirements/work items):
  - Draft: `bg-muted text-slate-700`
  - Active: `bg-success-50 text-success-700`
  - Archived: `bg-slate-200 text-slate-600`
  - Won't Do: `bg-warning-50 text-warning-700`
- Priority variants (use `components/app/priority-badge.tsx`):
  - Must: `bg-accent text-accent-foreground` (indigo-50/indigo-700)
  - Should: `bg-cool-sky-50 text-cool-sky-700`
  - Could: `bg-muted text-slate-700`
  - Won't: `bg-background text-slate-500 border border-border`

### Table (`components/ui/table.tsx`)
- Header: `bg-muted border-b border-border`. Column headers: `label-sm` scale
  (`text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground`).
- Body rows: `border-b border-border hover:bg-muted/50`.
- Selected row: `bg-accent border-l-2 border-l-primary` (use `data-selected` attribute).
- ID cells: `font-mono text-muted-foreground`.

### Dialog (`components/ui/dialog.tsx`)
- Backdrop: `fixed inset-0 bg-black/10 supports-backdrop-filter:backdrop-blur-xs`.
- Container: `fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-sm
  bg-popover rounded-xl ring-1 ring-foreground/10 p-4 shadow-lg`.
- Focus trap and focus restore on close are required. Base UI Dialog handles this.
- Title: `text-xl font-semibold tracking-[-0.01em]`.

### Dropdown / Popover (`components/ui/dropdown-menu.tsx`)
- Container: `bg-popover border-0 ring-1 ring-foreground/10 rounded-lg shadow-md p-1.5 min-w-[180px]`.
- Items: `px-3 py-1.5 rounded-md text-sm text-foreground hover:bg-muted data-[active]:bg-accent
  data-[active]:text-accent-foreground`.
- Arrow key, Enter, Escape navigation is handled by Base UI.

### Tabs (`components/ui/tabs.tsx`)
- Two CVA variants: `default` (pill tabs on muted background) and `line` (underline tabs).
- Line variant: `border-b border-border`. Active tab: underline via `after:` pseudo-element
  `after:bg-foreground` (not `border-b-2` directly). Active tab text: `text-foreground`,
  inactive: `text-muted-foreground`.
- Use the `<Tabs>`, `<TabsList>`, `<TabsTrigger>`, `<TabsContent>` slot components.

### Form Layout
- Field container: `space-y-5` between fields.
- Label: `block text-sm font-medium text-foreground mb-1.5`. Use `<label>` component from
  `components/ui/label.tsx`.
- Helper text: `text-xs text-muted-foreground mt-1`.
- Error text: `text-xs text-destructive mt-1`.

### Navigation
- Top bar: `h-[52px] bg-background border-b border-border sticky top-0 z-50`.
  *(Note: live uses `bg-white` — treat `bg-background` as the target spec.)*
- Sidebar: `w-60 bg-background border-r border-border h-[calc(100vh-52px)] overflow-y-auto`.
  See `components/app/app-sidebar.tsx`.
- Nav item: `flex items-center gap-2 h-9 px-3 rounded-sm text-sm font-medium text-muted-foreground
  hover:bg-muted hover:text-foreground`.
- Active nav item: `bg-accent text-accent-foreground relative` with left accent bar via
  `before:absolute before:left-0 before:top-1 before:bottom-1 before:w-0.5 before:bg-primary
  before:rounded-r`.

### Toast / Notification
- Position: fixed top-right, `max-w-[420px]`, `gap-3` from viewport edge.
- Container: `bg-popover border border-border rounded-md shadow-md p-4 flex gap-3`.
- Icon: size 20. Success `text-success-500`. Error `text-destructive`.
- Text: `text-sm`. Title `font-medium text-foreground`. Subtitle `text-muted-foreground mt-0.5`.
- Auto-dismiss: 4s (success), 8s (error).

### Command Palette (⌘K)
- Container: `fixed left-1/2 top-20 -translate-x-1/2 w-full max-w-[640px] bg-popover rounded-xl
  shadow-xl ring-1 ring-foreground/10 overflow-hidden`.
- Search input: `w-full text-lg bg-transparent outline-none placeholder:text-slate-400` inside
  `px-4 py-3 border-b border-border`.
- Results: `max-h-[400px] overflow-y-auto p-2`. Group labels in `label-sm` scale.

### Empty State
- Container: `flex flex-col items-center justify-center py-16 text-center`.
- Icon/illustration: `text-muted-foreground/40 mb-6` at 48px.
- Heading: `text-base font-semibold text-foreground mb-2`.
- Body: `text-sm text-muted-foreground mb-6 max-w-[380px]`.
- CTA: `<Button variant="default">`.

### Diff View (`components/changelog/diff-view.tsx`, `components/app/diff-block.tsx`)
- Container: `bg-background border border-border rounded-md overflow-hidden`.
- Header: `px-4 py-2.5 border-b border-border bg-muted`. Author/timestamp: `flex items-center
  gap-2 text-xs text-muted-foreground`. Reason: `text-xs text-muted-foreground italic`
  (the italic on reason text is intentional brand exception, not a general rule).
- Lines: `font-mono text-[13px] leading-[1.5]`.
  Gutter: `w-14 px-3 py-1.5 text-right text-muted-foreground bg-diff-gutter-bg select-none`.
  Content: `flex-1 px-4 py-1.5`.
- Added line: `bg-diff-added-bg text-diff-added-text`.
- Removed line: `bg-diff-removed-bg text-diff-removed-text line-through`.

### Timeline / Changelog (`components/changelog/timeline.tsx`, `components/app/activity-timeline.tsx`)
- Spine: `absolute left-4 top-0 bottom-0 w-px bg-border`.
- Entry: `relative pl-12 pb-6 last:pb-0`. Avatar: `absolute left-0 top-0`.
- Meta: `text-sm font-medium text-foreground` + `text-muted-foreground` for verb + timestamp.

---

## Brand & Logo

- Strata mark: three left-aligned horizontal bars, `currentColor`. Use `<StrataMark size={n}>` from
  `components/brand/strata-mark.tsx`.
- Wordmark: "grundet" (always lowercase), Inter Medium (`font-sans`), `tracking-[-0.02em]`. Use
  `<Wordmark>` from `components/brand/wordmark.tsx`.
- Logo lockup: `<Logo>` from `components/brand/logo.tsx`.
- Constraints: never recolour, rotate, skew, compress, or apply gradient/glow to any brand element.
  Minimum clear space 1× strata bar height (8/64 units).

---

## Marketing

Marketing code lives in `components/marketing/` and page files under `app/(marketing)/`.
Marketing accent colours (Cool Sky, Bubblegum, Emerald, Espresso) are permitted here but follow
strict budgeting rules: one accent per section, two maximum visible on-screen at once, small
surface area only.

- Page sections: build with the `MARKETING_CONTAINER` constant (`components/marketing/layout.ts`),
  not a hand-rolled `mx-auto max-w-6xl px-6`. It encodes the responsive shell: base `max-w-6xl px-6
  py-24 md:py-32`, widening to `2xl:max-w-[88rem]` / `3xl:max-w-[96rem]` with matching `px`/`py`
  tiers for large presentation displays (see Spacing & Layout → Max-width containers). Per-section
  type and gap scaling (`2xl:text-6xl`, `2xl:gap-…`) is still applied case by case. Keep inner text
  blocks capped at `max-w-xl`/`max-w-2xl` so line length stays within the reading rule as the shell
  widens.
- Long-form (blog/docs): `mx-auto max-w-[720px] px-6`.
- Hero: `pt-32 pb-24`. Tagline pill: `bg-bubblegum-50 border border-bubblegum-100`.
  Display headline: use display-2xl scale (`text-[5rem] leading-[1.0] font-semibold tracking-[-0.03em]`).
  DM Serif Display (`font-serif`) is used only on the hero H1. Background glow: radial gradient,
  `pointer-events-none`.
- Section rhythm: default `bg-background`. Alternating `bg-muted` sections for visual breathing
  room.
- Section heading pattern: eyebrow in `label-sm` scale with accent colour. Main heading in
  `heading-xl` or `heading-lg` scale. Subheading `text-lg text-muted-foreground`.
- Preview frames: `rounded-xl shadow-xl`. Browser chrome: three stone dots (not system colours).
  Hover lift: `hover:-translate-y-1 transition-transform duration-300`.
- Marketing accents in product app (`/app` authenticated routes): forbidden.
- Pricing cards: three-column grid, `max-w-[1080px]`. Recommended: `ring-2 ring-primary`.
  Others: `ring-1 ring-foreground/10`.
- Footer: see `components/marketing/marketing-footer.tsx`. `py-16 bg-muted border-t border-border`.

### Docs
- Layout: 240px sidebar, `max-w-[720px]` content, `text-lg leading-relaxed` body.
- Code blocks: `bg-slate-900 text-slate-50 rounded-md font-mono text-sm p-4 border border-slate-700`.
- Inline code: `bg-muted text-foreground font-mono text-sm rounded-sm px-1.5 py-0.5`.
- Callouts: `border-l-4 p-4 rounded-md`. Note (indigo), Warning (warning), Tip (cool-sky).

---

## Voice & Microcopy

- UI copy uses spaced en dashes only: `some – text in the – middle`. No em dashes or unspaced dashes.
- Headlines: direct claim, concrete enumeration, named truth. ("Every change. Every reason. Every version.")
- Button labels: verb + noun ("Save requirement") or single verb ("Continue"). Never "Submit."
- Form labels: noun, sentence case ("Acceptance criterion"). Never all-caps with colon.
- Helper text: full sentence, period. ("Atomic — one testable statement per requirement.")
- Error messages: what failed + corrective action in one sentence.
- Forbidden words: synergy, paradigm shift, next-generation, revolutionary, cutting-edge,
  AI-powered (as hype), empower, unleash, game-changing, seamless, robust, world-class.

---

## Accessibility

- Focus ring: `focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:border-ring` —
  implemented in all Base UI components. Ensure it is never suppressed with `outline-none` unless
  replaced.
- All inputs paired with `<label>` (use `components/ui/label.tsx`).
- Standalone icons: `aria-label` required.
- Images: `alt` on all. Empty `alt=""` for decorative.
- Buttons: `type="button"` unless submitting a form.
- Contrast: 4.5:1 for body text (AA). 3:1 for large text (AA).
- Reduced motion: wrap all transitions/animations in `motion-safe:` or
  `@media (prefers-reduced-motion: reduce)`. All animations must halt.
- Dialogs: Base UI Dialog handles focus trap and restoration on close. Do not build custom dialogs.
- Dropdowns: Base UI Menu handles keyboard navigation. Do not build custom dropdown menus.
- Selected text: `bg-accent text-accent-foreground`.

---

## Code Quality & Component Reuse

**Check the component tree first.** Before writing any className string, audit
`components/ui/`, `components/app/`, `components/brand/`, `components/changelog/`,
`components/marketing/`. If a component exists, use it. If something similar exists, extend it
via `className` prop. Never duplicate existing components.

**Semantic tokens in components, palette tokens in exceptions only.**
Use `text-foreground`, `bg-background`, `bg-card`, `bg-muted`, `text-muted-foreground`,
`border-border`, `bg-primary`, `text-primary-foreground`, `ring-ring` etc. in all component code.
Reserve palette tokens (`text-slate-900`, `bg-indigo-50`) for diff tokens, specific badge
colour variants, and brand-locked elements that must never shift with theme.

**Base UI primitives.** All interactive UI composes from `@base-ui/react/`. When building a new
interactive component, check Base UI before building from raw HTML. Do not use Radix UI.

**CVA for variants.** When a component needs multiple visual states or sizes, use `cva` from
`class-variance-authority`. Follow the pattern in `components/ui/button.tsx`. Do not use
conditional template literals for multi-variant className logic.

**Always use `cn()`.** Import from `@/lib/utils`. Never concatenate classNames with `+` or
template literals. Never use `clsx` directly.

**File naming.** `kebab-case.tsx` for all components. Named exports only — no default exports.

**No one-off styles.** If a colour, shadow, or radius value is not in `globals.css @theme` or as a
semantic token, stop. If the value is genuinely new, add it to the token system — do not inline
arbitrary values. Exception: pixel-locked shell measurements tied to existing layout constants
(e.g., `h-[52px]` for the nav bar).

**Do not regenerate shadcn primitives.** The files in `components/ui/` are already the overridden
implementations. Never re-run `npx shadcn add` without reviewing what would be overwritten.

**No dark mode speculation.** The `.dark {}` block in `globals.css` is reserved and empty. Do not
add `dark:` variant classes speculatively. Only use them in components that already have dark
mode styles.

**Tailwind discipline.** Stick to the spacing scale: `p-1` (4px), `p-2` (8px), `p-3` (12px),
`p-4` (16px), `p-6` (24px), `p-8` (32px). Avoid `p-5`, `p-7`, `p-9`, `p-10` — they fall
off the 8px rhythm grid.

**Minimise duplication.** Three identical patterns is enough reason to extract a component.
But do not extract prematurely: two instances is not.

---

## Anti-Patterns

Do not do any of these:

- Use `#FFFFFF`, `#000000`, or Tailwind's default `indigo-*` values (they are fully overridden).
- Gradient backgrounds, glassmorphism, or 3D elements anywhere in the product.
- Marketing accent colours (cool-sky, bubblegum, emerald, espresso) inside authenticated routes.
- `DM Serif Display` / `font-serif` inside the product app.
- Body text under 14px in the product UI.
- Pill-shaped buttons (`rounded-full`).
- Multiple accent colours on one surface.
- Pure-black box shadows (`shadow-black`, `rgba(0,0,0,1)`).
- `font-bold` or `font-extrabold` anywhere.
- All-caps labels outside of `label-sm` (table headers, eyebrows).
- Italicised UI labels (exception: diff reason text).
- `border-radius` above `rounded-xl` outside marketing preview frames.
- Bouncy spring animations, parallax >8px, auto-advancing carousels.
- Stock photography or emoji in headlines.
- Writing "GRUNDET" (always lowercase "grundet").
- Importing font files via CSS — use `next/font` only.

---

## Component Reference

Root: `src/Grundet.Web/components/`

| Directory | Components |
|---|---|
| `ui/` | avatar, badge, button, card, dialog, dropdown-menu, input, label, select, separator, table, tabs, textarea, tooltip |
| `app/` | acceptance-criterion, app-sidebar, app-topbar, diff-block, priority-badge, project-switcher, status-badge, version-entry, version-timeline (card-grid based — see `requirement-card-grid.tsx`, `work-item-card-grid.tsx`, `project-tile.tsx`, not a table/row layout) |
| `brand/` | logo, strata-mark, wordmark |
| `marketing/` | changelog-showcase, hero, hero-diff-mock, marketing-footer, marketing-nav, not-list, pillars, request-access-form |
| (root) | site-nav |
