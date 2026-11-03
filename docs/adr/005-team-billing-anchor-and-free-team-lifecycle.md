# 005 — Team model, billing anchor, and free team lifecycle

**Date:** 2026-05-26  
**Status:** Accepted

## Context

Grundet is structured around Teams (`Team → Project → Feature → Requirement`). Without a team, the product is unusable. Two questions had to be resolved before billing can be introduced:

1. **Where does billing attach — user or team?** The answer determines how plans, quotas, and enforcement are modelled throughout the codebase.
2. **What happens at signup?** Users must be in a team to do anything. Making them manually create one before seeing value adds unnecessary friction.

These are not independent: the answer to (1) shapes what auto-creating a team at signup means, and (2) determines what the "free tier" anchor looks like.

## Decision

**Billing is team-scoped.** A plan (and eventually a Stripe subscription) attaches to a `Team`, not a `User`. Quota checks always ask "what does this team's plan allow?" — never "how many teams does this user have?" with one exception (see below).

**Auto-create a team on signup.** When a new user account is created (on first magic-link verification), a team named "My work" is automatically created and the user is added as Lead. This team starts on the free Solo plan. The name is a sensible default; the user can rename it at any time.

**Max 1 Solo workspace per user.** A user may own/lead at most one non-collaborative Solo workspace at a time. This is the single user-level quota check in the system and exists to keep the product and pricing model simple: every user can have one personal workspace; additional workspaces are collaborative Teams. Being a *member* of other teams (invited by someone else) does not consume this quota.

**Plan structure:**

| Tier | Plans | Collab |
|---|---|---|
| Solo workspace | Solo (free), Power, Expert | No — single owner/Lead only |
| Team workspace | Team S, Team M, Team L | Yes — multiple members |
| Internal/Admin | TeamAlpha, TeamUnlimited | Yes — admin-granted only |

Solo plans upgrade the user's single personal workspace. Team plans unlock member invites and are the only way to create additional workspaces. There are no paid "extra Solo teams"; if a user wants another workspace, it is a Team workspace and must go through checkout once billing is active.

**Manual creation rules.** New users get "My work" automatically. If they later delete it, leave it, transfer it, or convert it to a Team workspace, Grundet does not silently recreate it. If the user currently has no Solo workspace, the UI may show an explicit notice in team/workspace management: "You don't have a personal workspace. Create one." Creating it is a deliberate one-click action and creates a free Solo workspace named "My work" by default. Creating any additional workspace requires selecting a Team plan.

**Solo-to-Team transitions.** A Solo workspace may be upgraded to a Team workspace. A Team workspace may only be downgraded back to a Solo plan when it has exactly one member; otherwise collaboration state would be silently invalidated.

**Alpha and admin-granted plans.** During Alpha, newly created Team workspaces may be placed on `TeamAlpha`: an unlimited, non-billable Team plan with Team L-or-better functionality and no practical limits. `TeamAlpha` exists to make early evaluation possible before checkout is live. It is retired before production billing; affected teams must choose a normal Team plan or be explicitly moved by an admin. `TeamUnlimited` is separate and remains admin-granted only for testing, demos, friends/family, and exceptional internal use.

**MVP scope.** The data model seam (`Team.Plan` field, `ITeamQuotaService`) is built in the MVP so that billing can be wired in later without schema or structural changes. Stripe integration is post-MVP. The one-Solo-workspace-per-user rule is enforced in MVP. Other quota enforcement may remain permissive during Alpha/early MVP through `TeamAlpha`/`TeamUnlimited` or the quota stub, but all resource creation still flows through `ITeamQuotaService`.

## Implementation notes

- `Team.Plan` column (`TeamPlan` enum, default `Solo`) added to the Teams table. Pre-deployment: extend `InitialMigration` rather than adding a new migration.
- `ITeamQuotaService` is the enforcement seam. The stub reads `Team.Plan` and returns hardcoded `TeamLimits` (max projects, max requirements per project, max members, AI enabled/budget). Stripe wires in post-MVP by replacing or decorating this implementation.
- Auto-create logic lives in `AuthService.VerifyMagicLinkAsync` — when `isNewUser`, create the team and add the Lead membership in the same transaction as user creation.
- Frontend surfaces upgrade prompts at hard quota walls (project creation, member invite) and the personal-workspace nudge in team/workspace management when the user has no Solo workspace.
- Toast/notification infrastructure (shadcn Sonner) is used for the dashboard nudge. Not a new dependency — Sonner ships with shadcn/ui.

## Alternatives Considered

**User-scoped billing:** Plans attach to User, quotas apply globally across all teams. Rejected — complex per-user quota counting, wrong abstraction for a collaboration tool, and harder to price transparently (users can't reason about "what does this team cost?").

**`IsPersonal` flag on Team:** Mark auto-created teams as personal, hide them from the UI, treat them specially. Rejected — unnecessary abstraction. A team is a team. The user-facing model is simpler and more honest without a hidden tier.

**No auto-create at signup:** Users manually create their first team after onboarding. Rejected — the product is inert without a team; requiring an explicit creation step before seeing any value is unnecessary friction for a near-universal action.

**Silent auto-create of a second free Solo workspace:** When a user promotes "My work" to a Team plan, quietly create a new free Solo workspace. Rejected — creates records without user consent, potentially unwanted ("Karteileichen"), and the scenario is rare enough that an explicit one-click nudge is sufficient.

**Paid extra Solo teams:** Allow users to create multiple non-collaborative Solo workspaces on paid Solo plans. Rejected — uncommon, harder to explain, and blurs the clean pricing rule. A user gets one Solo workspace; additional workspaces are Team workspaces.

## Consequences

- All quota enforcement flows through `ITeamQuotaService`. No endpoint or service may hardcode plan limits inline.
- `Team.Plan` is the source of truth for limits. It is a simple enum column; no billing-provider SDK is required to read it.
- Signup always produces a team. Any code path that assumes a newly authenticated user may have zero teams is incorrect.
- The 1-Solo-workspace-per-user check is a user-level guard, not a team-level one. It lives in the team/workspace creation and plan-transition flows, checked before `ITeamQuotaService`.
- New non-Solo workspaces are Team workspaces. In Alpha they may receive `TeamAlpha`; in production they require checkout unless an admin grants `TeamUnlimited`.
- When Stripe is integrated, `TeamSubscription` rows (or Stripe metadata) back-fill `Team.Plan` on webhook events. The rest of the codebase is unaffected.
- UI must handle "at quota" states gracefully everywhere a team resource is created: upgrade prompt, not a silent failure.
