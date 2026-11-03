# Code Map — API Contract Chain

**Last verified:** 2026-07-11

Backend DTO → frontend, no step skipped (ADR-003, `docs/memory.md` → API contract):

```
*Endpoints.cs request/response records
  → /openapi/v1.json           (running backend, non-prod)
  → make generate-api          (openapi-ts.config.ts)
  → lib/api/generated/{sdk,types}.gen.ts
  → pages via serverClient (RSC) or browser singleton (client components)
```

Changing any endpoint the frontend consumes ⇒ regenerate the client (backend must be running). CI gates on the chain: `OpenApiSchemaIntegrationTests.cs`, `EnumContractIntegrationTests.cs`.
