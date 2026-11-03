# 008 — Per-Team API keys: MCP-only, acts-as-issuing-user

**Date:** 2026-06-23
**Status:** Accepted

## Context

Agents (Claude Code and remote/web MCP clients) need to read versioned intent from
Grundet outside the cookie + magic-link browser session. Today the only credential is a
cookie tied to a `ClaimsPrincipal "sub"` that drives `RlsContext.UserId` and Postgres RLS.
There is no API-key, bearer, or service-account concept.

We want agent access in alpha **without** building the eventual native identity model. The
real model — a first-class `Actor` supertype distinct from `User`, with its own permissions —
is a larger, hard-to-reverse decision that we are deliberately deferring (see `docs/ideas.md`).

The constraint that shapes everything: a non-cookie credential is new attack surface. We want
the smallest surface that still proves the security model end-to-end.

## Decision

Introduce a **per-Team API key** with these properties:

1. **Acts as the issuing user.** A key carries `CreatedByUserId`. On a request it populates the
   same `"sub"` claim and `RlsContext.UserId` a cookie would, so the entire existing
   authorization + RLS stack (`IResourceAuthorizationService`, `AuthorizedId`, RLS policies)
   applies **unchanged**. The key is *attribution + transport*, not a new identity.

2. **Scoped to exactly one Team.** A key carries `TeamId`. A dedicated team-scope guard asserts
   any requested team equals the key's `TeamId` before any service/store call, so client-supplied
   identifiers can only ever *narrow* within the key's team, never broaden it.

3. **MCP-only in alpha.** The `ApiKey` authentication scheme is accepted **only** on the `/mcp`
   endpoint. The general authenticated HTTP API stays cookie-only. This confines team-scope
   enforcement to one small tool surface instead of every route.

4. **Read-only in alpha.** No write/mutation MCP tools. The append-only versioning invariant is
   untouched.

5. **Role is metadata only.** The key stores a `TeamRole` snapshot at creation for forward
   compatibility, but authorization uses the issuing member's **current** `TeamMembers` role at
   request time. We do not claim a "role cap" we don't enforce. Because membership is checked
   live, a removed or downgraded member's key automatically loses access — no extra revocation
   wiring.

6. **In-process C# MCP server.** No second deploy artifact, no TypeScript sidecar. MCP is one
   transport over the same Service layer.

Key storage mirrors the existing `MagicLinkToken` / `TeamInvite` pattern: store only
`SHA-256(rawKey)` (raw shown exactly once on creation), with `ExpiresAt` + revocation columns and
a unique index on the hash. `TeamApiKey` is a mutable row, **not** a versioned entity — it sits
outside the append-only invariant by design (it is credential lifecycle, not product truth).

Management is **Lead-only** and has **no UI in alpha** — keys are minted via the management API
(or admin-cli). UI follows once the security model is proven.

## Alternatives Considered

- **Native `Actor` identity now.** The correct long-term model, but hard to reverse and large.
  Rejected for alpha; a key that resolves to a real `UserId` is forward-compatible — the `Actor`
  migration can come later without re-architecting.
- **API keys on the general HTTP API.** Rejected: every authenticated route would need team-scope
  enforcement. MCP-only shrinks the surface to a handful of read tools.
- **Enforced per-key permission scopes.** Rejected for alpha: live `TeamMembers` role is simpler
  and already correct. A real per-key scope model can build on the `Role` snapshot column later.
- **TypeScript MCP sidecar / separate service.** Rejected: a second deploy artifact and a second
  copy of auth + business logic. In-process C# reuses the Service layer directly.
- **Bcrypt/argon2 key hashing.** Unnecessary: keys are ≥256 bits of CSPRNG entropy, so plain
  SHA-256 is sufficient and matches the existing token pattern. (Bcrypt is for low-entropy human
  passwords.)

## Consequences

- The auth handler is security-critical: cross-team isolation, revocation, expiry, and a single
  generic `401` for every failure mode (not-found / expired / revoked / malformed — no enumeration
  signal) must be airtight and integration-tested **through the real `/mcp` endpoint**.
- RLS is unchanged but must be **proven** set during MCP tool service calls, with no context bleed
  across consecutive stateless requests.
- `TeamApiKey` gets its own RLS policies: a GUC-based (`app.api_key_hash`) lookup path for the
  pre-authentication handler, plus `rls_is_team_member` for the cookie-authenticated management
  path — mirroring how `MagicLinkTokens` allows a pre-auth token lookup.
- Adds one new dependency for the server half (`ModelContextProtocol.AspNetCore`, preview-era);
  pinned and isolated in `Features/Mcp/`. (Tracked separately; this ADR covers the identity model.)
- The deferred `Actor` model remains an open, ADR-worthy decision for post-alpha.
