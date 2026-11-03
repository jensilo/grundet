---
name: before-frontend
description: Briefing skill for frontend work. Covers domain routing (proxy.ts, not middleware.ts), two-domain architecture (my.grundet.app/grundet.app), route group structure, navigation rules, RSC conventions, component model, TypeScript rules, Tailwind discipline, and API client usage. Also instructs reading docs/design/ruleset.md for design constraints. Run before touching any file in src/Grundet.Web/.
---

# Frontend Briefing — Grundet Web (Next.js 16 App Router)

Read this before touching any file in `src/Grundet.Web/`.

**Also read `docs/design/ruleset.md`** before writing any className, component, or UI copy. The design ruleset is the single source of truth for colour, typography, spacing, and component patterns.

**Also read `src/Grundet.Web/AGENTS.md`** — this version of Next.js has breaking changes from training data.

**File-level lookup, on demand** — URL → page file, directory roles, API client paths, shared component machinery: `docs/codemap/frontend.md`. Load it when you need to locate code, not as part of this briefing; skip it when you already know your target files.

---

## Domain routing — `proxy.ts`

**This project does not use `middleware.ts`.** Next.js 16 renamed the convention to `proxy.ts` with `export function proxy(request: NextRequest): NextResponse`. Do not create or reference `middleware.ts`.

### Two domains, two directory trees

| Domain | Local dev | Physical directory |
|--------|-----------|-------------------|
| `grundet.app` / `www.grundet.app` | `grundet.test` | `app/marketing/` |
| `my.grundet.app` | `my.grundet.test` | `app/my/` |

`proxy.ts` reads the `Host` header and **rewrites** the path transparently — the browser URL is unchanged:
- `grundet.*` or `www.grundet.*` → prepends `/marketing` (e.g. `/about` → serves `app/marketing/about/page.tsx`)
- All other hosts → prepends `/my` (e.g. `/dashboard` → serves `app/my/dashboard/page.tsx`)
- Exception: bare `/` on the app domain (`my.*`) **redirects** to `/dashboard`

The `/my/` and `/marketing/` prefixes are internal implementation details. They never appear in browser URLs or in `next/link` hrefs.

### Route layout under `app/my/`

```
app/my/
  (authenticated)/     ← auth-guard layout; unauthenticated → redirect /login
    dashboard/
    profile/
    teams/
    projects/
    …
  auth/
    error/             ← /auth/error?reason=…  (public)
  login/               ← public
  onboarding/          ← public
```

`(authenticated)/layout.tsx` calls `getMe` and redirects to `/login` when unauthenticated, and to `/onboarding` when `displayName` is missing. Pages that must be reachable without a session (login, onboarding, auth error) live **outside** that group — never move them inside it.

### Navigation rules

**Use relative paths everywhere.** `<Link href="/dashboard">` and `redirect("/login")` are always correct from app pages. The proxy handles the rest. Never write `/my/dashboard` or `/marketing/about` in code — and never write bare `/marketing` either; it is an internal rewrite target, not a navigable URL.

**`/` is the universal home link.** On the marketing domain the proxy rewrites it to `/marketing`; on the app domain it redirects to `/dashboard`. Use `<Link href="/">` for any "go home" link — no domain logic needed.

**`app/not-found.tsx` renders on both domains.** The global not-found handler has no knowledge of which domain triggered it. All navigation inside it must use relative paths — never internal prefixes like `/marketing` or `/my/`.

**Never redirect between domains for equivalent paths.** `my.grundet.app/pricing` and `grundet.app/pricing` are independent routes on independent sub-trees. A user hitting an unknown path on the app domain gets a 404 — they are not silently forwarded to the marketing domain. Redirecting between domains for path-equivalence breaks navigation history, breaks the back button, and serves no user value.

**Cross-domain links** (e.g. "Back to homepage" from the app to marketing) use **absolute URLs**, not `next/link`:
```tsx
<a href="https://grundet.app">Home</a>
```
`next/link` assumes same-origin and must not be used for cross-domain navigation.

---

## Architectural role — representation layer

Next.js/React is primarily the representation layer. Business logic, data shaping, and arithmetic belong in C# on the backend — not in components, hooks, or utilities. If you find yourself computing derived facts from API responses (totals, filtered counts, state transitions), stop and ask whether the backend should be providing that value directly. The API contract is the boundary; the frontend renders what the API gives it.

## Component model

- RSC by default. Add `"use client"` only for event handlers, browser APIs, or React hooks.
- Push `"use client"` to the leaf — keep Client Components as small as possible.
- `loading.tsx` and `error.tsx` at every meaningful route level.

## Data fetching

- Fetch in Server Components; pass data down as props.
- **Generated API client only.** Never handwrite `fetch()` calls or call `serverClient.get(rawUrl)` directly. If the generated client can't express the call or emits `unknown`, fix the backend DTO/OpenAPI annotation first, regenerate the client, then adjust the frontend. Do not patch around a bad contract with local request-body wrappers, `as unknown` casts, or frontend-defined pseudo-contracts. Rare exceptions require an explicit comment explaining why no generated path exists.
- Regenerate the client with `make generate-api`, not ad-hoc `openapi-ts` commands. Ensure the backend serving the current worktree is running first (`make backend` or `make dev`). If using a temporary backend URL, run `OPENAPI_SPEC_URL=http://host:port/openapi/v1.json make generate-api`.
- `cache: 'no-store'` for dynamic data; `next: { revalidate }` for ISR.
- No `useEffect` for data fetching.

## API client — two-path architecture

Two clients, both intentional:
- **RSC pages (Server Components):** use `serverClient` — created per-request from the incoming session cookie.
- **Client Components (browser):** use the singleton configured by `ApiConfigProvider` with `credentials: "include"`.

Do not unify them or introduce a third path. Do not use `as unknown` body casts — if the generated type is wrong, the fix is upstream (backend DTO → regenerate).

## Pagination

All paginated responses return `{ items, page, pageSize, total, hasMore, totalPages }`. Use `hasMore` and `totalPages` directly from the response — do not recompute them on the client. Next.js components are the representation layer; business arithmetic belongs on the backend.

## Forms and mutations

- Server Actions for form submission where possible.
- Mutation errors must be specific and actionable. Inspect generated `ProblemDetails.type`/`detail` and map known business-rule failures to helpful copy and next steps. Do not show "Failed. Please try again" for non-retryable states such as forbidden actions, quota limits, duplicate records, invalid ownership, or plan restrictions.
- Generic retry copy is only acceptable for likely transient load/network/server failures. If the backend does not expose enough canonical error information, fix the backend/OpenAPI contract first instead of guessing in the UI.
- **TanStack Query is not in this project** (deliberately deferred — see `docs/memory.md` → Open Threads). Do not add it. Client-side async state uses `useState`/`useTransition` or Server Actions. The threshold to introduce it: debounced client-side refetch, polling, reliable optimistic-update rollback, or the same loading-state boilerplate in 3+ components.

## Component library

- `components/ui/` — Base UI primitives (button, input, card, dialog, dropdown-menu, table, tabs, etc.)
- `components/app/` — Product app components (sidebar, topbar, requirement-row, status-badge, etc.)
- `components/brand/` — Logo, strata-mark, wordmark
- `components/changelog/` — diff-view, timeline
- `components/marketing/` — marketing section components

**Check the component tree first.** Before writing any className string, audit these directories. Use existing components. Extend via `className` prop. Never duplicate.

**Do not modify `components/ui/` files directly.** Compose them.

## Styling

- Tailwind only. No CSS modules, no styled-components.
- Semantic tokens in components: `text-foreground`, `bg-background`, `bg-card`, `bg-muted`, `border-border`, `bg-primary`, etc.
- Palette tokens (`text-slate-900`, `bg-indigo-50`) only for diff tokens, specific badge variants, brand-locked elements.
- Always use `cn()` from `@/lib/utils`. Never concatenate classNames with `+` or template literals.
- Spacing scale: `p-1` (4px), `p-2` (8px), `p-3` (12px), `p-4` (16px), `p-6` (24px), `p-8` (32px). Avoid off-grid values.
- **Do not manually order Tailwind classes.** `prettier-plugin-tailwindcss` enforces a canonical class order. Write classes in whatever order is readable — Prettier will reorder on format, and `prettier --check` will fail if they don't match.

## Simplicity — default to less

The default failure mode of AI-generated code is over-engineering: unnecessary wrapper components, abstractions for a single use case, hooks that encapsulate two lines of logic. Before extracting a component or hook, ask: is this genuinely reused or does the problem force the abstraction? Two instances is not enough. Three identical patterns is. If neither the problem nor the spec requires it, keep it inline.

## TypeScript

- `strict: true`. Always.
- No `any`. Use `unknown` + narrowing.
- No non-null assertions (`!`) unless documented.
- Explicit return types on exported functions in `.ts` files. Not required in `.tsx` — React component return types are inferred.
- Absolute imports (`@/...`). No barrel `index.ts` unless a genuine public API.

## Import order

`simple-import-sort` is active and enforced as an ESLint error. Imports must be ordered:
1. External packages (`react`, `next/...`, third-party)
2. Internal `@/` paths
3. Relative paths (`./`, `../`)

Alphabetical within each group. Unsorted imports fail the lint check.

## Performance

- `next/image` and `next/link`.
- `next/dynamic` for heavy Client Components not needed at first paint.

## Fonts

Do NOT import fonts via CSS `@import` or `<link>`. Font variables are injected on `<html>` in `app/layout.tsx`.

## Dark mode

Do not add `dark:` variant classes speculatively. The `.dark {}` block is reserved and empty.

## Formatting conventions

UI copy uses spaced en dashes only: `some – text in the – middle`.

Prettier config: `singleQuote: false`, `semi: true`, `trailingComma: "es5"`, `printWidth: 100`.

- **Double quotes**, not single. `"value"` not `'value'`.
- Semicolons required.
- Trailing commas in objects, arrays, and destructuring (not function parameter lists).

`prettier --check` runs in CI. A single-quote string or missing trailing comma fails the check.

## Testing

- **Done includes tests.** A change is not done until the breakage paths it touches are covered, committed in the same change — see `docs/test-strategy.md` → "Done includes tests". New untested breakage paths are tracked as debt in `docs/work-state.md`.
- Vitest + `@testing-library/react` for unit and component tests.
- Test files colocated with source: `lib/utils.test.ts` next to `lib/utils.ts`, `components/foo.test.tsx` next to `components/foo.tsx`.
- Test behaviour, not implementation: query by role, label, or visible text — never by class name or internal state.
- Never use array index as `key` in JSX lists (`key={index}`). Use a stable ID from the data.
- **Client Components** (`"use client"`): render with `@testing-library/react` in jsdom.
- **Server Components (RSC)**: test as plain async functions — call the component directly and assert on the JSX or data it returns. No special render wrapper needed. Next.js 16 passes `params`/`searchParams` as **Promises**, so call as `Page({ params: Promise.resolve({...}) })`. Mock `@/lib/api/server` (`createServerClient`) and `next/navigation` with a **throwing** `notFound`/`redirect` so a guard firing is an assertable throw. See the `app/my/(authenticated)/projects/**/page.test.tsx` guard tests for the copy-paste mock block.
- Do not test Tailwind class output. Test content and behaviour.
- Use `@testing-library/user-event` for interactions (click, type) — not `fireEvent`.
