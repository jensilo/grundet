using Grundet.AdminCli.Core;

namespace Grundet.AdminCli.Cli;

/// <summary>
/// Non-interactive face. Parses positional args + flags, calls the command core, and renders
/// the typed results to stdout/stderr with the exact text, stream usage, and exit codes of the
/// original flag tool. Agents and scripts depend on this contract being byte-for-byte stable.
/// </summary>
internal static class FlagDispatcher
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "waitlist" && args[1] == "list")
            {
                Dictionary<string, string?> options = ParseOptions(args[2..], ["connection-string", "status", "limit"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                int limit = ParseLimit(options);
                WaitlistStatus? status = ParseOptionalStatus(options.GetValueOrDefault("status"));
                WaitlistListResult result = await WaitlistService.ListAsync(config, new WaitlistListRequest(limit, status));
                return RenderWaitlistList(result);
            }

            if (args.Length >= 3 && args[0] == "waitlist" && args[1] == "show")
            {
                Dictionary<string, string?> options = ParseOptions(args[3..], ["connection-string", "include-reason"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                WaitlistShowResult result = await WaitlistService.ShowAsync(config, args[2], options.ContainsKey("include-reason"));
                return RenderWaitlistShow(result);
            }

            if (args.Length >= 3 && args[0] == "waitlist" && args[1] == "approve")
            {
                return await ChangeStatusAsync(args[2], WaitlistStatus.Approved, ParseOptions(args[3..], ["connection-string", "operator", "execute"]));
            }

            if (args.Length >= 3 && args[0] == "waitlist" && args[1] == "decline")
            {
                return await ChangeStatusAsync(args[2], WaitlistStatus.Declined, ParseOptions(args[3..], ["connection-string", "operator", "execute"]));
            }

            if (args.Length >= 2 && args[0] == "feedback" && args[1] == "list")
            {
                Dictionary<string, string?> options = ParseOptions(args[2..], ["connection-string", "limit", "unread", "include-deleted"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                int limit = ParseLimit(options);
                FeedbackListResult result = await FeedbackService.ListAsync(config, new FeedbackListRequest(
                    limit,
                    Unread: options.ContainsKey("unread") ? true : null,
                    Deleted: options.ContainsKey("include-deleted") ? null : false));
                return RenderFeedbackList(result);
            }

            if (args.Length >= 3 && args[0] == "feedback" && args[1] == "show")
            {
                Dictionary<string, string?> options = ParseOptions(args[3..], ["connection-string", "include-deleted"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                FeedbackShowResult result = await FeedbackService.ShowAsync(config, args[2], options.ContainsKey("include-deleted"));
                return RenderFeedbackShow(result);
            }

            if (args.Length >= 3 && args[0] == "feedback" && args[1] == "export")
            {
                Dictionary<string, string?> options = ParseOptions(args[3..], ["connection-string", "output", "include-deleted"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                FeedbackExportResult result = await FeedbackService.ExportAsync(config, args[2], options.GetValueOrDefault("output"), options.ContainsKey("include-deleted"));
                return RenderFeedbackExport(result);
            }

            if (args.Length >= 3 && args[0] == "feedback" && args[1] == "mark-read")
            {
                Dictionary<string, string?> options = ParseOptions(args[3..], ["connection-string", "operator", "execute"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                FeedbackMutateResult result = await FeedbackService.MarkReadAsync(config, args[2], AdminConfig.GetOperator(options.GetValueOrDefault("operator")), options.ContainsKey("execute"));
                return RenderFeedbackMutate(result);
            }

            if (args.Length >= 3 && args[0] == "feedback" && args[1] == "delete")
            {
                Dictionary<string, string?> options = ParseOptions(args[3..], ["connection-string", "operator", "execute"]);
                AdminConfig config = new(options.GetValueOrDefault("connection-string"));
                FeedbackMutateResult result = await FeedbackService.DeleteAsync(config, args[2], AdminConfig.GetOperator(options.GetValueOrDefault("operator")), options.ContainsKey("execute"));
                return RenderFeedbackMutate(result);
            }

            PrintUsage();
            return 2;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static async Task<int> ChangeStatusAsync(string email, WaitlistStatus targetStatus, Dictionary<string, string?> options)
    {
        AdminConfig config = new(options.GetValueOrDefault("connection-string"));
        string? operatorName = AdminConfig.GetOperator(options.GetValueOrDefault("operator"));
        WaitlistChangeResult result = await WaitlistService.ChangeStatusAsync(
            config,
            new WaitlistChangeRequest(email, targetStatus, operatorName, options.ContainsKey("execute")));
        return RenderWaitlistChange(result);
    }

    // --- Renderers: structured result -> exact console output ---

    private static int RenderWaitlistList(WaitlistListResult result)
    {
        Console.WriteLine($"Showing {result.Entries.Count} waitlist entries (limit {result.Limit}){(result.Status is null ? string.Empty : $", status {result.Status}")}.");
        foreach (WaitlistEntry entry in result.Entries)
        {
            Console.WriteLine($"{entry.CreatedAt:O}\t{entry.Status}\t{entry.AccountType}\t{entry.EmailNormalized}\t{entry.Name}");
        }

        return 0;
    }

    private static int RenderWaitlistShow(WaitlistShowResult result)
    {
        switch (result.Lookup)
        {
            case WaitlistLookup.NotFound:
                Console.Error.WriteLine($"No waitlist entry found for {result.NormalizedEmail}.");
                return 1;
            case WaitlistLookup.Ambiguous:
                Console.Error.WriteLine($"Ambiguous waitlist state: {result.MatchCount} entries found for {result.NormalizedEmail}.");
                return 1;
            default:
                PrintEntry(result.Entry!, result.IncludeReason);
                return 0;
        }
    }

    private static int RenderWaitlistChange(WaitlistChangeResult result)
    {
        if (result.Outcome == WaitlistChangeOutcome.OperatorMissing)
        {
            Console.Error.WriteLine("Mutating commands require --operator, GRUNDET_ADMIN_OPERATOR, or AdminCli:Operator.");
            return 2;
        }

        Console.WriteLine($"Database target: {result.DatabaseTarget}");

        switch (result.Outcome)
        {
            case WaitlistChangeOutcome.NotFound:
                Console.Error.WriteLine($"No waitlist entry found for {result.NormalizedEmail}.");
                return 1;
            case WaitlistChangeOutcome.Ambiguous:
                Console.Error.WriteLine($"Ambiguous waitlist state: {result.MatchCount} entries found for {result.NormalizedEmail}.");
                return 1;
        }

        PrintMutation(result.Entry!, result.Target, result.Operator, result.Execute);

        switch (result.Outcome)
        {
            case WaitlistChangeOutcome.NotPending:
                Console.WriteLine("Affected rows: 0");
                Console.Error.WriteLine("Refusing to mutate entry because current status is not Pending.");
                return 1;
            case WaitlistChangeOutcome.DryRun:
                Console.WriteLine("Affected rows: 0");
                Console.WriteLine("Dry-run only. Re-run with --execute to write.");
                return 0;
            case WaitlistChangeOutcome.AffectedMismatch:
                Console.WriteLine($"Affected rows: {result.AffectedRows}");
                Console.Error.WriteLine("Refusing to commit because affected row count was not exactly 1.");
                return 1;
            default: // Committed
                Console.WriteLine($"Affected rows: {result.AffectedRows}");
                return 0;
        }
    }

    private static int RenderFeedbackList(FeedbackListResult result)
    {
        Console.WriteLine($"Showing {result.Entries.Count} feedback submissions (limit {result.Limit}){(result.IncludeDeleted ? ", including deleted" : string.Empty)}.");
        foreach (FeedbackEntry entry in result.Entries)
        {
            string state = entry.DeletedAt is not null ? "deleted" : entry.ReadAt is null ? "unread" : "read";
            Console.WriteLine($"{entry.CreatedAt:O}\t{state}\t{entry.Kind}\t{entry.Id}\t{entry.UserEmail}\t{entry.AttachmentCount} file(s)\t{entry.PagePath}\t{Preview(entry.Message)}");
        }

        return 0;
    }

    private static int RenderFeedbackShow(FeedbackShowResult result)
    {
        switch (result.Lookup)
        {
            case FeedbackLookup.NotFound:
                Console.Error.WriteLine($"No feedback submission found for {result.IdEcho}.");
                return 1;
            case FeedbackLookup.Ambiguous:
                Console.Error.WriteLine($"Ambiguous feedback id prefix {result.IdEcho}: matches multiple submissions.");
                return 1;
        }

        PrintFeedback(result.Entry!);
        foreach (FeedbackAttachmentEntry attachment in result.Attachments)
        {
            Console.WriteLine($"Attachment: {attachment.Id} {attachment.StoragePath} {attachment.Width}x{attachment.Height} {attachment.StoredSizeBytes} bytes");
        }

        return 0;
    }

    private static int RenderFeedbackExport(FeedbackExportResult result)
    {
        switch (result.Lookup)
        {
            case FeedbackLookup.NotFound:
                Console.Error.WriteLine($"No feedback submission found for {result.IdEcho}.");
                return 1;
            case FeedbackLookup.Ambiguous:
                Console.Error.WriteLine($"Ambiguous feedback id prefix {result.IdEcho}: matches multiple submissions.");
                return 1;
        }

        foreach (string message in result.AttachmentMessages)
        {
            Console.WriteLine(message);
        }

        Console.WriteLine($"Exported feedback {result.Entry!.Id} to {result.Target}.");
        return 0;
    }

    private static int RenderFeedbackMutate(FeedbackMutateResult result)
    {
        if (result.Outcome == FeedbackMutateOutcome.OperatorMissing)
        {
            Console.Error.WriteLine("Mutating commands require --operator, GRUNDET_ADMIN_OPERATOR, or AdminCli:Operator.");
            return 2;
        }

        Console.WriteLine($"Database target: {result.DatabaseTarget}");

        if (result.Outcome == FeedbackMutateOutcome.NotFound)
        {
            Console.Error.WriteLine($"No active feedback submission found for {result.IdEcho}.");
            return 1;
        }

        if (result.Outcome == FeedbackMutateOutcome.Ambiguous)
        {
            Console.Error.WriteLine($"Ambiguous feedback id prefix {result.IdEcho}: matches multiple submissions.");
            return 1;
        }

        FeedbackEntry entry = result.Entry!;
        Console.WriteLine($"Feedback id: {entry.Id}");
        Console.WriteLine($"User: {entry.UserEmail} ({entry.UserId})");
        Console.WriteLine($"Current status: {(entry.DeletedAt is not null ? "deleted" : entry.ReadAt is null ? "unread" : "read")}");
        Console.WriteLine($"Proposed status: {result.TargetState}");
        Console.WriteLine($"Operator: {result.Operator}");
        Console.WriteLine($"Mode: {(result.Execute ? "execute" : "dry-run")}");

        switch (result.Outcome)
        {
            case FeedbackMutateOutcome.DryRun:
                Console.WriteLine("Affected rows: 0");
                Console.WriteLine("Dry-run only. Re-run with --execute to write.");
                return 0;
            case FeedbackMutateOutcome.AffectedMismatch:
                Console.WriteLine($"Affected rows: {result.AffectedRows}");
                Console.Error.WriteLine("Refusing to commit because affected row count was not exactly 1.");
                return 1;
            default: // Committed
                Console.WriteLine($"Affected rows: {result.AffectedRows}");
                return 0;
        }
    }

    private static void PrintMutation(WaitlistEntry entry, WaitlistStatus targetStatus, string? operatorName, bool execute)
    {
        Console.WriteLine($"Target email: {entry.EmailOriginal}");
        Console.WriteLine($"Normalized email: {entry.EmailNormalized}");
        Console.WriteLine($"Current status: {entry.Status}");
        Console.WriteLine($"Proposed status: {targetStatus}");
        Console.WriteLine($"Operator: {operatorName}");
        Console.WriteLine($"Mode: {(execute ? "execute" : "dry-run")}");
    }

    private static void PrintEntry(WaitlistEntry entry, bool includeReason)
    {
        Console.WriteLine($"Id: {entry.Id}");
        Console.WriteLine($"Email original: {entry.EmailOriginal}");
        Console.WriteLine($"Email normalized: {entry.EmailNormalized}");
        Console.WriteLine($"Name: {entry.Name}");
        Console.WriteLine($"Account type: {entry.AccountType}");
        Console.WriteLine($"Team size: {entry.TeamSize}");
        Console.WriteLine($"Line of work: {entry.LineOfWork}");
        Console.WriteLine($"Organization: {entry.OrganizationName}");
        Console.WriteLine($"Organization country: {entry.OrganizationCountry}");
        Console.WriteLine($"Status: {entry.Status}");
        Console.WriteLine($"Created at: {entry.CreatedAt:O}");
        Console.WriteLine($"Updated at: {entry.UpdatedAt:O}");
        Console.WriteLine($"Approved at: {entry.ApprovedAt:O}");
        Console.WriteLine($"Approved by: {entry.ApprovedBy}");
        Console.WriteLine($"Declined at: {entry.DeclinedAt:O}");
        Console.WriteLine($"Declined by: {entry.DeclinedBy}");
        if (includeReason)
        {
            Console.WriteLine($"Reason: {entry.Reason}");
        }
    }

    private static void PrintFeedback(FeedbackEntry entry)
    {
        Console.WriteLine($"Id: {entry.Id}");
        Console.WriteLine($"User: {entry.UserEmail} ({entry.UserId})");
        Console.WriteLine($"Kind: {entry.Kind}");
        Console.WriteLine($"Page: {entry.PagePath}");
        Console.WriteLine($"Created at: {entry.CreatedAt:O}");
        Console.WriteLine($"Read at: {entry.ReadAt:O}");
        Console.WriteLine($"Deleted at: {entry.DeletedAt:O}");
        Console.WriteLine($"User agent: {entry.UserAgent}");
        Console.WriteLine($"Viewport: {entry.ViewportWidth}x{entry.ViewportHeight}");
        Console.WriteLine($"Locale: {entry.Locale}");
        Console.WriteLine($"Time zone: {entry.TimeZone}");
        Console.WriteLine($"Attachments: {entry.AttachmentCount}");
        Console.WriteLine("Message:");
        Console.WriteLine(entry.Message);
    }

    // --- Arg parsing helpers (unchanged behaviour) ---

    private static Dictionary<string, string?> ParseOptions(string[] args, string[] allowedOptions)
    {
        HashSet<string> allowed = new(allowedOptions, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument: {arg}");
            }

            string name = arg[2..];
            if (!allowed.Contains(name))
            {
                throw new ArgumentException($"Unknown option for this command: {arg}.");
            }

            if (name is "execute" or "include-reason" or "unread" or "include-deleted")
            {
                options[name] = null;
            }
            else
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                options[name] = args[++i];
            }
        }

        return options;
    }

    private static int ParseLimit(Dictionary<string, string?> options) =>
        !options.TryGetValue("limit", out string? value) || string.IsNullOrWhiteSpace(value)
            ? DefaultLimit
            : int.TryParse(value, out int limit) && limit is >= 1 and <= MaxLimit
                ? limit
                : throw new ArgumentException($"--limit must be between 1 and {MaxLimit}.");

    private static WaitlistStatus? ParseOptionalStatus(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : int.TryParse(trimmed, out _) || !Enum.TryParse(trimmed, ignoreCase: true, out WaitlistStatus status)
                ? throw new ArgumentException("--status must be Pending, Approved, or Declined.")
                : status;
    }

    private static string Preview(string value)
        => value.Length <= 80 ? value.ReplaceLineEndings(" ") : string.Concat(value[..77].ReplaceLineEndings(" "), "...");

    public static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  grundet-admin waitlist list [--status pending|approved|declined] [--limit 50]");
        Console.Error.WriteLine("  grundet-admin waitlist show <email> [--include-reason]");
        Console.Error.WriteLine("  grundet-admin waitlist approve <email> --operator <name> [--execute]");
        Console.Error.WriteLine("  grundet-admin waitlist decline <email> --operator <name> [--execute]");
        Console.Error.WriteLine("  grundet-admin feedback list [--unread] [--include-deleted] [--limit 50]");
        Console.Error.WriteLine("  grundet-admin feedback show <id> [--include-deleted]");
        Console.Error.WriteLine("  grundet-admin feedback export <id> --output <dir> [--include-deleted]");
        Console.Error.WriteLine("  grundet-admin feedback mark-read <id> --operator <name> [--execute]");
        Console.Error.WriteLine("  grundet-admin feedback delete <id> --operator <name> [--execute]");
    }
}
