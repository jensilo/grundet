# Code Map — Repo & Toolchain

**Last verified:** 2026-07-11

## Top level

| Path | What it is |
|---|---|
| `src/Grundet.Api/` | Backend — .NET 10 Minimal API. Skill: `before-backend`; map: [backend.md](backend.md) |
| `src/Grundet.Api.Tests/` | Backend integration tests (xUnit + Testcontainers PostgreSQL) |
| `src/Grundet.Web/` | Frontend — Next.js 16 App Router. Skill: `before-frontend`; map: [frontend.md](frontend.md) |
| `src/Grundet.AdminCli/` (+ `.Tests`) | `grundet-admin` operator CLI, direct-DB. Skill: `before-admin-cli`; docs: `docs/admin-cli/` |
| `src/Grundet.Skills/` | **Product** skill shipped to Grundet users (MCP read-intent). Not the repo's dev skills |
| `tools/skills-gen/` | Zip generator for the product skill (`make skills`) |
| `.agents/skills/` | Internal dev skills: briefings, verification, personas |
| `.pi/checks/` | Fast checks: `backend.sh` (changed-file format), `backend-build.sh` (`-warnaserror` builds), `frontend.sh` (format+lint) |
| `scripts/` | `verify-backend.sh`, `verify-frontend.sh` (full slice checks), `seed-dev.sql`, `db/scenarios/`, DB backup/restore |
| `infra/compose.dev.yml` | Dev stack: PostgreSQL 18 + maildev |
| `.github/workflows/` | `backend.yml`, `frontend.yml`, `skills.yml`, `skills-release.yml` |
| `Makefile` | Dev entry points — below |
| `docs/` | Knowledge base — index in `docs/README.md` |

## Dev and verification toolchain

| Command | What it does |
|---|---|
| `make dev` / `make dev-fresh` | Full stack: infra + migrate + seed + backend watch (`:8080`) + frontend (`grundet.test` hosts) |
| `make backend` / `make frontend` | One side only |
| `make migrate` | `dotnet ef database update` |
| `make generate-api` | Regenerate the TS client (backend must be running) |
| `make infra` / `infra-reset` | Dev Postgres + maildev via `infra/compose.dev.yml` |
| `/check backend` / `/check frontend` | Fast changed-file checks (`.pi/checks/`) — while working |
| `/check backend-build` | All C# projects, `-warnaserror`, no tests |
| `format-lint-test` skill | Full proof: `scripts/verify-backend.sh` + `scripts/verify-frontend.sh` — before handoff |
| `make skills` | Build the product skill zip (`tools/skills-gen`) |

Frontend package manager is **pnpm** (Node 24); backend config contract: `docs/infra/backend-configuration-contract.md`.

## Maintenance

Update when top-level layout, make targets, or CI workflows change. Verify: `eza -la --git` at root, `ls .github/workflows`, and read the `Makefile`.
