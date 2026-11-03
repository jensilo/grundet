---
name: test-coverage
description: Analyses test coverage for a given feature or file. Reads implementation, enumerates invariants and edge cases, maps to existing tests, and reports gaps. No test execution — analysis only.
argument-hint: <feature-name or file-path>
arguments: [target]
---

# Test Coverage Analysis

Target: $target

## Steps

1. Read the implementation file(s) for `$target` (service, store, endpoints)
2. Read the relevant test file(s) in `src/Grundet.Api.Tests/`
3. Enumerate the invariants and edge cases that should be covered:

**Standard categories:**
- Happy path (successful operation)
- Auth / tenant isolation (cross-tenant access, missing auth, wrong role)
- Versioning (mutation produces new version row, cascade copies all fields)
- Boundary values and error states (invalid input, duplicate, conflict)
- RLS enforcement (same-tenant data returned, cross-tenant blocked)
- Concurrency / race conditions (where applicable)
- Cascade behaviour (where applicable)

4. For each case: map to an existing test or flag as a gap
5. Report gaps as a numbered checklist. For each gap:
   - Suggested test name (`MethodName_Condition_ExpectedOutcome`)
   - The key assertion needed
   - Priority (high/medium/low)

Do not write test code unless explicitly asked. This is analysis only.
