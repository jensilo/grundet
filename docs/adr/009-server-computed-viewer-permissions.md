# 009 — Server-computed viewer permissions

**Date:** 2026-06-25
**Status:** Accepted

## Context

The frontend re-derives authorization client-side to decide what UI to show — e.g. the team page computes `isLead`, `isSoloWorkspace`, `canInviteMembers`, `canManageTeam`, `canDeleteTeam` from `listTeamMembers` + `getMe` + `getTeamLimits` (`teams/[teamId]/page.tsx`). This logic mixes **team role** and **billing plan**, duplicates backend rules, and drifts from them silently: the real gates live in `TeamService` (delete → 409 on solo, `TeamService.cs:90`) and `TeamQuotaService` (invite → 409 unless plan allows collaboration, `TeamQuotaService.cs:88`). Response DTOs carry no permission metadata, so the UI cannot know in advance whether an action is allowed.

## Decision

Compute viewer capabilities **server-side** and ship them to the frontend as a typed permission model:

- A plain `Permission` enum (string-serialized) names discrete capabilities.
- A **pure policy** maps `(TeamRole, TeamPlan) → Permission[]`, reusing the canonical `TeamPlanExtensions` predicates so the hint mirrors the real enforcement gates.
- An `IPermissionService` composes the viewer's role (via `IResourceAuthorizationService`) and plan (via `ITeamQuotaService`) and calls the policy — through the standard endpoint → service → store layering.
- Delivery is a **dedicated per-resource endpoint** (`GET /teams/{teamId}/permissions` first), consistent with the sibling `GET /teams/{teamId}/limits`, returning `{ permissions: Permission[] }`. The frontend renders options from this set instead of deriving them.

**The permission model is a UI hint, not the security boundary.** Backend enforcement is unchanged and remains the only authority: every mutation still passes through `IResourceAuthorizationService` (403) and the relevant business gates (403/409). A bug in the hint can hide or wrongly show a button; it can never grant access. Tests assert parity in both directions (a granted permission ⟺ its backend op won't 403/409).

Scope is team-level now. Per-resource permissions (requirements, features, work items) are **not** built yet — today any Contributor has full CRUD, so such flags would be uniform. The enum + policy pattern is the extension point when per-resource roles actually exist. The constraint that must hold when that happens is below.

## Forward constraint — backend enforcement must converge on the permission model

Today there are deliberately **two authorization shapes**: backend enforcement is **role-based** (`RequireTeamAccessAsync(userId, teamId, TeamRole.Lead)` at each endpoint, plus plan gates in `TeamService`/`TeamQuotaService`), while the shipped `Permission[]` model is a **finer-grained capability vocabulary used only as a frontend hint**. This is coherent **only while capabilities are role-uniform** — i.e. while `(role, plan) → capabilities` is a total static mapping with no per-instance variation. In that regime the policy can faithfully mirror the role gates, and the parity tests prove it.

The moment **per-resource permissions** exist — e.g. a Contributor who may edit Intent A but not Intent B, or a role with capability subsets that depend on the specific resource — that equivalence breaks:

- Role-based backend enforcement (`TeamRole.Lead` at the endpoint) can no longer express the gate.
- A granular frontend hint would then predict an authorization the backend **does not actually evaluate** — a real divergence, not a hint-vs-boundary nuance. The UI could offer (or hide) actions the backend cannot correctly allow or deny.

**Therefore, when per-resource permissions are introduced, the permission policy (`TeamPermissionPolicy` or its successor) MUST become the single evaluator invoked by BOTH the enforcement path and the hint endpoint.** Backend mutations must authorize against the permission model itself, not against coarse roles. Shipping per-resource *frontend* permissions without first moving *backend enforcement* onto the same permission model is prohibited — it would institutionalize the two-system split this ADR currently keeps benign.

The existing parity tests guard the **role-era mirror** (granted hint ⟺ role/plan op won't 403/409). They do **not** guard this structural shift; the convergence above is a design obligation that lands with the first per-resource capability, not something a test will catch after the fact. See the locked entry in `docs/memory.md` (Architectural Decisions).

## Alternatives Considered

- **Keep client-side derivation** — rejected: duplicates and silently drifts from backend rules; leaks billing-plan logic into the UI.
- **Embed `viewerPermissions` in `TeamResponse`** — rejected: inconsistent with how the sibling plan-derived concern (limits) is delivered, and pollutes a 3-field record reused by create/get/rename. Per-resource embedding stays open as a future option where data and permissions naturally travel together.
- **A generic capability/policy registry** — rejected as over-engineering for two roles and one resource; a static pure policy is enough.

## Consequences

- Single server-side source of truth for `(role, plan) → capabilities`; the frontend stops inferring authorization.
- One extra request per page (mirrors the existing limits fetch; negligible inside the page's `Promise.all`) and a second source of truth versus the data it gates — accepted for separation/consistency.
- Enforcement parity must be maintained: when an enforcement gate changes, the policy and its parity test change with it. This is the standing risk of a hint layer and is covered by tests.
- Establishes the contract (`Permission[]` via dedicated per-resource endpoints, never the security boundary) that future resource permissions follow.
