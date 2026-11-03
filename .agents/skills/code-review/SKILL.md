---
name: code-review
description: Reviews current branch diff or specified scope for correctness, tenant isolation, versioning integrity, security, simplicity, and convention adherence. Returns findings labelled must-fix / should-fix / consider.
argument-hint: "[branch-name or file-path (optional, defaults to current branch vs main)]"
arguments: [scope]
---

# Code Review

**Commits** (if scope is a branch):

```!
[ -n "$scope" ] && git log main..$scope --oneline 2>/dev/null || git log main..HEAD --oneline 2>/dev/null
```

**Diff:**

```!
[ -n "$scope" ] && git diff main...$scope 2>/dev/null || git diff main...HEAD 2>/dev/null
```

Apply a thorough review. Evaluate in this exact order:

## 0. Verification gate

If this review leads to code changes, require running the `format-lint-test` skill before declaring the task done. Do not duplicate verification script commands in this skill.

## 1. Correctness

Does it do what it says? Are there edge cases that break it? Logic errors, off-by-one, null dereferences?

## 2. Tenant isolation

- Raw `Guid` passed to a service (should be `AuthorizedId`)?
- Store called directly from an endpoint?
- Filter parameter passed through without authorization (e.g. `?featureId=` not authorized at the endpoint layer)?
- `AuthorizedId` constructors accessible outside `ResourceAuthorizationService`?

## 3. Versioning integrity

- Every mutation to a versioned entity inserts a new row — no in-place updates?
- Cascade-delete paths copy all required fields into the new version row (no partial initializers)?
- No `SaveChanges` on tracked `Modified`/`Deleted` state for versioned entities?

## 4. Security

- Input validation at system boundaries?
- SQL injection risk in raw queries?
- Host header injection in URL construction?
- Sensitive data in logs or responses?

## 5. Clarity

Readable without excessive comments? Self-documenting names?

## 6. Simplicity and modularity

- Is there a simpler path to the same result?
- Is the solution larger or more abstract than the problem warrants?
- Are there new abstractions, base classes, helpers, or generalisations that weren't explicitly required by the spec?
- Could this be solved with less code without losing correctness?

The default failure mode of AI-generated code is over-engineering. Flag any solution that introduces complexity beyond what the problem forces. Complexity requires justification; simplicity does not.

## 7. Convention adherence

- Layer contract respected (endpoint/service/store)?
- Naming conventions (PascalCase, DTOs, interfaces)?
- Error handling via `HttpProblemException` + `ProblemTypes`?
- `AsNoTracking()` on read-only queries?
- RFC 9457 status codes?

---

Label every finding:

- **Must fix** — bugs, security issues, versioning violations, tenant isolation gaps
- **Should fix** — clarity, convention violations
- **Consider** — suggestions, no obligation

Be specific: quote the relevant code, explain the concern, propose the fix. If the diff is empty, say so.
