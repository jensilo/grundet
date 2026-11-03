using System.Globalization;
using Grundet.AdminCli.Core;
using Spectre.Console;

namespace Grundet.AdminCli.Tui;

/// <summary>
/// Interactive Spectre.Console face. A thin renderer + prompt loop over the command core:
/// no SQL and no business logic live here. The entrypoint routes a no-args interactive TTY
/// to <see cref="RunAsync"/>; every safety rule (operator identity, DB-target print, dry-run
/// preview, transaction-wrapped writes, fail-closed) is enforced by the core, not the menu.
/// Destructive actions run the core in dry-run, render the preview, gate on an explicit
/// <c>y</c>/<c>Y</c>, then re-run with execute. Config/connection errors are caught and printed
/// sanitized — raw exceptions are never handed to Spectre's renderer (a connection string must
/// never leak into a stack trace).
/// </summary>
internal static class TuiApp
{
    private const int ListLimit = 50;
    private const string BackChoice = "← Back";

    public static async Task<int> RunAsync()
    {
        AdminConfig config = new(connectionStringOption: null);

        // Resolve the DB target up front so the operator always sees which database they are
        // pointed at — and so a missing/invalid connection string fails fast with a clear
        // message rather than dropping into a broken menu. Never prints the connection string.
        string databaseTarget;
        try
        {
            databaseTarget = config.DescribeDatabaseTarget();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Configuration error:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }

        // Operator precedence mirrors the flag path (env → config), with an interactive prompt
        // replacing the missing --operator. Held for the session, re-shown before each write.
        string? operatorName = AdminConfig.GetOperator(null);
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            operatorName = AnsiConsole.Prompt(
                new TextPrompt<string>("Operator identity:").Validate(static value =>
                    string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Operator identity is required.")
                        : ValidationResult.Success())).Trim();
        }

        AnsiConsole.Write(new Panel(new Markup(
                $"[bold]Grundet admin[/]\n[dim]{Markup.Escape(databaseTarget)}[/]\nOperator: [green]{Markup.Escape(operatorName!)}[/]"))
            .Header("Interactive mode")
            .BorderColor(Color.Grey));

        while (true)
        {
            string choice = Select("Choose a [green]namespace[/]:", "Waitlist", "Feedback", "Quit");

            switch (choice)
            {
                case "Waitlist":
                    await RunActionMenuAsync(config, operatorName!, "Waitlist", WaitlistActionAsync);
                    break;
                case "Feedback":
                    await RunActionMenuAsync(config, operatorName!, "Feedback", FeedbackActionAsync);
                    break;
                default:
                    return 0;
            }
        }
    }

    private static async Task RunActionMenuAsync(
        AdminConfig config,
        string operatorName,
        string title,
        Func<AdminConfig, string, string, Task> action)
    {
        string[] choices = title == "Waitlist"
            ? ["list", "show", "approve", "decline", BackChoice]
            : ["list", "show", "export", "mark-read", "delete", BackChoice];

        while (true)
        {
            string choice = Select($"[green]{title}[/] action:", choices);
            if (choice == BackChoice)
            {
                return;
            }

            // Every action call is wrapped so a connection/DB failure returns to the menu with a
            // sanitized message instead of crashing the session or leaking a stack trace.
            try
            {
                await action(config, operatorName, choice);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            }

            AnsiConsole.WriteLine();
        }
    }

    // --- Waitlist ---

    private static async Task WaitlistActionAsync(AdminConfig config, string operatorName, string action)
    {
        switch (action)
        {
            case "list":
            {
                if (!TryPromptWaitlistStatus(out WaitlistStatus? status))
                {
                    break;
                }

                WaitlistListResult result = await WaitlistService.ListAsync(config, new WaitlistListRequest(ListLimit, status));
                RenderWaitlistList(result);
                break;
            }

            case "show":
            {
                string? email = AskOptional("Email");
                if (email is null)
                {
                    break;
                }

                WaitlistShowResult result = await WaitlistService.ShowAsync(config, email, includeReason: true);
                RenderWaitlistShow(result);
                break;
            }

            case "approve":
                await ChangeWaitlistStatusAsync(config, operatorName, WaitlistStatus.Approved);
                break;

            case "decline":
                await ChangeWaitlistStatusAsync(config, operatorName, WaitlistStatus.Declined);
                break;
        }
    }

    private static async Task ChangeWaitlistStatusAsync(AdminConfig config, string operatorName, WaitlistStatus target)
    {
        string? email = AskOptional("Email");
        if (email is null)
        {
            return;
        }

        // Dry-run first: the core finds the row and reports what a write would do, without writing.
        WaitlistChangeResult preview = await WaitlistService.ChangeStatusAsync(
            config, new WaitlistChangeRequest(email, target, operatorName, Execute: false));

        AnsiConsole.MarkupLine($"[dim]Database target: {Markup.Escape(preview.DatabaseTarget ?? "(unresolved)")}[/]");

        switch (preview.Outcome)
        {
            case WaitlistChangeOutcome.NotFound:
                AnsiConsole.MarkupLine($"[red]No waitlist entry found for {Markup.Escape(preview.NormalizedEmail)}.[/]");
                return;
            case WaitlistChangeOutcome.Ambiguous:
                AnsiConsole.MarkupLine($"[red]Ambiguous: {preview.MatchCount} entries for {Markup.Escape(preview.NormalizedEmail)}.[/]");
                return;
            case WaitlistChangeOutcome.NotPending:
                RenderWaitlistMutationPreview(preview, operatorName);
                AnsiConsole.MarkupLine("[red]Refusing to mutate: current status is not Pending.[/]");
                return;
            case WaitlistChangeOutcome.OperatorMissing:
                AnsiConsole.MarkupLine("[red]Operator identity is required.[/]");
                return;
        }

        RenderWaitlistMutationPreview(preview, operatorName);
        if (!ConfirmDestructive())
        {
            AnsiConsole.MarkupLine("[yellow]Aborted. Nothing written.[/]");
            return;
        }

        WaitlistChangeResult result = await WaitlistService.ChangeStatusAsync(
            config, new WaitlistChangeRequest(email, target, operatorName, Execute: true));
        RenderWaitlistChangeResult(result);
    }

    private static void RenderWaitlistList(WaitlistListResult result)
    {
        if (result.Entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No waitlist entries.[/]");
            return;
        }

        Table table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Created", "Status", "Type", "Email", "Name");
        foreach (WaitlistEntry entry in result.Entries)
        {
            table.AddRow(
                entry.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Markup.Escape(entry.Status.ToString()),
                Markup.Escape(entry.AccountType),
                Markup.Escape(entry.EmailNormalized),
                Markup.Escape(entry.Name));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{result.Entries.Count} entr{(result.Entries.Count == 1 ? "y" : "ies")} (limit {result.Limit}).[/]");
    }

    private static void RenderWaitlistShow(WaitlistShowResult result)
    {
        switch (result.Lookup)
        {
            case WaitlistLookup.NotFound:
                AnsiConsole.MarkupLine($"[red]No waitlist entry found for {Markup.Escape(result.NormalizedEmail)}.[/]");
                return;
            case WaitlistLookup.Ambiguous:
                AnsiConsole.MarkupLine($"[red]Ambiguous: {result.MatchCount} entries for {Markup.Escape(result.NormalizedEmail)}.[/]");
                return;
        }

        WaitlistEntry entry = result.Entry!;
        Grid grid = new();
        grid.AddColumn();
        grid.AddColumn();
        AddRow(grid, "Email", entry.EmailOriginal);
        AddRow(grid, "Normalized", entry.EmailNormalized);
        AddRow(grid, "Name", entry.Name);
        AddRow(grid, "Account type", entry.AccountType);
        AddRow(grid, "Organization", entry.OrganizationName);
        AddRow(grid, "Status", entry.Status.ToString());
        AddRow(grid, "Created", entry.CreatedAt.ToString("u"));
        AddRow(grid, "Approved", entry.ApprovedAt is null ? null : $"{entry.ApprovedAt:u} by {entry.ApprovedBy}");
        AddRow(grid, "Declined", entry.DeclinedAt is null ? null : $"{entry.DeclinedAt:u} by {entry.DeclinedBy}");
        AddRow(grid, "Reason", entry.Reason);
        AnsiConsole.Write(new Panel(grid).Header(Markup.Escape(entry.EmailNormalized)).BorderColor(Color.Grey));
    }

    private static void RenderWaitlistMutationPreview(WaitlistChangeResult preview, string operatorName)
    {
        WaitlistEntry entry = preview.Entry!;
        Grid grid = new();
        grid.AddColumn();
        grid.AddColumn();
        AddRow(grid, "Target email", entry.EmailOriginal);
        AddRow(grid, "Normalized", entry.EmailNormalized);
        AddRow(grid, "Current status", entry.Status.ToString());
        AddRow(grid, "Proposed status", preview.Target.ToString());
        AddRow(grid, "Operator", operatorName);
        AnsiConsole.Write(new Panel(grid).Header("[yellow]Dry-run preview[/]").BorderColor(Color.Yellow));
    }

    private static void RenderWaitlistChangeResult(WaitlistChangeResult result)
    {
        switch (result.Outcome)
        {
            case WaitlistChangeOutcome.Committed:
                AnsiConsole.MarkupLine($"[green]Committed. Affected rows: {result.AffectedRows}.[/]");
                break;
            case WaitlistChangeOutcome.NotPending:
                AnsiConsole.MarkupLine("[red]Row changed before commit: current status is no longer Pending. Nothing written.[/]");
                break;
            case WaitlistChangeOutcome.AffectedMismatch:
                AnsiConsole.MarkupLine($"[red]Rolled back: affected rows was {result.AffectedRows}, expected exactly 1.[/]");
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Not committed ({result.Outcome}). Nothing written.[/]");
                break;
        }
    }

    // --- Feedback ---

    private static async Task FeedbackActionAsync(AdminConfig config, string operatorName, string action)
    {
        switch (action)
        {
            case "list":
            {
                FeedbackListRequest? request = PromptFeedbackFilter(ListLimit);
                if (request is null)
                {
                    break;
                }

                FeedbackListResult result = await FeedbackService.ListAsync(config, request);
                RenderFeedbackList(result);
                break;
            }

            case "show":
            {
                string? id = AskOptional("Feedback id (full UUID or unique prefix)");
                if (id is null)
                {
                    break;
                }

                FeedbackShowResult result = await FeedbackService.ShowAsync(config, id, ConfirmIncludeDeleted());
                RenderFeedbackShow(result);
                break;
            }

            case "export":
            {
                string? id = AskOptional("Feedback id (full UUID or unique prefix)");
                if (id is null)
                {
                    break;
                }

                string? output = AskOptional("Output directory");
                if (output is null)
                {
                    break;
                }

                FeedbackExportResult result = await FeedbackService.ExportAsync(config, id, output, ConfirmIncludeDeleted());
                RenderFeedbackExport(result);
                break;
            }

            case "mark-read":
                await MutateFeedbackAsync(config, operatorName, "read",
                    static (c, id, op, exec) => FeedbackService.MarkReadAsync(c, id, op, exec));
                break;

            case "delete":
                await MutateFeedbackAsync(config, operatorName, "deleted",
                    static (c, id, op, exec) => FeedbackService.DeleteAsync(c, id, op, exec));
                break;
        }
    }

    private static async Task MutateFeedbackAsync(
        AdminConfig config,
        string operatorName,
        string targetState,
        Func<AdminConfig, string, string, bool, Task<FeedbackMutateResult>> mutate)
    {
        string? id = AskOptional("Feedback id (full UUID or unique prefix)");
        if (id is null)
        {
            return;
        }

        FeedbackMutateResult preview = await mutate(config, id, operatorName, false);
        AnsiConsole.MarkupLine($"[dim]Database target: {Markup.Escape(preview.DatabaseTarget ?? "(unresolved)")}[/]");

        switch (preview.Outcome)
        {
            case FeedbackMutateOutcome.NotFound:
                AnsiConsole.MarkupLine($"[red]No active feedback submission found for {Markup.Escape(preview.IdEcho)}.[/]");
                return;
            case FeedbackMutateOutcome.Ambiguous:
                AnsiConsole.MarkupLine($"[red]Ambiguous prefix {Markup.Escape(preview.IdEcho)}: matches multiple submissions.[/]");
                return;
            case FeedbackMutateOutcome.OperatorMissing:
                AnsiConsole.MarkupLine("[red]Operator identity is required.[/]");
                return;
        }

        RenderFeedbackMutationPreview(preview, operatorName, targetState);
        if (!ConfirmDestructive())
        {
            AnsiConsole.MarkupLine("[yellow]Aborted. Nothing written.[/]");
            return;
        }

        FeedbackMutateResult result = await mutate(config, id, operatorName, true);
        switch (result.Outcome)
        {
            case FeedbackMutateOutcome.Committed:
                AnsiConsole.MarkupLine($"[green]Committed. Affected rows: {result.AffectedRows}.[/]");
                break;
            case FeedbackMutateOutcome.AffectedMismatch:
                AnsiConsole.MarkupLine($"[red]Rolled back: affected rows was {result.AffectedRows}, expected exactly 1.[/]");
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Not committed ({result.Outcome}). Nothing written.[/]");
                break;
        }
    }

    private static void RenderFeedbackList(FeedbackListResult result)
    {
        if (result.Entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No feedback submissions.[/]");
            return;
        }

        Table table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Created", "State", "Kind", "Id", "User", "Files", "Page");
        foreach (FeedbackEntry entry in result.Entries)
        {
            string state = entry.DeletedAt is not null ? "deleted" : entry.ReadAt is null ? "unread" : "read";
            table.AddRow(
                entry.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                state,
                Markup.Escape(entry.Kind.ToString()),
                Markup.Escape(entry.Id.ToString()),
                Markup.Escape(entry.UserEmail),
                entry.AttachmentCount.ToString(CultureInfo.InvariantCulture),
                Markup.Escape(entry.PagePath));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{result.Entries.Count} submission(s) (limit {result.Limit}).[/]");
    }

    private static void RenderFeedbackShow(FeedbackShowResult result)
    {
        switch (result.Lookup)
        {
            case FeedbackLookup.NotFound:
                AnsiConsole.MarkupLine($"[red]No feedback submission found for {Markup.Escape(result.IdEcho)}.[/]");
                return;
            case FeedbackLookup.Ambiguous:
                AnsiConsole.MarkupLine($"[red]Ambiguous prefix {Markup.Escape(result.IdEcho)}: matches multiple submissions.[/]");
                return;
        }

        FeedbackEntry entry = result.Entry!;
        string state = entry.DeletedAt is not null ? "deleted" : entry.ReadAt is null ? "unread" : "read";
        Grid grid = new();
        grid.AddColumn();
        grid.AddColumn();
        AddRow(grid, "Id", entry.Id.ToString());
        AddRow(grid, "User", $"{entry.UserEmail} ({entry.UserId})");
        AddRow(grid, "Kind", entry.Kind.ToString());
        AddRow(grid, "State", state);
        AddRow(grid, "Page", entry.PagePath);
        AddRow(grid, "Created", entry.CreatedAt.ToString("u"));
        AddRow(grid, "Attachments", entry.AttachmentCount.ToString(CultureInfo.InvariantCulture));
        AnsiConsole.Write(new Panel(grid).Header(Markup.Escape(entry.Id.ToString())).BorderColor(Color.Grey));

        AnsiConsole.Write(new Panel(new Text(entry.Message)).Header("Message").BorderColor(Color.Grey));
        foreach (FeedbackAttachmentEntry attachment in result.Attachments)
        {
            AnsiConsole.MarkupLine($"[dim]Attachment: {Markup.Escape(attachment.StoragePath)} {attachment.Width}x{attachment.Height} {attachment.StoredSizeBytes} bytes[/]");
        }
    }

    private static void RenderFeedbackExport(FeedbackExportResult result)
    {
        switch (result.Lookup)
        {
            case FeedbackLookup.NotFound:
                AnsiConsole.MarkupLine($"[red]No feedback submission found for {Markup.Escape(result.IdEcho)}.[/]");
                return;
            case FeedbackLookup.Ambiguous:
                AnsiConsole.MarkupLine($"[red]Ambiguous prefix {Markup.Escape(result.IdEcho)}: matches multiple submissions.[/]");
                return;
        }

        foreach (string message in result.AttachmentMessages)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
        }

        AnsiConsole.MarkupLine($"[green]Exported feedback {Markup.Escape(result.Entry!.Id.ToString())} to {Markup.Escape(result.Target ?? "?")}.[/]");
    }

    private static void RenderFeedbackMutationPreview(FeedbackMutateResult preview, string operatorName, string targetState)
    {
        FeedbackEntry entry = preview.Entry!;
        string current = entry.DeletedAt is not null ? "deleted" : entry.ReadAt is null ? "unread" : "read";
        Grid grid = new();
        grid.AddColumn();
        grid.AddColumn();
        AddRow(grid, "Feedback id", entry.Id.ToString());
        AddRow(grid, "User", entry.UserEmail);
        AddRow(grid, "Current status", current);
        AddRow(grid, "Proposed status", targetState);
        AddRow(grid, "Operator", operatorName);
        AnsiConsole.Write(new Panel(grid).Header("[yellow]Dry-run preview[/]").BorderColor(Color.Yellow));
    }

    // --- Shared helpers ---

    /// <summary>
    /// The interactive equivalent of dry-run + <c>--execute</c>. Accepts only an explicit
    /// <c>y</c>/<c>Y</c>; Enter (empty), EOF (null), or anything else aborts with no write.
    /// Reads a raw line rather than Spectre's <c>ConfirmationPrompt</c> to keep that contract
    /// exact (no Enter-default acceptance).
    /// </summary>
    private static bool ConfirmDestructive()
    {
        AnsiConsole.Markup("[yellow]are you sure? (y/n):[/] ");
        string? input = Console.ReadLine();
        return input?.Trim() is "y" or "Y";
    }

    /// <summary>
    /// The interactive equivalent of the <c>--include-deleted</c> flag. Soft-deleted submissions
    /// are excluded unless the operator explicitly opts in, mirroring the flag path's default of
    /// <c>false</c>. Accepts only an explicit <c>y</c>/<c>Y</c>; anything else (including Enter) keeps
    /// deleted feedback hidden.
    /// </summary>
    private static bool ConfirmIncludeDeleted()
    {
        AnsiConsole.Markup("[yellow]include soft-deleted feedback? (y/n):[/] ");
        string? input = Console.ReadLine();
        return input?.Trim() is "y" or "Y";
    }

    /// <summary>A selection prompt with wrap-around navigation (cycle past the ends).</summary>
    private static string Select(string title, params string[] choices)
        => AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).WrapAround().AddChoices(choices));

    /// <summary>
    /// A cancellable text prompt: an empty entry returns null so the caller can abort the action
    /// and fall back to the menu (Spectre has no native Esc-to-cancel).
    /// </summary>
    private static string? AskOptional(string label)
    {
        string value = AnsiConsole.Prompt(new TextPrompt<string>($"{label} [grey](empty to cancel)[/]:").AllowEmpty()).Trim();
        if (value.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return null;
        }

        return value;
    }

    private static bool TryPromptWaitlistStatus(out WaitlistStatus? status)
    {
        string choice = Select("Status filter:", "All", "Pending", "Approved", "Declined", BackChoice);
        if (choice == BackChoice)
        {
            status = null;
            return false;
        }

        status = choice == "All" ? null : Enum.Parse<WaitlistStatus>(choice);
        return true;
    }

    /// <summary>
    /// Feedback status filter mirroring the waitlist one: a single pick maps to the orthogonal
    /// list filters. Returns null when the operator backs out.
    /// </summary>
    private static FeedbackListRequest? PromptFeedbackFilter(int limit)
        => Select("Status filter:", "All", "Unread", "Read", "Deleted", BackChoice) switch
        {
            "All" => new FeedbackListRequest(limit, Unread: null, Deleted: null),
            "Unread" => new FeedbackListRequest(limit, Unread: true, Deleted: false),
            "Read" => new FeedbackListRequest(limit, Unread: false, Deleted: false),
            "Deleted" => new FeedbackListRequest(limit, Unread: null, Deleted: true),
            _ => null,
        };

    private static void AddRow(Grid grid, string label, string? value)
        => grid.AddRow($"[dim]{label}[/]", Markup.Escape(string.IsNullOrEmpty(value) ? "—" : value));
}
