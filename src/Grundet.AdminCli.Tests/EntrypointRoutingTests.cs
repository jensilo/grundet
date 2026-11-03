using Grundet.AdminCli.Cli;
using Xunit;

namespace Grundet.AdminCli.Tests;

/// <summary>
/// The TUI must never trap a non-interactive caller. These run without Docker: the xUnit host
/// redirects stdout/stdin, so the interactive gate is always false and every case routes to the
/// non-interactive flag path. Asserts the bypass guarantees and the sole sanctioned diff
/// (--help/-h now exit 0).
/// </summary>
public sealed class EntrypointRoutingTests
{
    [Fact]
    public async Task HelpLong_PrintsUsage_Exit0()
    {
        var (exit, _, err) = await RunAsync("--help");
        Assert.Equal(0, exit);
        Assert.Contains("Usage:", err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpShort_PrintsUsage_Exit0()
    {
        var (exit, _, err) = await RunAsync("-h");
        Assert.Equal(0, exit);
        Assert.Contains("Usage:", err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoTui_SoleArg_PrintsUsage_Exit2()
    {
        var (exit, _, err) = await RunAsync("--no-tui");
        Assert.Equal(2, exit);
        Assert.Contains("Usage:", err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoArgs_NonInteractiveHost_PrintsUsage_Exit2_NeverHangs()
    {
        var (exit, _, err) = await RunAsync();
        Assert.Equal(2, exit);
        Assert.Contains("Usage:", err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncompleteSubcommand_RoutesToFlagPath_Exit2()
    {
        var (exit, _, err) = await RunAsync("waitlist");
        Assert.Equal(2, exit);
        Assert.Contains("Usage:", err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommand_RoutesToFlagPath_Exit2()
    {
        var (exit, _, err) = await RunAsync("frobnicate");
        Assert.Equal(2, exit);
        Assert.Contains("Usage:", err, StringComparison.Ordinal);
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            int exit = await Entrypoint.RunAsync(args);
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
