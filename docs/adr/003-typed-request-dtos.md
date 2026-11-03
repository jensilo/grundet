# 003 — Typed request DTOs (no JsonElement on endpoint signatures)

**Date:** 2026-05-19
**Status:** Accepted

## Context

Several mutation endpoints accept `JsonElement` as the request body. The motivation was partial-update semantics (field absence ≠ null). The consequence is that OpenAPI emits an untyped body schema, the generated TypeScript client cannot express the shape, and the frontend uses `as unknown` casts to satisfy the compiler. PR #10's M1 bug (silent assignee deletion) was a direct result: a field was omitted from the body with no build-time error. Every future schema change to these endpoints will fail silently in the same way.

## Decision

All endpoint signatures use typed `record` DTOs for request bodies. No `JsonElement` parameters on public endpoints.

For partial-update semantics (where field absence means "don't change this field"), use nullable fields on the DTO. Absent fields from the client are deserialized as `null`; the service layer distinguishes `null` (not provided, keep existing value) from a provided value by convention documented in the DTO. If a property genuinely must carry `null` as a meaningful value distinct from "not provided", use a wrapper type or a separate dedicated endpoint.

The process is: fix the DTO on the backend → regenerate the TypeScript client → adjust the frontend. Never go the other direction.

## Alternatives Considered

**Keep `JsonElement` + typed OpenAPI annotation:** Annotate the OpenAPI spec with the expected shape while keeping `JsonElement` at the implementation level. Partially addresses the generated client problem but leaves the implementation untyped — runtime deserialization errors replace compile-time errors.

**Frontend wrapper layer:** Hand-write typed wrappers over the untyped generated client. Adds a maintenance burden and two representations of the same shape.

## Consequences

- Eliminates the `as unknown` / silent-data-loss class of bugs.
- OpenAPI spec accurately reflects endpoint contracts.
- Generated TypeScript client is fully typed end-to-end.
- Requires migrating existing `JsonElement` endpoints to typed DTOs.
- Nullable DTO fields used for optional partial-update semantics must be documented clearly in the DTO record.
