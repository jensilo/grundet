# Code Map — Frontend (`src/Grundet.Web`)

**Last verified:** 2026-07-11 · Refresh commands: [Maintenance](#maintenance)

`proxy.ts` (not `middleware.ts`) maps domains to directory trees: `grundet.*` → `app/marketing/`, everything else → `app/my/`. Browser URLs never contain `/my` or `/marketing`. Details: `before-frontend`.

## URL → file (app domain, under `app/my/`)

| Browser URL | File |
|---|---|
| `/login`, `/onboarding`, `/auth/error` | `login/`, `onboarding/`, `auth/error/` — public, **outside** `(authenticated)/` |
| `/invite/accept` | `invite/accept/route.ts` (route handler, not a page) |
| `/dashboard` | `(authenticated)/dashboard/page.tsx` — Home |
| `/projects` | `(authenticated)/projects/page.tsx` |
| `/projects/{id}/features` | `(authenticated)/projects/[projectId]/features/page.tsx` |
| `/projects/{id}/requirements[/new \| /{reqId} \| /{reqId}/edit \| /{reqId}/history]` | `…/[projectId]/requirements/**` — Intent browse/composer/detail/history |
| `/projects/{id}/work-items[…]` | `…/[projectId]/work-items/**` — same shape as requirements |
| `/projects/{id}/search`, `/projects/{id}/changelog` | `…/search/page.tsx`, `…/changelog/page.tsx` |
| `/projects/{id}/ref/{reference}` | `…/ref/[reference]/page.tsx` — resolves `IN-n`/`WO-n`/`#n` and redirects |
| `/teams`, `/teams/{id}`, `/teams/{id}/projects` | `(authenticated)/teams/**` — team settings panels in `teams/[teamId]/_components/` |
| `/profile` | `(authenticated)/profile/page.tsx` |
| anything else | `(authenticated)/[...path]/page.tsx` → in-shell `notFound()` |

Marketing (`app/marketing/`): `/` (landing), `/about`, `/contact`, `/join-alpha`, `/privacy`, `/tos`. `app/design/` is a dev-only component gallery (404s in production). Auth gate + `displayName`/onboarding redirects: `(authenticated)/layout.tsx`. Route-private components live in a sibling `_components/` dir.

## API client — `lib/api/`

| Path | Role |
|---|---|
| `generated/` | Output of `make generate-api` — never hand-edit. `sdk.gen.ts` = one function per operation; `types.gen.ts` = DTO types |
| `server.ts` | `createServerClient()` — per-request client for RSC pages, forwards session cookie |
| `client.ts` | Re-exports the browser singleton; configured in `providers/api-config-provider.tsx` (`credentials: "include"`) |
| `mutation-error.ts` | Maps backend `ProblemDetails.type` URIs (mirror of `ProblemTypes.cs`) to user-facing copy |
| `queries/` | Shared multi-call fetch helpers (`teams.ts` → `fetchTeamsWithProjects`, `projects.ts`, `features.ts`) |

## Shared machinery

| Path | Role |
|---|---|
| `components/ui/` | Base UI primitives — compose, never modify |
| `components/app/` | Product components: shell (`app-sidebar`, `app-topbar`, `mobile-nav-drawer`), browse (`browse-filter-panel`, `browse-pagination`, card grids), version history (`version-entry`, `version-timeline`, `version-diff-dialog`, `diff-block`), references (`reference-text`, `reference-link`), attachments (`attachment-gallery`, `attachment-uploader`, `lightbox-image`), `command-palette` (⌘K search) |
| `components/app/composer/` | The shared Intent/Work composer: `composer-fields`, `label-chip-input`, `acceptance-criteria-editor`, pickers, `use-draft-autosave` (→ Drafts endpoints via `draft-envelope.ts`) |
| `components/brand/`, `components/marketing/` | Logo/wordmark; marketing sections (shared container: `marketing/layout.ts`) |
| `hooks/use-filters.ts` | URL-searchParams filter state for browse views |
| `lib/references.ts` | `IN-`/`WO-`/`#n` tokenizer behind the reference overlay |
| `lib/word-diff.ts`, `version-change-comparison.ts`, `changelog.ts` | Hand-rolled LCS word diff + version/changelog shaping |
| `lib/text-limits.ts` | Client mirror of backend `TextInput` caps — keep in sync |
| `lib/domains.ts` | Cross-domain absolute-URL builders (`buildAppUrl`, …) |
| `lib/utils.ts` (`cn`), `toast.ts`, `format-date.ts`, `user.ts`, `avatar-palette.ts`, `onboarding-guides.ts` | Small single-purpose helpers; tests colocated |

## Tests

Vitest + testing-library, colocated `*.test.ts(x)` next to source (`vitest.config.ts`). RSC page guard-test pattern: see `before-frontend` → Testing. Strategy: `docs/test-strategy.md`.

## Maintenance

Update when an app route is added/removed or the `lib/`/`components/` layout shifts. Verify: `fd 'page.tsx|route.ts' src/Grundet.Web/app`.
