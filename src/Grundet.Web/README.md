# Grundet.Web

> **Design system only — authentication, data fetching, and state management are not yet implemented.**

Next.js 16 (App Router) frontend for Grundet.

## Stack

- Next.js 16 (App Router)
- TypeScript
- Tailwind CSS v4
- shadcn/ui (base-nova style, neutral palette via `@base-ui/react`)

## Getting Started

```bash
pnpm install
pnpm dev
```

Visit [http://localhost:3000/design](http://localhost:3000/design) for the component gallery.

## Structure

```text
app/
  layout.tsx        Root layout (nav shell + TooltipProvider)
  page.tsx          Home placeholder
  design/
    page.tsx        Component gallery — primary design system testbed
components/
  site-nav.tsx      Global nav bar
  ui/               shadcn/ui components (do not edit manually)
lib/
  utils.ts          cn() helper
```

## Intentionally Omitted

- Authentication (no next-auth, no cookie handling)
- Data fetching (no react-query, no SWR, no server actions wired to the API)
- State management (no zustand, no context providers)

These will be added in subsequent phases once the design system is validated.
