# Admin CLI — Waitlist

`grundet-admin waitlist` manages private-alpha waitlist entries through direct database access.

## Configuration

Connection string precedence:

1. `--connection-string "Host=...;Database=...;Username=...;Password=..."`
2. `GRUNDET_ADMIN_CONNECTION_STRING="Host=...;Database=...;Username=...;Password=..."` — preferred production variable
3. `ConnectionStrings:Admin`
4. `ConnectionStrings:Default`
5. Development-only local fallback: `src/Grundet.Api/appsettings.Development.json` `ConnectionStrings:Default`

The CLI defaults to `Development` when neither `DOTNET_ENVIRONMENT` nor `ASPNETCORE_ENVIRONMENT` is set, so local repo usage picks up the API development connection string without passing a flag. In `Production`, set `DOTNET_ENVIRONMENT=Production`; the CLI then fails closed unless one of the explicit/configured sources above provides a connection string. This prevents a repo checkout from accidentally overriding production defaults with a local dev database.

Do not pass production connection strings via `--connection-string` unless there is no alternative. Shell history and process inspection can expose command-line secrets. Prefer the container/platform secret environment variable `GRUNDET_ADMIN_CONNECTION_STRING`. `ConnectionStrings__Admin` also works, but the explicit admin CLI variable is the canonical production setting.

Mutating commands also require an operator identity. Operator precedence:

```bash
--operator "operator"
GRUNDET_ADMIN_OPERATOR="operator"
AdminCli__Operator="operator"
```

For current single-operator production, prefer persistent `GRUNDET_ADMIN_OPERATOR` in the platform/container environment so commands do not need unsafe per-session exports. If multiple humans operate production later, prefer `--operator` per command or per-person shell context so the audit actor stays accurate. `AdminCli:Operator` is mainly a local-dev convenience.

## List entries

```bash
grundet-admin waitlist list
grundet-admin waitlist list --status pending
grundet-admin waitlist list --status approved --limit 25
```

Notes:

- Default limit is `50`.
- Maximum limit is `200`.
- Status may be `pending`, `approved`, or `declined`; numeric enum values such as `0` are rejected.
- List output intentionally omits the free-text reason field.

## Show one entry

```bash
grundet-admin waitlist show person@example.com
grundet-admin waitlist show person@example.com --include-reason
```

Notes:

- Email is normalized with the same v1 rule as auth/waitlist: `Trim().ToLowerInvariant()`.
- Reason text is only printed with `--include-reason`.
- No match exits non-zero.
- Multiple matches exits non-zero, even though the DB should prevent this.

## Approve access

Dry-run first:

```bash
grundet-admin waitlist approve person@example.com --operator "operator"
```

Execute:

```bash
grundet-admin waitlist approve person@example.com --operator "operator" --execute
```

Approval writes:

- `Status = Approved`
- `UpdatedAt = now`
- `ApprovedAt = now`
- `ApprovedBy = <operator>`
- clears declined audit fields

## Decline access

Dry-run first:

```bash
grundet-admin waitlist decline person@example.com --operator "operator"
```

Execute:

```bash
grundet-admin waitlist decline person@example.com --operator "operator" --execute
```

Decline writes:

- `Status = Declined`
- `UpdatedAt = now`
- `DeclinedAt = now`
- `DeclinedBy = <operator>`
- clears approved audit fields

## Mutation safety

Approve/decline commands:

- are dry-run by default;
- require `--execute` to write;
- require `--operator` or `GRUNDET_ADMIN_OPERATOR`;
- print target DB host/database before writing, without secrets;
- print target email, normalized email, current status, proposed status, operator, and affected rows;
- run in a database transaction;
- update by normalized email and expected current status `Pending`;
- commit only when exactly one row is affected;
- fail closed for no match, multiple matches, or non-`Pending` current status.

Approved or declined entries are intentionally not idempotently re-written in v1. Re-running approve/decline against a non-`Pending` entry exits non-zero.

## Interactive mode

Running `grundet-admin` with no arguments in a terminal opens the TUI (see the [overview](README.md#two-faces-one-core)). Under `Waitlist` you get the same `list`, `show`, `approve`, and `decline` actions over the same core. `list` offers a status pre-selection (`All` / `Pending` / `Approved` / `Declined`). Approve/decline run a dry-run preview first, then require an explicit `y`/`Y` confirmation — the interactive equivalent of dry-run + `--execute`. Operator identity comes from `GRUNDET_ADMIN_OPERATOR` / `AdminCli:Operator`, or a one-time prompt held for the session. Menus wrap around, a `← Back` entry returns to the previous menu, and an empty email prompt cancels. The same fail-closed rules apply; nothing is written on `n`, Enter, or EOF.
