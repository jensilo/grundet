# Code Map

File-level index of the codebase: which file owns a route, where cross-cutting helpers live, which page renders a URL. Rules and conventions live in the briefing skills (`.agents/skills/before-*`) and `docs/memory.md` — the map tells you **which files** they apply to.

**Load on demand, and load only the part you need.** This is a lookup layer, not session-start reading.

| Part | Load when |
|---|---|
| [backend.md](backend.md) | Locating backend code — slice → routes, cross-cutting helpers, data layer, request pipeline, write flows, tests |
| [frontend.md](frontend.md) | Locating frontend code — URL → page file, API client paths, shared machinery, tests |
| [contract.md](contract.md) | Changing any endpoint the frontend consumes — the DTO → OpenAPI → generated-client chain |
| [repo.md](repo.md) | Top-level orientation — what each directory is, make targets, checks, CI |

Keep it a map, not a spec: locations and flow shapes here; field lists in `docs/architecture.md`; product status in `docs/feature.md`; rules in `docs/memory.md` and the briefing skills. Each part carries its own refresh commands under Maintenance.
