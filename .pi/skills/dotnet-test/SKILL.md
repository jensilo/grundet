---
name: dotnet-test
description: Run .NET tests in repositories with multiple solutions or test projects. Use when selecting and executing dotnet test targets.
---

# .NET test

1. Find relevant `.sln`, `.slnx`, and test `.csproj` files.
2. Choose the narrowest target that verifies the change:
   - one test project for focused work;
   - the owning solution for broader verification;
   - multiple targets only when the change crosses their boundaries.
3. Run `dotnet test <path>` from a suitable repository directory.
4. Add options only for a concrete need. Use `--no-restore` only when restore is already valid. Use filters only when intentionally narrowing coverage.
5. Report the command, passed/failed/skipped counts, and failures.

If target ownership is materially ambiguous, inspect repository guidance or ask. Do not guess a solution or run every target by ritual.

Consult `dotnet test --help` for filters, configuration, logging, framework selection, and other details.
