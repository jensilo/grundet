using Grundet.AdminCli.Tui;

namespace Grundet.AdminCli.Cli;

/// <summary>
/// Selects the CLI face. The interactive TUI launches only when ALL hold: no subcommand args,
/// an interactive TTY (stdin and stdout not redirected), and no explicit opt-out. Every other
/// path routes to the non-interactive flag dispatcher, so agents, pipes, and CI can never be
/// trapped in a blocking prompt.
/// </summary>
internal static class Entrypoint
{
    public static async Task<int> RunAsync(string[] args)
    {
        // --help/-h are sole-arg only and the single sanctioned exit-code change from the
        // baseline: usage text to stderr, but exit 0 instead of the legacy fall-through exit 2.
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            FlagDispatcher.PrintUsage();
            return 0;
        }

        // --no-tui as the sole arg is a hard escape hatch: print usage, exit 2 (same as no-args
        // on the flag path). Combined with a subcommand it is unsupported and errors as today.
        if (args.Length == 1 && args[0] == "--no-tui")
        {
            FlagDispatcher.PrintUsage();
            return 2;
        }

        bool optedOut = string.Equals(Environment.GetEnvironmentVariable("GRUNDET_ADMIN_NO_TUI"), "1", StringComparison.Ordinal);
        bool wantsTui =
            args.Length == 0
            && !optedOut
            && !Console.IsInputRedirected
            && !Console.IsOutputRedirected;

        return wantsTui
            ? await TuiApp.RunAsync()
            : await FlagDispatcher.RunAsync(args);
    }
}
