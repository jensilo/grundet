# Grundet Foundation — Design System & Implementation Spec

> The complete identity, design language, and implementation specification for **Grundet** — the versioned requirements platform. This document is the source of truth for visual decisions and contains everything a developer or implementing agent needs to build the product UI, marketing site, docs, email, and brand surfaces in **Next.js 16 (App Router) + Tailwind CSS + shadcn/ui**.

> Read top-to-bottom for full context, or jump straight to **§16 Implementation Reference** for the full `tailwind.config.ts`, `globals.css`, and shadcn theme overrides you can drop into the codebase.

---

## 0. How to Use This Document

This document is **opinionated and implementation-ready**. It does not present options; it commits.

- **§1–§5** are brand context. Read once.
- **§6–§11** are the design system tokens and component specs. Reference often.
- **§12–§15** are surface specifications (marketing, email, social). Reference when building those surfaces.
- **§16** is copy-pasteable implementation code (Tailwind, CSS, shadcn).
- **§17–§19** are guardrails, open questions, and glossary.

The system is named **Grundet Foundation**. Tokens are namespaced under it (e.g. `--foundation-color-indigo-500`, `<FoundationButton>`).

**Core stack assumption:** Next.js 16 App Router · TypeScript · Tailwind CSS · shadcn/ui · Framer Motion (for non-trivial animations) · Lucide icons · Geist Sans/Mono typography.

---

## 1. The Brand at a Glance

| | |
|---|---|
| **Product** | Grundet — versioned, structured requirements platform for software teams |
| **Tagline** | Git for requirements. Finally. |
| **Promise** | Requirements your team can actually trust. |
| **Audience** | Staff/lead engineers and PM directors at agile software teams. Solo developers and indie founders as a secondary audience. |
| **Personality** | Precise. Confident. No-nonsense. |
| **Atmosphere** | High-contrast monochrome with a confident Indigo brand accent. Engineering-precise, B2B-credible, with deliberate moments of colour for energy. **Stripe-coded, not Anthropic-coded.** |
| **System name** | Grundet Foundation |
| **Surfaces in v1** | Product UI, marketing site, docs, transactional email, favicon/logo, social/OG images |
| **Modes** | Light only at launch (token system reserves namespace for dark mode later) |
| **Accessibility floor** | WCAG AA minimum; AAA where free |

---

## 2. Strategic Foundation

### 2.1 The problem

Requirements live nowhere structured. They scatter across Notion pages, Slack threads, ticket descriptions, and senior heads. By the time something breaks, no one knows what was decided when, by whom, or why.

### 2.2 The product

Grundet is the versioned source of truth for requirements. Every change is recorded, attributed, and reversible. It plugs into existing tools (MCP, Jira, Git) so teams adopt it without process upheaval.

### 2.3 The audience

**Primary:** Staff engineers, engineering leads, PM directors at agile teams of 5–50 people. Heavy users of Linear, Notion, Stripe, Vercel, and Claude Code. They tolerate density. They will judge the product in 10 seconds based on whether it *feels* serious.

**Not the audience:** Enterprise procurement committees. Non-software project managers. Anyone who wants gamified dashboards.

### 2.4 Brand promise — decoded

> **Requirements your team can actually trust.**

- *Trust* implies provenance, history, durability.
- *Actually* names the unspoken truth: today's requirements cannot be trusted.
- *Your team* is collective and concrete.
- *Requirements* — the artifact people already use. Not "items," not "stories."

Every visual choice must reinforce **trust through precision**.

---

## 3. Personality & Voice

### 3.1 Three adjectives

**Precise.** Grids align. Type is set with intent. Numbers are right. Diffs are exact.

**Confident.** Grundet does not apologise. The product makes opinionated choices and explains them once.

**No-nonsense.** No marketing fluff. No buzzwords. No "AI-powered" preening. We respect the reader's time.

### 3.2 Tonal palette

| Trait | We are | We are not |
|---|---|---|
| Tone | Direct, calm, professional | Cute, breezy, hype |
| Authority | Earned through specificity | Borrowed from buzzwords |
| Humour | Dry, occasional, never the headline | Punny, emoji-laden |
| Density | Generous on marketing; dense in product | Cluttered or padded |
| Posture | Engineering-precise — like Stripe Press or Linear's changelog | Pitch-deck — generic SaaS landing page |

### 3.3 Voice — concrete examples

**Headline (hero)**
- ✅ "Requirements your team can actually trust."
- ✅ "Every change. Every reason. Every version."
- ❌ "Unleash your team's productivity with AI-powered requirements!"

**Empty state**
- ✅ "No requirements yet. Add the first one — it'll show up in the changelog the moment you save."
- ❌ "Looks pretty empty in here! Click the big button to get started."

**Error**
- ✅ "Couldn't save. Your connection dropped — try once more."
- ❌ "Oops! Something went wrong. Please try again later."

### 3.4 Forbidden vocabulary

`synergy`, `paradigm shift`, `next-generation`, `revolutionary`, `cutting-edge`, `AI-powered` (as standalone hype), `empower` (vague), `unleash`, `game-changing`, `seamless`, `robust`, `world-class`.

---

## 4. Visual Direction

### 4.1 The reference: Stripe, modern Vercel, Linear

Grundet's visual direction is anchored in **Stripe-class B2B clarity**: high-contrast monochrome neutrals, a single confident electric accent, sans-only typography, in-page interactive product previews on marketing surfaces, restrained motion that serves comprehension.

**Take from Stripe:**
- High contrast black-on-white as the spine.
- Live, animated UI components rendered directly on marketing pages (not flat screenshots).
- Tight grid discipline.
- Subtle bright accents in marketing surfaces (Stripe's cyan/orange/purple section moments).
- Code blocks as a first-class brand element.

**Take from Vercel / Linear:**
- Geist Sans + Geist Mono typography stack — precise, modern, technical.
- Pure cool-tinted neutrals (no warm beige).
- Animated in-app preview frames as the homepage centerpiece.
- Crisp, almost-flat component aesthetic — no glassmorphism, no soft shadows everywhere.

**Take from Sentry:**
- One memorable accent colour you can describe to a friend without looking it up.
- Willingness to inject brief moments of brighter colour (in marketing only, never product UI) to signal personality.

### 4.2 Synthesis statement

> **Grundet looks like a precision instrument: monochrome by default, confident Indigo when it acts, and willing to flash a brighter colour when it wants to make you smile.**

The product UI is monochrome with Indigo as the only accent. The marketing site is mostly monochrome with Indigo as the brand action colour, and uses a small palette of brighter accent colours (Cool Sky, Bubblegum Pink, Emerald, Espresso) sparingly to add energy and section identity.

### 4.3 What we are not

- Anthropic's warm beige editorial calm. (We respect it; we are not it.)
- Generic SaaS gradient washes (we use Indigo as a flat, decisive accent — not as a billowing background).
- Atlassian-style enterprise blandness.
- Dark-mode-first "gamer" aesthetics.
- 3D blob illustrations or characters.
- Glassmorphism.

---

## 5. Logo & Wordmark

### 5.1 The mark — *Strata*

**Concept:** "Grundet" reads as *the foundation* / *the ground* in Scandinavian. The metaphor is **geological strata** — horizontal bands of earth, stacked in chronological order, each one a record of a moment. This maps onto Grundet's core mechanic: every requirement is a stack of immutable versions, each layer a record of change.

**Form:** Three horizontal bars, stacked vertically, of subtly varying widths. The middle bar is the longest; the top is medium; the bottom is shortest. All bars share the left edge — this anchors the mark.

**Geometry (64×64 reference unit):**
- Bar height: 8 units each.
- Bar gaps: 6 units.
- Top bar width: 40 units (left-aligned).
- Middle bar width: 56 units (left-aligned).
- Bottom bar width: 32 units (left-aligned).
- Bar fill: solid `slate-950` (no rounded corners, no gradients).

**SVG (drop into the codebase as `components/brand/strata-mark.tsx`):**

```tsx
export function StrataMark({
  size = 24,
  className,
}: { size?: number; className?: string }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 64 64"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={className}
      aria-hidden="true"
    >
      <rect x="8" y="14" width="40" height="8" fill="currentColor" />
      <rect x="8" y="28" width="56" height="8" fill="currentColor" />
      <rect x="8" y="42" width="32" height="8" fill="currentColor" />
    </svg>
  );
}
```

The mark uses `currentColor` so it inherits from CSS — a single component covers light, dark, on-coloured-background, and inverted contexts.

### 5.2 The wordmark

**"grundet"** set in **Geist Sans Medium (500)**, all lowercase, `-0.02em` letter-spacing. The all-lowercase decision is deliberate — confident, modern, unmistakable.

```tsx
export function Wordmark({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        "font-sans font-medium tracking-[-0.02em] text-foreground",
        className
      )}
    >
      grundet
    </span>
  );
}
```

### 5.3 Lockups

| Lockup | Use | Component |
|---|---|---|
| Mark + wordmark, horizontal | Primary — nav, hero, footer | `<Logo />` |
| Wordmark only | Narrow contexts (email signature) | `<Wordmark />` |
| Mark only | App icon, favicon, ≤32px contexts | `<StrataMark />` |

```tsx
export function Logo({ className }: { className?: string }) {
  return (
    <div className={cn("flex items-center gap-2", className)}>
      <StrataMark size={20} className="text-foreground" />
      <Wordmark className="text-[18px]" />
    </div>
  );
}
```

### 5.4 Constraints

- Clear space: minimum 1× bar-height (8/64 of mark size) on all sides.
- Minimum size, mark only: 16×16 px.
- Minimum size, mark + wordmark: 96 px wide.
- **Never** recolour the mark in arbitrary brand colours.
- **Never** apply gradients, glows, or 3D effects.
- **Never** rotate, skew, or compress the mark.
- **Never** set the wordmark in any font other than Geist Sans, lowercase, medium weight.

---

## 6. Color System

### 6.1 Philosophy

Three layers, in order of dominance:

1. **Slate** — cool-tinted monochrome neutrals carrying ~95% of every surface.
2. **Indigo** — single confident brand accent. Used for primary actions, links, focus, and brand moments. Reads as confident, premium, decisive.
3. **Marketing accents** — Cool Sky, Bubblegum Pink, Emerald, Espresso. Used **only** on marketing surfaces (landing, blog, social) for section identity and energy. **Never** appears in the product UI.

We commit to:

- Pure cool monochrome neutrals — **no warm beige tint**.
- High contrast as the default mode (Stripe-class).
- Single primary accent (Indigo `#610F7F`) — a deep, confident purple. Distinct from default-blue SaaS aesthetic, distinct from Stripe's indigo, distinct from Linear's lavender.
- Bright marketing accents are a *seasoning*, not a *meal* — they appear in 1–3 places per marketing page max.

### 6.2 Slate — the neutral spine

Cool-tinted monochrome with a faint blue undertone. These are the workhorses.

| Token | Hex | RGB | Use |
|---|---|---|---|
| `slate-50` | `#FAFBFC` | 250 251 252 | Page background |
| `slate-100` | `#F2F4F7` | 242 244 247 | Surface fills, sidebar, table header |
| `slate-200` | `#E5E8ED` | 229 232 237 | Hairline dividers, table row hover |
| `slate-300` | `#CFD4DC` | 207 212 220 | Borders, input outlines |
| `slate-400` | `#9AA1AD` | 154 161 173 | Placeholder, disabled text |
| `slate-500` | `#6B7280` | 107 114 128 | Secondary text, metadata |
| `slate-600` | `#4B5563` | 75 85 99 | Body text alt, captions |
| `slate-700` | `#303744` | 48 55 68 | Body text |
| `slate-800` | `#1A1F2B` | 26 31 43 | Headings |
| `slate-900` | `#0E1117` | 14 17 23 | Primary text |
| `slate-950` | `#06080C` | 6 8 12 | Highest-contrast text, hero headlines |

**Background:** `slate-50` (`#FAFBFC`) — slightly cool, slightly off pure white. Crisp without being sterile.
**Primary text:** `slate-900` (`#0E1117`) — pure-feeling near-black with imperceptible cool tint.

### 6.3 Indigo (primary) — the brand accent

A single confident Indigo purple. **`indigo-500` (`#610F7F`) is the colour users will associate with Grundet.** Deliberately deep and saturated — confident and premium, distinct from the default-blue SaaS aesthetic. Where Stripe goes electric and Linear goes lavender, Grundet commits to a serious, grounded purple.

| Token | Hex | RGB | Use |
|---|---|---|---|
| `indigo-50` | `#FBF5FC` | 251 245 252 | Subtle accent fill, callouts |
| `indigo-100` | `#F4E5F7` | 244 229 247 | Selected row, light accent surface |
| `indigo-200` | `#E5C6EC` | 229 198 236 | Light hover, secondary accent |
| `indigo-300` | `#CB94D6` | 203 148 214 | Mid accent (rare) |
| `indigo-400` | `#A75BB7` | 167 91 183 | Hover state of primary |
| `indigo-500` | `#610F7F` | 97 15 127 | **Primary brand colour** |
| `indigo-600` | `#530B6E` | 83 11 110 | Pressed / active |
| `indigo-700` | `#43085A` | 67 8 90 | High-contrast accent text |
| `indigo-800` | `#320744` | 50 7 68 | Dark accent (rare) |
| `indigo-900` | `#21052D` | 33 5 45 | Deepest accent |
| `indigo-950` | `#14031C` | 20 3 28 | Near-black brand wash |

**Where Indigo appears (and only here):**
- Primary buttons.
- Brand links (in body copy).
- Active state of navigation items (left border + tinted background).
- Focus rings (everywhere).
- The Strata mark in the hero lockup of the landing page.
- Selected row indicators in tables.
- Required-field asterisks in forms.

**Where Indigo must never appear:**

- As a decorative gradient (except the deliberate Indigo × Bubblegum brand gradient on marketing).
- As a section background.
- As a wash behind text.
- In illustrations as a default fill.
- Anywhere a marketing accent (§6.4) would do the job better.

### 6.4 Marketing accents — *the playful palette*

Four secondary colours used **only on marketing surfaces** to add energy and section identity. Never in the product UI.

| Token | Hex | Use |
|---|---|---|
| `cool-sky-500` | `#5DA9E9` | Friendly informational moments, illustrations, decorative |
| `bubblegum-500` | `#FF6978` | Pairs with Indigo in gradients; CTAs that need energy; never on body text |
| `emerald-500` | `#06D6A0` | Marketing-only success moments and bright illustrations (do **not** use for system success states — that's `success-500`) |
| `espresso-500` | `#551B14` | Grounding/depth element, premium "expensive coffee" warmth (matches the brand audience) |

Each marketing accent has a 50/100/500/700 scale only (full scale in §16):

```
cool-sky-50:    #E8F3FC   cool-sky-100:   #C8E2F7
cool-sky-500:   #5DA9E9   cool-sky-700:   #1F6FB0

bubblegum-50:   #FFE9EC   bubblegum-100:  #FFCAD0
bubblegum-500:  #FF6978   bubblegum-700:  #C2293A

emerald-50:     #E0FAF1   emerald-100:    #B6F2DD
emerald-500:    #06D6A0   emerald-700:    #028A66

espresso-50:    #F4E8E5   espresso-100:   #E2C5BD
espresso-500:   #551B14   espresso-700:   #3A120C
```

**Rules:**

- Maximum **one** marketing accent per page section.
- Maximum **two** marketing accents visible on screen at once.
- Marketing accents are used as **seasoning** — small surface area, high impact (a single chip, a section divider, an illustration accent, a hover highlight).
- Marketing accents **never** appear in the authenticated product app. The transition from marketing → product is a transition from playful → focused.
- **Emerald** is marketing-only; the system success state lives in `success-500` (§6.5). They are deliberately distinct so a celebratory illustration never gets confused with a "save succeeded" toast.

### 6.5 Functional palette

Used for state communication only.

| Token | Hex | Use |
|---|---|---|
| `success-50` | `#E6F8EF` | Success surface (toast, callout) |
| `success-500` | `#16A34A` | Successful save, completed work item |
| `success-700` | `#0F7A37` | Success text on light surface |
| `warning-50` | `#FEF6E1` | Warning surface |
| `warning-500` | `#D97706` | Warnings, "Won't Do," deprecated |
| `warning-700` | `#92520A` | Warning text on light surface |
| `destructive-50` | `#FDE9E9` | Destructive surface |
| `destructive-500` | `#DC2626` | Delete, error toast |
| `destructive-700` | `#A41C1C` | Destructive text on light surface |

**Note:** functional colours stay within their lane. Success is *only* success state, never decoration. Warnings are *only* warnings, never accent variation.

### 6.6 Diff highlighting (product-specific)

The diff/changelog view is the brand component. Industry-standard git-diff conventions apply.

| Token | Hex | Use |
|---|---|---|
| `diff-added-bg` | `#E6F8EF` | Added line background (= `success-50`) |
| `diff-added-text` | `#0F4F26` | Added line text |
| `diff-removed-bg` | `#FDE9E9` | Removed line background (= `destructive-50`) |
| `diff-removed-text` | `#5F1414` | Removed line text (with strikethrough) |
| `diff-context-bg` | `#FAFBFC` | Unchanged line background (= `slate-50`) |
| `diff-gutter-bg` | `#F2F4F7` | Line-number gutter (= `slate-100`) |

### 6.7 Hard rules

- **Never** use `#FFFFFF` or `#000000` directly. Always go through Slate.
- **Never** mix Indigo with another saturated brand colour on the same surface (the deliberate Indigo × Bubblegum gradient on marketing is the single exception).
- **Always** ensure 4.5:1 contrast for body text (AA), 7:1 where it costs nothing (AAA).
- **Reserve** Indigo for primary action and brand moments. If everything is brand-coloured, nothing is.
- **Marketing accents stay on marketing.** No leakage into the authenticated app.

---

## 7. Typography

### 7.1 Type stack

**Body & display: Inter**
- Open-source variable font, designed for screen readability at all sizes. Used by Linear, GitHub, Figma, and others in dense B2B interfaces.
- Used for all UI text — headings, body, navigation, buttons, labels, form elements.
- Hosted via `next/font` for zero-CLS loading.

**Display serif (marketing only): DM Serif Display**
- High-contrast serif for marketing hero H1s and section catch lines only.
- Never used in the authenticated product app.
- Paired with Inter at large display sizes for visual contrast and personality on the landing page.

**Mono: Geist Mono**
- Companion to Geist Sans.
- Used for: requirement IDs (`IN-104`), code snippets, MCP examples, technical metadata, diff content, monospace UI moments.

**Fallbacks:**

```css
/* UI / body */
font-family: 'Inter', system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;

/* Display serif — marketing only */
font-family: 'DM Serif Display', Georgia, 'Times New Roman', serif;

/* Mono */
font-family: 'Geist Mono', 'JetBrains Mono', ui-monospace, SFMono-Regular, Menlo, monospace;
```

### 7.2 Loading via `next/font`

```ts
// app/layout.tsx
import { Inter, DM_Serif_Display, Geist_Mono } from "next/font/google"

const inter = Inter({ variable: "--font-inter", subsets: ["latin"], display: "swap" })
const dmSerifDisplay = DM_Serif_Display({
  variable: "--font-dm-serif",
  subsets: ["latin"],
  weight: "400",
  display: "swap",
})
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] })
```

```tsx
// app/layout.tsx — body className
<body className={`${inter.variable} ${dmSerifDisplay.variable} ${geistMono.variable} antialiased`}>
```

### 7.3 Type scale

Based on a 1.250 (major third) modular scale. Base size: 16px. All values in rem for accessibility.

| Token | Size | Line-height | Weight | Tracking | Tailwind class |
|---|---|---|---|---|---|
| `display-2xl` | 80px / 5rem | 1.0 | 600 | -0.04em | `text-[5rem] leading-[1.0] font-semibold tracking-[-0.04em]` |
| `display-xl` | 64px / 4rem | 1.05 | 600 | -0.035em | `text-[4rem] leading-[1.05] font-semibold tracking-[-0.035em]` |
| `display-lg` | 48px / 3rem | 1.1 | 600 | -0.03em | `text-5xl leading-[1.1] font-semibold tracking-[-0.03em]` |
| `display-md` | 40px / 2.5rem | 1.15 | 600 | -0.025em | `text-4xl leading-[1.15] font-semibold tracking-[-0.025em]` |
| `heading-xl` | 32px / 2rem | 1.2 | 600 | -0.02em | `text-3xl leading-[1.2] font-semibold tracking-[-0.02em]` |
| `heading-lg` | 24px / 1.5rem | 1.3 | 600 | -0.015em | `text-2xl leading-[1.3] font-semibold tracking-[-0.015em]` |
| `heading-md` | 20px / 1.25rem | 1.35 | 600 | -0.01em | `text-xl leading-[1.35] font-semibold tracking-[-0.01em]` |
| `heading-sm` | 16px / 1rem | 1.4 | 600 | 0 | `text-base leading-[1.4] font-semibold` |
| `body-lg` | 20px / 1.25rem | 1.6 | 400 | 0 | `text-xl leading-relaxed` |
| `body-md` | 17px / 1.0625rem | 1.55 | 400 | 0 | `text-[17px] leading-[1.55]` |
| `body-sm` | 15px / 0.9375rem | 1.5 | 400 | 0 | `text-[15px] leading-normal` |
| `body-xs` | 14px / 0.875rem | 1.45 | 400 | 0 | `text-sm leading-[1.45]` |
| `label-md` | 14px / 0.875rem | 1.4 | 500 | 0 | `text-sm font-medium` |
| `label-sm` | 12px / 0.75rem | 1.35 | 500 | 0.04em | `text-xs font-medium tracking-[0.04em] uppercase` |
| `code-md` | 14px / 0.875rem | 1.55 | 400 | 0 | `font-mono text-sm leading-[1.55]` |
| `code-sm` | 13px / 0.8125rem | 1.5 | 400 | 0 | `font-mono text-[0.8125rem] leading-[1.5]` |

### 7.4 Pairing rules

- **Display serif is marketing-only.** `DM Serif Display` (`font-serif`) appears exclusively on marketing surfaces (landing page hero H1, section catch lines). It never appears in the authenticated product app.
- **Display sizes (≥40px)** use weight 600 with negative tracking. The crispness comes from weight + tracking, not from a serif moment.
- **Body weight is 400.** Medium (500) is the *only* exception, used for labels, button text, and inline emphasis.
- **Bold (700+)** is rarely used. If you find yourself reaching for it, the hierarchy is wrong.
- **All-caps** is reserved for `label-sm` (eyebrow text, table headers, badges) at 12px with `+0.04em` tracking. Never set headlines in all-caps.
- **Italic** is reserved for foreign terms or quoted titles. Never italicise UI labels.

### 7.5 Reading rhythm

- Marketing/docs body: 60–75 character line length, `text-lg leading-relaxed`.
- Product UI body: 80–100 character line length, `text-base leading-[1.55]`.
- Paragraph spacing: `mt-6` (24px) between paragraphs in long-form, `mt-4` (16px) in UI.
- Heading-to-body spacing: `mt-2` (8px) between heading and following paragraph for `heading-sm`/`heading-md`; `mt-4` for larger.

---

## 8. Spacing, Radius, Shadow

### 8.1 Spacing scale

**4px base, 8px primary grid.** Tailwind's default scale aligns; no custom extension needed for spacing tokens.

| Token | Value | Tailwind | Use |
|---|---|---|---|
| `space-1` | 4px | `p-1` `gap-1` | Tight in-line (icon-to-text) |
| `space-2` | 8px | `p-2` | Compact element padding |
| `space-3` | 12px | `p-3` | Default form-control padding |
| `space-4` | 16px | `p-4` | Standard component padding |
| `space-5` | 20px | `p-5` | Form row spacing |
| `space-6` | 24px | `p-6` | Card padding |
| `space-8` | 32px | `p-8` | Section internal spacing |
| `space-12` | 48px | `p-12` | Section-to-section (compact) |
| `space-16` | 64px | `p-16` | Section-to-section (default) |
| `space-24` | 96px | `p-24` | Marketing section spacing |
| `space-32` | 128px | `p-32` | Marketing hero margins |

### 8.2 Layout containers

```tsx
// Marketing page max-width
<div className="mx-auto max-w-[1280px] px-6">…</div>

// Product UI content max-width (forms / reading)
<div className="mx-auto max-w-[1080px] px-6">…</div>

// Long-form reading (docs, blog)
<div className="mx-auto max-w-[720px] px-6">…</div>

// Marketing section vertical padding
<section className="py-24">…</section>     // Default
<section className="py-32">…</section>     // Hero
<section className="py-16">…</section>     // Compact
```

### 8.3 Radius

| Token | Value | Tailwind | Use |
|---|---|---|---|
| `radius-none` | 0 | `rounded-none` | Strata mark, code blocks (optional) |
| `radius-sm` | 4px | `rounded-sm` | Inputs (small), badges |
| `radius-md` | 6px | `rounded-md` | **Default** — buttons, cards, inputs |
| `radius-lg` | 8px | `rounded-lg` | Larger cards, modals, dialogs |
| `radius-xl` | 12px | `rounded-xl` | Marketing in-app preview frames |
| `radius-full` | 9999px | `rounded-full` | Avatars only (use sparingly elsewhere) |

We commit to **6px (`rounded-md`)** as the default. No 16px+ radii anywhere except the marketing preview frames.

### 8.4 Shadow

Subtle, cool-tinted (using `slate-900` rgba), never harsh black. Inline values (drop into Tailwind's `boxShadow` extension, see §16):

| Token | CSS | Use |
|---|---|---|
| `shadow-sm` | `0 1px 2px rgb(14 17 23 / 0.04)` | Hover lift on cards |
| `shadow-md` | `0 4px 12px rgb(14 17 23 / 0.06), 0 1px 2px rgb(14 17 23 / 0.04)` | Dropdown, popover |
| `shadow-lg` | `0 16px 32px rgb(14 17 23 / 0.08), 0 4px 8px rgb(14 17 23 / 0.04)` | Modal, dialog |
| `shadow-xl` | `0 32px 64px rgb(14 17 23 / 0.12), 0 8px 16px rgb(14 17 23 / 0.06)` | Marketing preview frames |
| `shadow-focus` | `0 0 0 2px var(--background), 0 0 0 4px var(--indigo-500)` | Focus ring |

### 8.5 Borders

| Token | CSS | Use |
|---|---|---|
| `border-default` | `1px solid var(--slate-300)` | Inputs, cards |
| `border-hairline` | `1px solid var(--slate-200)` | Table rows, dividers |
| `border-strong` | `1px solid var(--slate-700)` | Selected, emphasised |
| `border-focus` | `1px solid var(--indigo-500)` | Focus state on inputs |

---

## 9. Motion & Interaction

### 9.1 Principles

1. **Motion serves comprehension.** Every animation explains a state change. If it doesn't, cut it.
2. **Confident, not bouncy.** Ease-out curves, not springs. No overshoot.
3. **Brief.** 150–300ms standard. 400ms max for surface transitions.
4. **`prefers-reduced-motion` always respected.**

### 9.2 Timing tokens

```css
:root {
  --duration-instant: 100ms;
  --duration-fast: 150ms;
  --duration-base: 200ms;
  --duration-medium: 300ms;
  --duration-slow: 400ms;

  --ease-out: cubic-bezier(0.2, 0, 0, 1);
  --ease-in: cubic-bezier(0.4, 0, 1, 1);
  --ease-in-out: cubic-bezier(0.4, 0, 0.2, 1);
  --ease-linear: linear;
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

### 9.3 Standard patterns (Framer Motion)

```tsx
// Standard entrance
const entrance = {
  initial: { opacity: 0, y: 12 },
  animate: { opacity: 1, y: 0 },
  transition: { duration: 0.4, ease: [0.2, 0, 0, 1] },
};

// Stagger children (marketing sections)
const staggerContainer = {
  animate: { transition: { staggerChildren: 0.08 } },
};

// Modal entrance
const modalEntrance = {
  initial: { opacity: 0, scale: 0.96 },
  animate: { opacity: 1, scale: 1 },
  exit: { opacity: 0, scale: 0.98 },
  transition: { duration: 0.2, ease: [0.2, 0, 0, 1] },
};
```

### 9.4 Specific animations

**Diff reveal — *the brand animation*.** When a requirement edit appears in the changelog:
- Old text strikes through over 200ms (linear).
- New text fades in below over 300ms ease-out, 100ms after the strike-through starts.
- The whole sequence reads as a deliberate edit, not a flicker.

```tsx
// components/changelog/diff-reveal.tsx
<motion.span
  className="line-through"
  initial={{ textDecorationColor: 'transparent' }}
  animate={{ textDecorationColor: 'currentColor' }}
  transition={{ duration: 0.2, ease: 'linear' }}
>
  {oldText}
</motion.span>
<motion.span
  className="block mt-1"
  initial={{ opacity: 0, y: 4 }}
  animate={{ opacity: 1, y: 0 }}
  transition={{ duration: 0.3, ease: [0.2, 0, 0, 1], delay: 0.1 }}
>
  {newText}
</motion.span>
```

**Marketing preview frame hover.** `translateY(-4px)` over 300ms ease-in-out. No scale.

**Toast.** Slide from `translateX(24px)` to 0 over 200ms ease-out. Auto-dismiss after 4s with 150ms ease-in fade.

**Loading shimmer.** 1200ms linear loop, 1px-wide gradient sweep on skeleton.

### 9.5 What we do not do

- Bouncy springs.
- Auto-advancing carousels.
- Parallax beyond 8px hero offset.
- Section-by-section reveal animations (only the hero gets a real entrance).
- Element rotations.
- Decorative looping animations except the deliberate Live Changelog loop on the homepage.

---

## 10. Iconography

### 10.1 Lucide as the system

```bash
pnpm add lucide-react
```

```tsx
import { Diff, GitBranch, Layers, Plus } from 'lucide-react';

<Diff size={16} strokeWidth={1.75} className="text-slate-700" />
```

**Rules:**
- Default size: 16px (UI), 20px (touch targets), 24px (navigation).
- Default stroke: `1.75`.
- Colour: `currentColor` (inherits from text colour).
- Icon + text spacing: `gap-2` (8px), icon always first.

### 10.2 Custom icons (commission separately)

- The Strata mark itself (§5.1).
- A "diff" composite glyph: two horizontal bars, one struck through, one solid.
- An "MCP" mark (when MCP integration ships publicly).

### 10.3 Empty-state illustrations

Single-purpose, monochrome line drawings (~120px square). Built from the Strata bar primitive:
- Empty list: a single bar with a faint dashed outline above and below (suggesting more to come).
- Empty diff: two bars, one ghosted, one solid.
- Empty timeline: a single bar with a vertical guideline above it.

These are simple SVGs — no commission needed. Implement directly in `components/empty-states/`.

---

## 11. Component Specs (Product UI)

This section is the implementation contract for every shadcn component used in Grundet. For each, the spec includes intended look, key tokens, and a code skeleton.

### 11.1 Button

**Variants:** `primary` (default), `secondary`, `ghost`, `destructive`, `link`.
**Sizes:** `sm` (32px), `md` (40px, default), `lg` (48px, marketing only).

```tsx
// components/ui/button.tsx (shadcn override)
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        primary:
          "bg-indigo-500 text-slate-50 hover:bg-indigo-400 active:bg-indigo-600",
        secondary:
          "bg-slate-50 text-slate-900 border border-slate-300 hover:bg-slate-100 active:bg-slate-200",
        ghost:
          "text-slate-700 hover:bg-slate-100 active:bg-slate-200",
        destructive:
          "bg-destructive-500 text-slate-50 hover:bg-destructive-700 active:bg-destructive-700",
        link:
          "text-indigo-500 underline-offset-4 hover:underline",
      },
      size: {
        sm: "h-8 px-3 text-sm",
        md: "h-10 px-4 text-sm",
        lg: "h-12 px-6 text-base",
        icon: "h-10 w-10",
      },
    },
    defaultVariants: { variant: "primary", size: "md" },
  }
);
```

### 11.2 Input / Textarea

```tsx
// Default classes for shadcn Input
"flex h-10 w-full rounded-md border border-slate-300 bg-slate-50 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus-visible:outline-none focus-visible:border-indigo-500 focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-400"
```

- Error state: replace border with `border-destructive-500`, helper text `text-destructive-500`.
- Required indicator: `<span className="text-indigo-500 ml-0.5">*</span>` after label.

### 11.3 Card

```tsx
<div className="bg-slate-50 border border-slate-200 rounded-md p-6 shadow-sm hover:shadow-md transition-shadow">
  {children}
</div>
```

Marketing preview frames use `rounded-xl shadow-xl` instead.

### 11.4 Dialog / Modal

```tsx
// Backdrop
<div className="fixed inset-0 bg-slate-900/40" />

// Container
<div className="fixed left-[50%] top-[50%] -translate-x-1/2 -translate-y-1/2
                w-full max-w-[560px] bg-slate-50 rounded-lg shadow-lg p-8">
  <header className="flex items-start justify-between mb-6">
    <h2 className="text-xl font-semibold tracking-[-0.01em]">{title}</h2>
    <button className="text-slate-500 hover:text-slate-700">
      <X size={20} />
    </button>
  </header>
  <div className="text-sm text-slate-700">{children}</div>
</div>
```

### 11.5 Dropdown / Popover

```tsx
<div className="bg-slate-50 border border-slate-200 rounded-md shadow-md p-2 min-w-[200px]">
  <button className="w-full text-left px-3 py-2 rounded-sm text-sm text-slate-900 hover:bg-slate-100 data-[active]:bg-indigo-50 data-[active]:text-indigo-700">
    {label}
  </button>
</div>
```

### 11.6 Tabs

```tsx
<div className="border-b border-slate-200">
  <nav className="flex gap-6">
    <button className="py-3 text-sm font-medium text-slate-600 hover:text-slate-800 data-[active]:text-slate-900 data-[active]:border-b-2 data-[active]:border-indigo-500 -mb-[1px]">
      Active
    </button>
  </nav>
</div>
```

### 11.7 Badge / Chip

Used heavily for Status, Priority, Labels.

```tsx
// components/ui/badge.tsx
const badgeVariants = cva(
  "inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium tracking-[0.02em]",
  {
    variants: {
      variant: {
        // Status variants
        draft:    "bg-slate-100 text-slate-700",
        active:   "bg-success-50 text-success-700",
        archived: "bg-slate-200 text-slate-600",
        wontDo:   "bg-warning-50 text-warning-700",

        // Priority variants
        must:   "bg-destructive-50 text-destructive-700",
        should: "bg-indigo-50 text-indigo-700",
        could:  "bg-slate-100 text-slate-700",
        wont:   "bg-slate-50 text-slate-500 border border-slate-200",

        // Generic label
        label: "bg-slate-100 text-slate-700",
      },
    },
    defaultVariants: { variant: "label" },
  }
);
```

### 11.8 Table

The list view of requirements.

```tsx
<table className="w-full text-sm">
  <thead>
    <tr className="bg-slate-100 border-b border-slate-200">
      <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-[0.04em] text-slate-600">
        ID
      </th>
      {/* … */}
    </tr>
  </thead>
  <tbody>
    <tr className="border-b border-slate-200 hover:bg-slate-50 data-[selected]:bg-indigo-50 data-[selected]:border-l-2 data-[selected]:border-l-indigo-500">
      <td className="px-4 py-3 font-mono text-slate-600">IN-104</td>
      {/* … */}
    </tr>
  </tbody>
</table>
```

### 11.9 Diff View — *the brand component*

The single most important component. Renders before/after for a Requirement version.

```tsx
// components/changelog/diff-view.tsx
export function DiffView({ before, after, author, timestamp, reason }: Props) {
  return (
    <div className="bg-slate-50 border border-slate-200 rounded-md overflow-hidden">
      <header className="flex items-center justify-between px-4 py-2.5 border-b border-slate-200 bg-slate-100">
        <div className="flex items-center gap-2 text-xs">
          <Avatar size={20} user={author} />
          <span className="font-medium text-slate-800">{author.name}</span>
          <span className="text-slate-500">{relativeTime(timestamp)}</span>
        </div>
        {reason && (
          <span className="text-xs text-slate-600 italic">"{reason}"</span>
        )}
      </header>
      <div className="font-mono text-[13px] leading-[1.5]">
        <DiffLine type="removed" lineNumber={1}>{before}</DiffLine>
        <DiffLine type="added" lineNumber={1}>{after}</DiffLine>
      </div>
    </div>
  );
}

function DiffLine({ type, lineNumber, children }) {
  const styles = {
    removed: "bg-destructive-50 text-[#5F1414] line-through",
    added: "bg-success-50 text-[#0F4F26]",
    context: "bg-slate-50 text-slate-700",
  }[type];

  return (
    <div className={`flex ${styles}`}>
      <span className="w-14 px-3 py-1.5 text-right text-slate-500 bg-slate-100 select-none">
        {lineNumber}
      </span>
      <span className="flex-1 px-4 py-1.5">{children}</span>
    </div>
  );
}
```

### 11.10 Timeline / Changelog

```tsx
<ol className="relative">
  <div className="absolute left-4 top-0 bottom-0 w-px bg-slate-200" aria-hidden="true" />
  {entries.map((entry) => (
    <li key={entry.id} className="relative pl-12 pb-6 last:pb-0">
      <Avatar size={32} user={entry.author} className="absolute left-0 top-0" />
      <div className="text-sm">
        <span className="font-medium text-slate-900">{entry.author.name}</span>
        <span className="text-slate-600"> {entry.verb} </span>
        <span className="text-slate-500">{relativeTime(entry.timestamp)}</span>
      </div>
      <CollapsibleDiff entry={entry} />
    </li>
  ))}
</ol>
```

### 11.11 Empty state

```tsx
<div className="flex flex-col items-center justify-center py-16 text-center">
  <EmptyStrata className="text-slate-300 mb-6" />
  <h3 className="text-base font-semibold text-slate-900 mb-2">
    No requirements yet.
  </h3>
  <p className="text-sm text-slate-600 mb-6 max-w-[380px]">
    Add the first one — it'll show up in the changelog the moment you save.
  </p>
  <Button>Add requirement</Button>
</div>
```

### 11.12 Toast / Notification

Position top-right, `gap-3` from edges. Auto-dismiss 4s (8s for errors).

```tsx
<div className="fixed top-6 right-6 max-w-[420px] bg-slate-50 border border-slate-200 rounded-md shadow-md p-4 flex gap-3">
  <CheckCircle2 size={20} className="text-success-500 flex-shrink-0 mt-0.5" />
  <div className="flex-1 text-sm">
    <p className="font-medium text-slate-900">Requirement saved</p>
    <p className="text-slate-600 mt-0.5">IN-104 was updated.</p>
  </div>
  <button className="text-slate-500 hover:text-slate-700">
    <X size={16} />
  </button>
</div>
```

### 11.13 Form layout

```tsx
<div className="space-y-5">
  <div>
    <label className="block text-sm font-medium text-slate-800 mb-2">
      Acceptance criterion
      <span className="text-indigo-500 ml-0.5">*</span>
    </label>
    <Input placeholder="Given… When… Then…" />
    <p className="text-xs text-slate-500 mt-1">
      Atomic — one testable statement.
    </p>
  </div>
</div>
```

### 11.14 Navigation

**Top bar:** 56px tall, `bg-slate-50`, `border-b border-slate-200`.

```tsx
<header className="sticky top-0 z-40 h-14 bg-slate-50 border-b border-slate-200">
  <div className="h-full flex items-center justify-between px-6">
    <div className="flex items-center gap-6">
      <Logo />
      <ProjectSwitcher />
    </div>
    <div className="flex items-center gap-3">
      <SearchTrigger />
      <NotificationsButton />
      <UserMenu />
    </div>
  </div>
</header>
```

**Sidebar:** 240px (default), `bg-slate-50`, `border-r border-slate-200`.

```tsx
<aside className="w-60 bg-slate-50 border-r border-slate-200 h-[calc(100vh-56px)] overflow-y-auto">
  <nav className="p-3 space-y-1">
    <NavSection label="Project" />
    <NavItem icon={Layers} active>Requirements</NavItem>
    <NavItem icon={GitBranch}>Work items</NavItem>
    <NavItem icon={History}>Changelog</NavItem>
  </nav>
</aside>

// NavItem (active state)
<a className="flex items-center gap-2 h-9 px-3 rounded-sm text-sm font-medium text-slate-700 hover:bg-slate-100
              data-[active]:bg-indigo-50 data-[active]:text-indigo-700
              data-[active]:before:absolute data-[active]:before:left-0 data-[active]:before:top-1 data-[active]:before:bottom-1 data-[active]:before:w-0.5 data-[active]:before:bg-indigo-500 data-[active]:before:rounded-r relative" />
```

### 11.15 Command palette (⌘K)

```tsx
<div className="fixed left-1/2 top-20 -translate-x-1/2 w-full max-w-[640px] bg-slate-50 rounded-lg shadow-xl border border-slate-200 overflow-hidden">
  <div className="px-4 py-3 border-b border-slate-200">
    <input
      className="w-full text-lg bg-transparent outline-none placeholder:text-slate-400"
      placeholder="Search requirements, features, projects…"
      autoFocus
    />
  </div>
  <div className="max-h-[400px] overflow-y-auto p-2">
    {/* Grouped results: each group has a label-sm header */}
  </div>
</div>
```

---

## 12. Marketing Surface

This is where Grundet earns the click. The brief: a marketing site that feels like *Stripe × Vercel × the inside of a precision instrument*.

### 12.1 Page inventory (v1)

- `/` — landing
- `/product` — deeper product dive
- `/pricing`
- `/changelog` — Grundet's own product changelog (eats own dog food)
- `/about`
- `/login` and `/signup`
- `docs.grundet.app` — separate subdomain, shares the system

### 12.2 Landing page section spec

Build each section as a dedicated component in `src/app/(marketing)/_sections/`.

#### Section 0 — Top nav

```tsx
// _sections/marketing-nav.tsx
<nav className="sticky top-0 z-50 h-16 bg-slate-50/80 backdrop-blur-md border-b border-slate-200">
  <div className="h-full mx-auto max-w-[1280px] px-6 flex items-center justify-between">
    <Logo />
    <div className="flex items-center gap-8">
      <NavLink href="/product">Product</NavLink>
      <NavLink href="/pricing">Pricing</NavLink>
      <NavLink href="/docs">Docs</NavLink>
      <NavLink href="/changelog">Changelog</NavLink>
    </div>
    <div className="flex items-center gap-3">
      <Button variant="ghost" size="sm">Log in</Button>
      <Button size="sm">Get early access</Button>
    </div>
  </div>
</nav>
```

#### Section 1 — Hero

```tsx
// _sections/hero.tsx
<section className="relative pt-32 pb-24 overflow-hidden">
  {/* Subtle bubblegum glow in top-right corner */}
  <div className="absolute top-0 right-0 w-[600px] h-[600px] -translate-y-1/2 translate-x-1/4
                  bg-bubblegum-500/15 blur-[120px] rounded-full pointer-events-none" />

  <div className="relative mx-auto max-w-[1280px] px-6">
    <div className="max-w-[760px]">
      <div className="inline-flex items-center gap-2 mb-6 px-3 py-1 rounded-full bg-bubblegum-50 border border-bubblegum-100">
        <span className="w-1.5 h-1.5 rounded-full bg-bubblegum-500" />
        <span className="text-xs font-medium tracking-[0.04em] text-slate-700 uppercase">
          Closed alpha — June 2026
        </span>
      </div>

      <h1 className="text-[5rem] leading-[1.0] font-semibold tracking-[-0.04em] text-slate-950">
        Requirements your team can actually trust.
      </h1>

      <p className="mt-6 text-lg leading-relaxed text-slate-600 max-w-[600px]">
        Grundet keeps requirements structured, versioned, and linked to the work —
        so nothing gets lost between the decision and the deploy.
      </p>

      <div className="mt-10 flex items-center gap-4">
        <Button size="lg">Get early access</Button>
        <Button size="lg" variant="ghost">
          See how it works <ArrowRight size={16} />
        </Button>
      </div>
    </div>

    <div className="mt-20">
      <LiveChangelogPreview />
    </div>
  </div>
</section>
```

#### Section 2 — The problem

```tsx
// _sections/problem.tsx — mostly typographic
<section className="py-24 bg-slate-100">
  <div className="mx-auto max-w-[960px] px-6 text-center">
    <p className="text-xs font-medium tracking-[0.08em] uppercase text-espresso-500 mb-4">
      The problem
    </p>
    <p className="text-[2.5rem] leading-[1.15] font-semibold tracking-[-0.025em] text-slate-900">
      Today, requirements live nowhere structured.
      <br />
      <span className="text-slate-500">Change is invisible.</span>
      <br />
      <span className="text-slate-500">Teams ship the wrong thing.</span>
    </p>
    <p className="mt-8 text-base text-slate-600">
      We can do better.
    </p>
  </div>
</section>
```

#### Section 3 — Three pillars

```tsx
<section className="py-24">
  <div className="mx-auto max-w-[1280px] px-6">
    <div className="grid grid-cols-1 md:grid-cols-3 gap-12">
      <Pillar
        icon={Layers}
        accent="indigo"
        title="Atomic & versioned."
        body="One testable statement per requirement. Every change creates a new version. The history is the changelog — searchable, attributable, reversible."
      />
      <Pillar
        icon={GitBranch}
        accent="espresso"
        title="Linked to the work."
        body="Requirements connect to work items, code, and Jira issues. Trace a feature from idea to commit without rebuilding the trail by hand."
      />
      <Pillar
        icon={Plug}
        accent="cool-sky"
        title="Plugs into your tools."
        body="MCP-native, so Claude Code reads the live spec. REST API for everything else. Your existing workflow gets a memory."
      />
    </div>
  </div>
</section>
```

Each pillar's icon uses the section's accent colour. **Indigo** is reserved for the section that demonstrates Grundet's primary action; **Espresso** grounds the linking section with depth; **Cool Sky** colours the integrations section. This is the marketing accent palette doing its job — section identity without dilution.

#### Section 4 — The Live Changelog moment

```tsx
<section className="py-24 bg-slate-100">
  <div className="mx-auto max-w-[1280px] px-6">
    <div className="text-center mb-16">
      <h2 className="text-5xl leading-[1.1] font-semibold tracking-[-0.03em] text-slate-950">
        Every change. Every reason. Every version.
      </h2>
      <p className="mt-4 text-lg text-slate-600 max-w-[640px] mx-auto">
        Grundet records every edit as an immutable version. Nothing is overwritten.
        Nothing is lost. The changelog is the truth.
      </p>
    </div>
    <LiveChangelogPreview variant="full" />
  </div>
</section>
```

#### Section 5–8

- **5. How teams use it** — three vignettes, alternating left/right product previews + copy.
- **6. Integrations strip** — `bg-slate-100`, monochrome logo row, hover reveals brand colour.
- **7. Pricing teaser** — single line + link.
- **8. Final CTA** — `bg-slate-950` band, `text-slate-50` headline, primary CTA in Indigo.

#### Section 9 — Footer

```tsx
<footer className="py-16 bg-slate-100 border-t border-slate-200">
  <div className="mx-auto max-w-[1280px] px-6 grid grid-cols-2 md:grid-cols-5 gap-8">
    <div className="md:col-span-2">
      <Logo />
      <p className="mt-4 text-sm text-slate-600 max-w-[280px]">
        Versioned requirements for software teams.
      </p>
    </div>
    <FooterColumn label="Product" links={[…]} />
    <FooterColumn label="Resources" links={[…]} />
    <FooterColumn label="Legal" links={[…]} />
  </div>
  <div className="mx-auto max-w-[1280px] px-6 mt-12 pt-8 border-t border-slate-200 flex justify-between text-xs text-slate-500">
    <span>© {new Date().getFullYear()} Grundet</span>
    <span>Made in Göteborg, Sweden</span>
  </div>
</footer>
```

### 12.3 In-app preview frames — *the brand centerpiece*

This is the most important visual idea on the marketing site. **Real product UI rendered as actual HTML/CSS, not screenshots.**

#### Frame anatomy

```tsx
// _components/preview-frame.tsx
export function PreviewFrame({
  children,
  showChrome = true,
  url = "grundet.app/projects/auth/requirements/IN-104",
}: Props) {
  return (
    <motion.div
      whileHover={{ y: -4 }}
      transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
      className="bg-slate-50 border border-slate-200 rounded-xl shadow-xl overflow-hidden"
    >
      {showChrome && (
        <div className="flex items-center gap-3 px-4 py-3 bg-slate-100 border-b border-slate-200">
          <div className="flex gap-1.5">
            <div className="w-3 h-3 rounded-full bg-slate-300" />
            <div className="w-3 h-3 rounded-full bg-slate-300" />
            <div className="w-3 h-3 rounded-full bg-slate-300" />
          </div>
          <div className="flex-1 mx-4 h-6 px-3 rounded-md bg-slate-50 border border-slate-200 flex items-center text-xs text-slate-500 font-mono">
            {url}
          </div>
        </div>
      )}
      <div className="p-6">{children}</div>
    </motion.div>
  );
}
```

**Note on the chrome dots:** Stone-coloured, *not* macOS traffic-light red/yellow/green. We are not pretending to be a screenshot of macOS — we are evoking a window frame in our own visual language.

#### The signature: Live Changelog preview

The 6-second loop for the Live Changelog preview. Implementation:

```tsx
// _components/live-changelog-preview.tsx
const STAGES = [
  { stage: 0, requirement: "User can reset password via email", status: "active" },
  { stage: 1, requirement: "User can reset password via email", status: "editing" },
  { stage: 2, requirement: "User can reset password via email or SMS", status: "active",
    diff: { before: "via email", after: "via email or SMS" } },
  { stage: 3, requirement: "User can reset password via email or SMS", status: "wontDo",
    reason: "Replaced by SSO flow — see Feature: Auth Overhaul" },
];

export function LiveChangelogPreview({ variant = "compact" }: Props) {
  const [stage, setStage] = useState(0);

  useEffect(() => {
    const respectsReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (respectsReducedMotion) return;

    const intervals = [1500, 500, 2000, 2000]; // total 6s
    const id = setTimeout(() => setStage((s) => (s + 1) % STAGES.length), intervals[stage]);
    return () => clearTimeout(id);
  }, [stage]);

  return (
    <PreviewFrame>
      <RequirementCard {...STAGES[stage]} />
      <Timeline stages={STAGES.slice(0, stage + 1)} />
    </PreviewFrame>
  );
}
```

The preview must be deterministic, accessible (`prefers-reduced-motion` halts the loop), and visually identical to the real product UI.

### 12.4 Pricing page

- Three plan cards in a 3-column grid, max-width 1080px.
- Cards: `bg-slate-50`, `border-slate-200`, `rounded-lg`, `p-8`.
- Recommended plan: `border-2 border-indigo-500` with an `indigo-500` "Recommended" pill at the top.
- Each card shows: plan name (heading-md), price (display-md), 5 bullets, primary CTA.
- Below: FAQ in two-column layout — question (heading-md) left, answer (body-md) right.
- No "Contact us" tier until enterprise is genuinely needed.

### 12.5 Docs

Separate surface (`docs.grundet.app`), shares Foundation tokens.

- Two-column: left sidebar (240px), right content (max-width 720px).
- Body: `text-lg leading-relaxed` for comfortable reading.
- Code blocks: `bg-slate-900 text-slate-50 rounded-md font-mono text-sm p-4 border border-slate-700`. Copy button top-right (icon-only ghost button).
- Inline code: `bg-slate-100 text-slate-800 font-mono text-sm rounded-sm px-1.5 py-0.5`.
- Callouts:
  - Note: `bg-indigo-50 border-l-4 border-indigo-500 p-4 rounded-md`.
  - Warning: `bg-warning-50 border-l-4 border-warning-500 p-4 rounded-md`.
  - Tip: `bg-cool-sky-50 border-l-4 border-cool-sky-500 p-4 rounded-md`.

### 12.6 Changelog page

Grundet's own product changelog, rendered with the same Timeline + Diff View components as the product. Public, no auth, RSS at `/changelog/feed.xml`. Visiting this page is the most powerful credibility signal we can give — *we use our own tool to track our own changes*.

---

## 13. Voice & Copy Rules

### 13.1 Headline patterns

- **Direct claim.** "Requirements your team can actually trust."
- **Concrete enumeration.** "Every change. Every reason. Every version."
- **Named truth.** "Today, requirements live nowhere structured."

### 13.2 Microcopy

- Buttons: verb + noun ("Save requirement," not "Submit"). Or single verb ("Continue").
- Labels: noun, sentence case ("Acceptance criterion," not "ACCEPTANCE CRITERION:").
- Helper text: full sentence, end with period. "Atomic — one testable statement per requirement."
- Error messages: name what failed and what to do, in a single sentence.

### 13.3 Numbers & specifics

When making claims, be specific. We have no usage data yet (Grundet is pre-alpha), so we never invent numbers. We claim the *mechanism* honestly: "Every change is recorded — when something breaks, you know who changed what, when, and why."

### 13.4 Forbidden vocabulary

See §3.4.

---

## 14. Email Templates

Three templates in v1: **Magic-link sign-in**, **Team invitation**, **Password change confirmation**.

- Layout: 600px max-width, single column, centered.
- Background: `slate-100`.
- Body container: `bg-slate-50` with `1px solid slate-200`, `rounded-md`, `p-6`.
- Header: Strata mark + wordmark, centered, `pb-6`.
- Body: body-md, `slate-800` text.
- CTA button: inline-styled Indigo (`background: #610F7F`, `color: #FAFBFC`, `padding: 12px 24px`, `border-radius: 6px`, `font-weight: 500`). Email-client safe.
- Footer: caption-size legal/unsubscribe in `slate-500`.
- All emails set in `'Geist Sans', system-ui, sans-serif` with Geist as a fallback chain that gracefully degrades.

Template files live in `src/Grundet.Api/Email/Templates/` and are rendered server-side. Use plain HTML with inline styles — no email-framework dependency.

---

## 15. Social, Favicon, OG

### 15.1 Favicon

Strata mark only. Sizes: 16, 32, 48, 96, 192, 512 px.

- Light: `slate-50` background, mark in `slate-950`.
- Dark variant (for OS dark-mode awareness): `slate-900` background, mark in `slate-50`.
- 16px version: hand-tuned, bars on exact pixel boundaries, no anti-aliasing fuzz.

### 15.2 Open Graph cards (1200×630)

```tsx
// src/app/opengraph-image.tsx — generated via Vercel OG
import { ImageResponse } from 'next/og';

export const size = { width: 1200, height: 630 };

export default async function Image() {
  return new ImageResponse(
    (
      <div style={{
        width: 1200, height: 630,
        background: '#FAFBFC',
        padding: 80,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        fontFamily: 'Geist Sans',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          {/* Inline Strata SVG */}
          <span style={{ fontSize: 28, fontWeight: 500, letterSpacing: '-0.02em' }}>
            grundet
          </span>
        </div>
        <div style={{
          fontSize: 64, fontWeight: 600, lineHeight: 1.1,
          letterSpacing: '-0.035em', color: '#06080C', maxWidth: 900,
        }}>
          Requirements your team can actually trust.
        </div>
      </div>
    ),
    size
  );
}
```

### 15.3 Social profile assets

- **Avatars** (LinkedIn, X, GitHub): Strata mark on `slate-50`, 12% inner padding.
- **Cover images**: a single Live Changelog preview frame on a `slate-100` band, with wordmark + tagline in upper-left.

---

## 16. Implementation Reference

Everything below is copy-pasteable into the codebase.

### 16.1 `globals.css` — CSS variables

```css
/* src/app/globals.css */
@import "tailwindcss";

@theme {
  /* Slate (neutral spine) */
  --color-slate-50:  rgb(250 251 252);
  --color-slate-100: rgb(242 244 247);
  --color-slate-200: rgb(229 232 237);
  --color-slate-300: rgb(207 212 220);
  --color-slate-400: rgb(154 161 173);
  --color-slate-500: rgb(107 114 128);
  --color-slate-600: rgb(75 85 99);
  --color-slate-700: rgb(48 55 68);
  --color-slate-800: rgb(26 31 43);
  --color-slate-900: rgb(14 17 23);
  --color-slate-950: rgb(6 8 12);

  /* Indigo (primary brand accent) */
  --color-indigo-50:  rgb(251 245 252);
  --color-indigo-100: rgb(244 229 247);
  --color-indigo-200: rgb(229 198 236);
  --color-indigo-300: rgb(203 148 214);
  --color-indigo-400: rgb(167 91 183);
  --color-indigo-500: rgb(97 15 127);
  --color-indigo-600: rgb(83 11 110);
  --color-indigo-700: rgb(67 8 90);
  --color-indigo-800: rgb(50 7 68);
  --color-indigo-900: rgb(33 5 45);
  --color-indigo-950: rgb(20 3 28);

  /* Marketing accents */
  --color-cool-sky-50:  rgb(232 243 252);
  --color-cool-sky-100: rgb(200 226 247);
  --color-cool-sky-500: rgb(93 169 233);
  --color-cool-sky-700: rgb(31 111 176);

  --color-bubblegum-50:  rgb(255 233 236);
  --color-bubblegum-100: rgb(255 202 208);
  --color-bubblegum-500: rgb(255 105 120);
  --color-bubblegum-700: rgb(194 41 58);

  --color-emerald-50:  rgb(224 250 241);
  --color-emerald-100: rgb(182 242 221);
  --color-emerald-500: rgb(6 214 160);
  --color-emerald-700: rgb(2 138 102);

  --color-espresso-50:  rgb(244 232 229);
  --color-espresso-100: rgb(226 197 189);
  --color-espresso-500: rgb(85 27 20);
  --color-espresso-700: rgb(58 18 12);

  /* Functional */
  --color-success-50:  rgb(230 248 239);
  --color-success-500: rgb(22 163 74);
  --color-success-700: rgb(15 122 55);
  --color-warning-50:  rgb(254 246 225);
  --color-warning-500: rgb(217 119 6);
  --color-warning-700: rgb(146 82 10);
  --color-destructive-50:  rgb(253 233 233);
  --color-destructive-500: rgb(220 38 38);
  --color-destructive-700: rgb(164 28 28);

  /* shadcn semantic tokens */
  --color-background: var(--color-slate-50);
  --color-foreground: var(--color-slate-900);
  --color-card: var(--color-slate-50);
  --color-card-foreground: var(--color-slate-900);
  --color-popover: var(--color-slate-50);
  --color-popover-foreground: var(--color-slate-900);
  --color-primary: var(--color-indigo-500);
  --color-primary-foreground: var(--color-slate-50);
  --color-primary-hover: var(--color-indigo-400);
  --color-primary-pressed: var(--color-indigo-600);
  --color-secondary: var(--color-slate-100);
  --color-secondary-foreground: var(--color-slate-900);
  --color-muted: var(--color-slate-100);
  --color-muted-foreground: var(--color-slate-500);
  --color-accent: var(--color-indigo-50);
  --color-accent-foreground: var(--color-indigo-700);
  --color-destructive: var(--color-destructive-500);
  --color-destructive-foreground: var(--color-slate-50);
  --color-border: var(--color-slate-200);
  --color-input: var(--color-slate-300);
  --color-ring: var(--color-indigo-500);

  /* Typography */
  --font-sans: var(--font-inter), 'Inter', system-ui, -apple-system, sans-serif;
  --font-serif: var(--font-dm-serif), Georgia, serif;
  --font-mono: var(--font-geist-mono), 'JetBrains Mono', ui-monospace, monospace;

  /* Radii */
  --radius-sm: 4px;
  --radius-md: 6px;
  --radius-lg: 8px;
  --radius-xl: 12px;

  /* Shadows */
  --shadow-sm: 0 1px 2px rgb(14 17 23 / 0.04);
  --shadow-md: 0 4px 12px rgb(14 17 23 / 0.06), 0 1px 2px rgb(14 17 23 / 0.04);
  --shadow-lg: 0 16px 32px rgb(14 17 23 / 0.08), 0 4px 8px rgb(14 17 23 / 0.04);
  --shadow-xl: 0 32px 64px rgb(14 17 23 / 0.12), 0 8px 16px rgb(14 17 23 / 0.06);

  /* Motion */
  --duration-instant: 100ms;
  --duration-fast: 150ms;
  --duration-base: 200ms;
  --duration-medium: 300ms;
  --duration-slow: 400ms;
  --ease-out: cubic-bezier(0.2, 0, 0, 1);
  --ease-in: cubic-bezier(0.4, 0, 1, 1);
  --ease-in-out: cubic-bezier(0.4, 0, 0.2, 1);
}

@layer base {
  * {
    border-color: var(--color-border);
  }

  html {
    font-family: var(--font-sans);
    color: var(--color-foreground);
    background-color: var(--color-background);
    -webkit-font-smoothing: antialiased;
    text-rendering: optimizeLegibility;
  }

  ::selection {
    background-color: var(--color-indigo-100);
    color: var(--color-indigo-900);
  }

  /* Focus-visible globally — Indigo ring */
  :focus-visible {
    outline: 2px solid var(--color-indigo-500);
    outline-offset: 2px;
    border-radius: var(--radius-sm);
  }
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

### 16.2 `tailwind.config.ts`

In Tailwind 4, most theming happens via the `@theme` block in CSS (above). The config below is for plugin registration and content paths. If using Tailwind 3, port the `@theme` definitions to the `theme.extend` object.

```ts
// tailwind.config.ts (Tailwind v4 — minimal)
import type { Config } from 'tailwindcss';

export default {
  content: [
    './src/app/**/*.{ts,tsx,mdx}',
    './src/components/**/*.{ts,tsx}',
  ],
  // All theme tokens live in src/app/globals.css under @theme.
  // No theme.extend needed when using v4 CSS-first config.
} satisfies Config;
```

### 16.3 `package.json` — required dependencies

```json
{
  "dependencies": {
    "next": "^16.0.0",
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "geist": "^1.3.1",
    "lucide-react": "^0.460.0",
    "framer-motion": "^11.11.0",
    "class-variance-authority": "^0.7.1",
    "clsx": "^2.1.1",
    "tailwind-merge": "^2.5.4"
  },
  "devDependencies": {
    "tailwindcss": "^4.0.0",
    "@tailwindcss/postcss": "^4.0.0"
  }
}
```

### 16.4 `cn` utility

```ts
// src/lib/utils.ts
import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
```

### 16.5 shadcn install

```bash
pnpm dlx shadcn@latest init
# Pick: Tailwind v4, TypeScript, src directory, App Router.
# Style: "new-york" (more square corners, closer to Foundation).
# Base colour: pick "slate" then override via @theme above.
# CSS variables: yes.

pnpm dlx shadcn@latest add button input label badge card dialog dropdown-menu \
  popover tabs table toast tooltip separator scroll-area command sheet
```

After install, edit each component's `tailwind` classes to match §11 specs (or accept defaults and override variants in `cva` calls).

### 16.6 Project structure recommendation

```
src/
├── app/
│   ├── (marketing)/
│   │   ├── _sections/        # Hero, Problem, Pillars, etc.
│   │   ├── _components/      # PreviewFrame, LiveChangelogPreview
│   │   ├── page.tsx          # Landing
│   │   ├── pricing/page.tsx
│   │   ├── changelog/page.tsx
│   │   └── about/page.tsx
│   ├── (app)/                # Authenticated product UI
│   │   ├── projects/
│   │   ├── requirements/
│   │   └── changelog/
│   ├── (auth)/
│   │   ├── login/page.tsx
│   │   └── signup/page.tsx
│   ├── globals.css
│   ├── layout.tsx
│   └── opengraph-image.tsx
├── components/
│   ├── brand/
│   │   ├── strata-mark.tsx
│   │   ├── wordmark.tsx
│   │   └── logo.tsx
│   ├── ui/                   # shadcn primitives
│   ├── changelog/
│   │   ├── diff-view.tsx
│   │   ├── timeline.tsx
│   │   └── diff-reveal.tsx
│   └── empty-states/
└── lib/
    ├── utils.ts              # cn()
    └── time.ts               # relativeTime()
```

### 16.7 Accessibility checklist (build-time)

- All interactive elements have visible `:focus-visible` state in Indigo.
- All icons used standalone (no adjacent text) have `aria-label`.
- All form inputs have associated `<label>`.
- All buttons have `type="button"` unless inside a form's submit flow.
- All images have `alt` attributes (decorative ones get `alt=""`).
- Colour contrast verified — body 4.5:1 minimum, large text 3:1 minimum.
- Animations honour `prefers-reduced-motion`.
- All modals trap focus and restore on close.
- All dropdowns are keyboard-navigable (arrow keys, enter, escape).

---

## 17. Anti-Patterns — *do not do these*

A consolidated list. If you find yourself reaching for one of these, stop.

- Pure black (`#000000`) or pure white (`#FFFFFF`).
- Saturated indigo / purple gradients (the "AI startup" look).
- Warm beige neutrals (the Anthropic look — we deliberately chose cool slate).
- Editorial serif headlines (we deliberately chose pure sans).
- Gradient backgrounds in heroes or primary surfaces.
- Glassmorphism / frosted-glass panels.
- 3D blob illustrations or character mascots.
- Stock photography of teams laughing at laptops.
- Emoji in product copy or marketing headlines (functional checkmarks/crosses in diff views are acceptable).
- Bouncy spring animations.
- Auto-playing video hero.
- Cookie banners that block content.
- "Free forever" claims or "AI-powered" hype.
- Capitalised wordmarks ("GRUNDET").
- Pill-shaped buttons (the candy look — `rounded-full` is for avatars only).
- Drop shadows in pure black.
- More than one accent colour on a surface (indigo must remain solo in product UI).
- Marketing accents inside the authenticated product app.
- Using `font-serif` (`DM Serif Display`) in the authenticated product app — it is a marketing-only surface accent.
- Body text below 14px in the product UI.
- Rotating / spinning decorative elements.
- Confetti / celebration animations.
- Decorative icons that don't communicate state.

---

## 18. Open Questions

Flagged decisions deferred to a later round.

- **Final Indigo hex.** `#610F7F` is the committed signature. May be adjusted ±5° hue / ±5% saturation if visual testing shows it clashes with Bubblegum Pink (`#FF6978`) in the brand gradient or on the hero.
- **Logomark intro animation.** Should the Strata bars stack in from the left on first page load? If yes, ≤600ms, plays once per session.
- **Custom illustration set.** v1 ships with simple SVG empty states built from the Strata primitive. Decide later if a richer set (~6 illustrations) is worth commissioning.
- **Docs theme variant.** Currently shares the marketing surface (`slate-50` background). Consider a slightly higher-contrast docs variant (`slate-100` background) to differentiate.
- **Dark mode.** Token system reserves namespace; not built in v1.

---

## 19. Glossary — System Vocabulary

- **Strata** — the logo mark (three stacked bars), and the metaphor for layered/versioned requirements.
- **Indigo** — the brand accent colour (`#610F7F`, confident purple).
- **Slate** — the cool-tinted neutral scale.
- **Foundation** — the design system itself; "Grundet Foundation."
- **Cool Sky / Bubblegum Pink / Emerald / Espresso** — the four marketing accents. Marketing surfaces only.
- **Diff View** — the brand component; visual rendering of a Requirement version-to-version change.
- **Live Changelog** — the signature marketing animation; 6-second loop demonstrating Grundet's value with no copy.
- **Preview Frame** — the floating product-UI card on the marketing site.

---

## 20. Implementation Order (suggested)

For an agent or developer building this from scratch, this is the recommended sequence.

1. **Tokens.** Drop `globals.css` (§16.1) and `tailwind.config.ts` (§16.2) into the codebase. Verify the `@theme` block parses.
2. **Typography.** Install Geist via `next/font` (§7.2). Verify `font-sans` and `font-mono` apply.
3. **Brand primitives.** Build `<StrataMark>`, `<Wordmark>`, `<Logo>` (§5).
4. **shadcn install.** Run `shadcn init` and add the components listed in §16.5. Override variants to match §11.
5. **Product UI shell.** Build the top nav (§11.14) and sidebar. Wire the project switcher.
6. **Diff View + Timeline.** The brand components (§11.9, §11.10). Build before any marketing work — the marketing site reuses them.
7. **Marketing layout.** `(marketing)` route group, top nav, footer.
8. **Hero + Live Changelog preview.** §12.2 Section 1 + §12.3.
9. **Remaining marketing sections.** Problem, Pillars, How-teams-use-it, Integrations, CTA.
10. **Pricing, About, Changelog, Login/Signup.**
11. **Docs site** at `docs.grundet.app` — separate Next.js app or MDX route group.
12. **Email templates** (§14).
13. **OG image generator** (§15.2).
14. **Accessibility audit** against §16.7.

> The success criterion for this whole system is simple: a senior engineer or PM director, looking at Grundet cold, says **"this is serious software, made by serious people."** Not playful (in the product). Not enterprise-bland. Not generic-startup. Engineering-precise. Confident. With a single electric accent that says: *Grundet acts.*
