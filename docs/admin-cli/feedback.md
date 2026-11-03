# Admin CLI — Feedback

`grundet-admin feedback` reviews authenticated app feedback submissions through direct database access.

## Configuration

Uses the same connection-string and operator configuration as the waitlist commands. See [waitlist.md](waitlist.md).

Feedback tables use database row-level security. The CLI sets the `app.admin_cli=true` session option on its database connections so direct admin reads and mutations can see feedback rows.

If `Feedback:AttachmentsRoot` / `Feedback__AttachmentsRoot` is configured for the CLI process, `feedback export` also copies stored JPEG attachment files. Without it, export still writes metadata JSON and prints skipped attachment paths.

Attachments-root resolution:

1. `Feedback:AttachmentsRoot` (config) / `Feedback__AttachmentsRoot` (env) — the production source. On the server this is set by Coolify on the API container, so running the CLI in the API's environment (`docker exec` over SSH) inherits it.
2. Development-only fallback: the API's `src/Grundet.Api/appsettings.Development.json` `Feedback:AttachmentsRoot`, resolved to an absolute path. This makes local export work without exporting anything, mirroring the connection-string fallback.

Attachment files live on the server's mounted volume (e.g. `/var/lib/grundet/feedback-attachments`). `feedback export` can only copy the binaries where that volume is reachable — i.e. on the server. Exporting from a laptop against a remote DB writes `metadata.json` and reports the files as missing, which is expected.

## Identifying submissions

`feedback show`, `export`, `mark-read`, and `delete` accept either a full UUID or a **git-style unique hex prefix** (minimum 4 hex characters, dashes ignored):

```bash
grundet-admin feedback show 38a299df          # resolves if the prefix is unique
grundet-admin feedback show 38a299df-daec     # full or partial, dashes optional
```

- A prefix that matches exactly one submission resolves to it.
- An **ambiguous** prefix (matches more than one) fails closed: non-zero exit, no write.
- A prefix matching nothing is a normal no-match (non-zero exit).
- Non-hex or too-short input is rejected as an argument error (exit 2), exactly as a malformed UUID was before.
- Prefix matching respects the same deleted scope as the command (mutations only resolve against active rows).

## List submissions

```bash
grundet-admin feedback list
grundet-admin feedback list --unread
grundet-admin feedback list --include-deleted --limit 25
```

Notes:

- Default limit is `50`.
- Maximum limit is `200`.
- Deleted submissions are hidden by default.
- Use `--include-deleted` to include soft-deleted submissions.
- Output includes created time, read/deleted state, kind, ID, user email, attachment count, page path, and a message preview.

## Show one submission

```bash
grundet-admin feedback show <feedback-id>
grundet-admin feedback show <feedback-id> --include-deleted
```

Notes:

- Deleted submissions are hidden by default.
- Full metadata, message text, and attachment storage keys are printed.
- No match exits non-zero.

## Export one submission

```bash
grundet-admin feedback export <feedback-id> --output ./feedback-export
grundet-admin feedback export <feedback-id> --output ./feedback-export --include-deleted
```

Export writes to `<output>/<feedback-id>/`:

- `metadata.json` with submission metadata and attachment rows.
- Attachment JPEG files, when the CLI can resolve `Feedback:AttachmentsRoot` and the files exist.

Export does not modify database state. Deleted submissions are hidden unless `--include-deleted` is supplied.

## Mark read

Dry-run first:

```bash
grundet-admin feedback mark-read <feedback-id> --operator "operator"
```

Execute:

```bash
grundet-admin feedback mark-read <feedback-id> --operator "operator" --execute
```

## Soft-delete

Dry-run first:

```bash
grundet-admin feedback delete <feedback-id> --operator "operator"
```

Execute:

```bash
grundet-admin feedback delete <feedback-id> --operator "operator" --execute
```

Delete semantics:

- Sets `FeedbackSubmissions.DeletedAt` and `DeletedByOperator`.
- Leaves attachment files on disk.
- Deleted submissions are hidden from list/show/export by default.
- There is no hard-delete command in v1.

## Mutation safety

`mark-read` and `delete` commands:

- are dry-run by default;
- require `--execute` to write;
- require `--operator` or `GRUNDET_ADMIN_OPERATOR`;
- print target DB host/database before writing, without secrets;
- print feedback ID, user, current status, proposed status, operator, mode, and affected rows;
- persist the operator to `ReadByOperator` or `DeletedByOperator` when executed;
- run in a database transaction;
- fail closed when the active submission is missing;
- commit only when exactly one row is affected.

## Interactive mode

Running `grundet-admin` with no arguments in a terminal opens the TUI (see the [overview](README.md#two-faces-one-core)). Under `Feedback` you get the same `list`, `show`, `export`, `mark-read`, and `delete` actions over the same core. `mark-read`/`delete` run a dry-run preview first, then require an explicit `y`/`Y` confirmation — the interactive equivalent of dry-run + `--execute`. `export` is read-only and prompts for the output directory. `show` and `export` both ask `include soft-deleted feedback? (y/n)` — mirroring the `--include-deleted` flag, soft-deleted submissions stay hidden unless you answer `y`/`Y`. The same fail-closed rules apply; nothing is written on `n`, Enter, or EOF.

`feedback list` in the TUI offers a single **status filter** mirroring the waitlist one — `All` / `Unread` / `Read` / `Deleted` — instead of the `--unread` / `--include-deleted` flags. Menus wrap around (cycle past the ends), the last `← Back` choice returns to the previous menu, and leaving an id/output prompt empty cancels the action. Partial-id resolution (above) applies in the TUI too: enter a unique prefix at any feedback-id prompt.
