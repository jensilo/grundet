import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import {
  CONFIG_DIR_NAME,
  DEFAULT_MAX_BYTES,
  DEFAULT_MAX_LINES,
  formatSize,
  truncateTail,
} from "@earendil-works/pi-coding-agent";
import { Box, Text } from "@earendil-works/pi-tui";
import { Type, type Static } from "typebox";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawn } from "node:child_process";

const RunCheckParams = Type.Object({
  name: Type.Optional(
    Type.String({ description: "Check name from .pi/check.json. Omit to run the default/all check." }),
  ),
});
type RunCheckInput = Static<typeof RunCheckParams>;

type CheckConfig = {
  default?: string | string[];
  checks?: Record<string, string | string[]>;
};

type CheckMessageDetails = {
  phase?: "running" | "done";
  commands: string[];
  summary?: string;
  fullOutput?: string;
  isError?: boolean;
};

function loadConfig(cwd: string, projectTrusted: boolean): CheckConfig | undefined {
  if (!projectTrusted) return undefined;
  const path = join(cwd, CONFIG_DIR_NAME, "check.json");
  if (!existsSync(path)) return undefined;
  return JSON.parse(readFileSync(path, "utf8")) as CheckConfig;
}

function commandsFor(config: CheckConfig, name?: string): string[] {
  if (name) {
    const value = config.checks?.[name];
    if (!value)
      throw new Error(`Unknown check '${name}'. Available: ${Object.keys(config.checks ?? {}).join(", ") || "none"}`);
    return Array.isArray(value) ? value : [value];
  }
  const value = config.default ?? (config.checks ? Object.keys(config.checks) : []);
  if (Array.isArray(value)) return value.flatMap((v) => (config.checks?.[v] ? commandsFor(config, v) : [v]));
  if (config.checks?.[value]) return commandsFor(config, value);
  return [value];
}

function summarizeOutput(raw: string, isError: boolean): { summary: string; fullOutput: string } {
  const isHeader = (line: string) => line.startsWith("$ ") || line.startsWith("> ");

  // Walk the raw output in order. Header lines ($ cmd / > section) and [/sum:]
  // lines both go into the collapsed summary, so each summary line stays grouped
  // under the header it followed. Everything else (file lists, tool noise) is
  // kept only in the full output shown on expand (ctrl+o). Markers are stripped
  // from the full output so they never leak into the expanded view.
  const summaryLines: string[] = [];
  const fullOutputLines: string[] = [];
  let sawExplicitSummary = false;

  for (const line of raw.split("\n")) {
    const match = /^\[\/sum:\](.*)$/.exec(line);
    if (match) {
      const text = match[1].trim();
      if (text) {
        summaryLines.push(text);
        sawExplicitSummary = true;
      }
      continue;
    }
    fullOutputLines.push(line);
    if (isHeader(line)) summaryLines.push(line);
  }

  const searchableLines = fullOutputLines.map((line) => line.trim()).filter(Boolean);

  // Backwards-compatible fallback for older check scripts. Prefer [/sum:] for new scripts.
  if (!sawExplicitSummary) {
    const prettierSummary = searchableLines.find((line) => /^prettier:\s+.+$/.test(line));
    if (prettierSummary) summaryLines.push(prettierSummary);
  }

  if (isError) {
    const failureLine = searchableLines.find((line) => line.startsWith("FAILED") || /error|warning/i.test(line));
    if (failureLine) summaryLines.push(failureLine);
  }

  return { summary: summaryLines.join("\n"), fullOutput: fullOutputLines.join("\n") };
}

// The run_check tool returns plain text rendered by pi's default tool view,
// which has no /check summary processing. Convert [/sum:] markers into normal
// lines there so the model (and default display) never see the raw prefix.
function stripSummaryMarkers(text: string): string {
  return text
    .split("\n")
    .map((line) => {
      const match = /^\[\/sum:\](.*)$/.exec(line);
      return match ? match[1].trim() : line;
    })
    .join("\n");
}

// Local result shape for the shared command runner. Distinct from
// AgentToolResult: `failed` records success/failure as data (not the tool
// error flag), since the run_check tool converts a failure into a *thrown*
// Error (see execute() below) rather than an `isError: true` return, which pi
// ignores. The /check slash command reads `failed` directly.
type RunCommandsResult = {
  content: [{ type: "text"; text: string }];
  details: { commands: string[]; fullOutput: string };
  failed: boolean;
};

// Writes the full (untruncated) check output to a stable temp file so the
// LLM can be pointed at it when the tool-visible content gets truncated.
// Returns the temp path, or null if the write failed — a failed temp write must
// not mask the (already truncated) check output the caller still needs to return.
function writeTempOutputFile(content: string): string | null {
  try {
    const path = join(tmpdir(), `pi-check-output-${Date.now()}.txt`);
    writeFileSync(path, content, "utf8");
    return path;
  } catch {
    return null;
  }
}

type ExecResult = { stdout: string; stderr: string; code: number; killed: boolean };

// pi's exported pi.exec() (core `execCommand`) only sends the abort signal to
// the single process it spawns directly. Check commands are shell scripts or
// package-manager commands (`npm test`, `.pi/checks/foo.sh`, ...) that
// themselves spawn further child processes, so a SIGTERM aimed only at the
// immediate process never reaches the real work — verified empirically: with
// pi.exec("/bin/sh", ["-c", "sleep 5"], { signal }) an aborted signal still
// let `sleep` run to completion. To make Esc actually kill a running check,
// run the command in its own detached process group and signal the whole
// group on abort.
function runCommand(command: string, cwd: string, signal: AbortSignal | undefined): Promise<ExecResult> {
  return new Promise((resolve) => {
    const proc = spawn(command, {
      cwd,
      shell: true,
      detached: process.platform !== "win32",
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";
    let killed = false;
    let settled = false;
    let killTimer: NodeJS.Timeout | undefined;
    const killGroup = (sig: NodeJS.Signals) => {
      if (!proc.pid) return;
      try {
        if (process.platform === "win32") spawn("taskkill", ["/pid", String(proc.pid), "/t", "/f"]);
        else process.kill(-proc.pid, sig);
      } catch {
        // Process/group may have already exited.
      }
    };
    const onAbort = () => {
      if (killed) return;
      killed = true;
      killGroup("SIGTERM");
      // Escalate if SIGTERM didn't take. Tracked so `close` can cancel it —
      // otherwise a late SIGKILL could hit a recycled pid's group.
      killTimer = setTimeout(() => killGroup("SIGKILL"), 3000);
      killTimer.unref();
    };
    const finish = (code: number) => {
      if (settled) return;
      settled = true;
      if (killTimer) clearTimeout(killTimer);
      signal?.removeEventListener("abort", onAbort);
      resolve({ stdout, stderr, code, killed });
    };
    if (signal) {
      if (signal.aborted) onAbort();
      else signal.addEventListener("abort", onAbort, { once: true });
    }
    proc.stdout?.on("data", (data: Buffer) => (stdout += data.toString()));
    proc.stderr?.on("data", (data: Buffer) => (stderr += data.toString()));
    // A spawn failure (e.g. shell missing) would otherwise throw as an
    // unhandled 'error' event; surface it as a non-zero exit with the message.
    proc.on("error", (err: Error) => {
      stderr += (stderr ? "\n" : "") + err.message;
      finish(127);
    });
    proc.on("close", (code) => finish(code ?? 0));
  });
}

async function runCommands(
  cwd: string,
  commands: string[],
  signal: AbortSignal | undefined,
  onProgress?: (message: string, details?: Record<string, unknown>) => void,
): Promise<RunCommandsResult> {
  const chunks: string[] = [];
  for (const [index, command] of commands.entries()) {
    const message = `Running check ${index + 1}/${commands.length}: ${command}`;
    onProgress?.(`${message}\nThis may take a while for formatting, linting, or tests.`, {
      command,
      index,
      total: commands.length,
    });
    chunks.push(`$ ${command}`);
    const result = await runCommand(command, cwd, signal);
    if (result.stdout.trim()) chunks.push(result.stdout.trimEnd());
    if (result.stderr.trim()) chunks.push(result.stderr.trimEnd());
    if (signal?.aborted) {
      chunks.push("Cancelled.");
      return {
        content: [{ type: "text", text: chunks.join("\n") }],
        details: { commands, fullOutput: chunks.join("\n") },
        failed: false,
      };
    }
    if (result.code !== 0 || result.killed) {
      chunks.push(`FAILED (exit ${result.code})`);
      return {
        content: [{ type: "text", text: chunks.join("\n") }],
        details: { commands, fullOutput: chunks.join("\n") },
        failed: true,
      };
    }
  }
  return {
    content: [{ type: "text", text: chunks.join("\n") || "No output." }],
    details: { commands, fullOutput: chunks.join("\n") || "No output." },
    failed: false,
  };
}

function renderCollapsedBody(
  message: { content: string; details?: CheckMessageDetails },
  theme: { fg(color: string, text: string): string },
): string {
  const lines = message.content.split("\n");
  if ((message.details?.phase ?? "done") === "running") {
    return lines.map((line, index) => (index === 0 ? line : theme.fg("dim", line))).join("\n");
  }
  const summaryColor = message.details?.isError === true ? "error" : "success";
  return lines
    .map((line) =>
      line.startsWith("$ ") || line.startsWith("> ") ? theme.fg("dim", line) : theme.fg(summaryColor, line),
    )
    .join("\n");
}

// Shared body for both the /check message and the run_check tool result: the
// colored collapsed summary, plus the full output under a divider when expanded.
function renderCheckBody(
  message: { content: string; details?: CheckMessageDetails },
  expanded: boolean,
  theme: { fg(color: string, text: string): string },
): string {
  let text = renderCollapsedBody(message, theme);
  if (expanded) {
    const fullOutput = message.details?.fullOutput?.trim();
    if (fullOutput) {
      text += `\n${theme.fg("dim", "─".repeat(24))}\n${theme.fg("dim", fullOutput)}`;
    }
  }
  return text;
}

function renderCheckMessage(
  message: { content: string; details?: CheckMessageDetails },
  expanded: boolean,
  theme: {
    fg(color: string, text: string): string;
    bg(color: string, text: string): string;
    bold(text: string): string;
  },
) {
  const phase = message.details?.phase;
  const isError = message.details?.isError === true;
  const header = phase === "running" ? "⏳ check" : isError ? "✗ check" : "✓ check";
  const headerColor = phase === "running" ? "warning" : isError ? "error" : "success";

  const text = `${theme.fg(headerColor, theme.bold(header))}\n${renderCheckBody(message, expanded, theme)}`;
  const box = new Box(1, 1, (t) => theme.bg("customMessageBg", t));
  box.addChild(new Text(text, 0, 0));
  return box;
}

export default function checkExtension(pi: ExtensionAPI) {
  pi.registerMessageRenderer("check", (message, options, theme) =>
    renderCheckMessage(message, options.expanded, theme),
  );

  pi.registerCommand("check", {
    description: "Run project-local checks from .pi/check.json",
    handler: async (args, ctx) => {
      const config = loadConfig(ctx.cwd, ctx.isProjectTrusted());
      if (!config) return ctx.ui.notify("No .pi/check.json found", "error");
      try {
        const commands = commandsFor(config, args.trim() || undefined);
        const started = `Running ${commands.length} command${commands.length === 1 ? "" : "s"}. May take a while.\n$ ${commands.join("\n$ ")}`;
        ctx.ui.notify("Check running… formatting/linting may take a while", "info");
        pi.sendMessage({
          customType: "check",
          content: started,
          display: true,
          details: { phase: "running", commands } satisfies CheckMessageDetails,
        });
        const res = await runCommands(ctx.cwd, commands, undefined, (message) => ctx.ui.notify(message, "info"));
        ctx.ui.notify(res.failed ? "Check failed" : "Check passed", res.failed ? "error" : "info");
        const raw = res.content
          .map((c) => (c.type === "text" ? c.text : ""))
          .join("\n")
          .trim();
        const { summary, fullOutput } = summarizeOutput(raw, res.failed);
        pi.sendMessage({
          customType: "check",
          content: summary,
          display: true,
          details: {
            phase: "done",
            commands,
            fullOutput,
            isError: res.failed,
          } satisfies CheckMessageDetails,
        });
      } catch (e) {
        ctx.ui.notify((e as Error).message, "error");
      }
    },
  });

  pi.registerTool({
    name: "run_check",
    label: "Run Check",
    description:
      "Use this after making code changes and before final handoff when .pi/check.json exists. Runs the repo's project-local verification for agents: formatting, linting, tests, or other configured checks. If missing setup, fail gracefully; inform the user very briefly at most once. For setup docs, read .pi/extensions/check/README.md. No automatic fix loop. " +
      `Output shown to the model is capped at ${formatSize(DEFAULT_MAX_BYTES)} / ${DEFAULT_MAX_LINES} lines (keeping the tail); when truncated, the full output is saved to a temp file whose path is included in the result.`,
    parameters: RunCheckParams,
    async execute(_id, params: RunCheckInput, signal, onUpdate, ctx) {
      const config = loadConfig(ctx.cwd, ctx.isProjectTrusted());
      if (!config) throw new Error("No .pi/check.json found.");
      // Let commandsFor's Error (e.g. unknown check name) propagate: pi only
      // sets the tool error flag when execute() throws, so we don't catch it
      // here — a caught-and-returned isError:true would be a silent no-op.
      const commands = commandsFor(config, params.name);
      onUpdate?.({
        content: [
          {
            type: "text",
            text: `Running ${commands.length} check command${commands.length === 1 ? "" : "s"}. Formatting, linting, or tests may take a while.`,
          },
        ],
        details: { commands },
      });
      const res = await runCommands(ctx.cwd, commands, signal, (message, details) =>
        onUpdate?.({ content: [{ type: "text", text: message }], details }),
      );
      const raw = res.content.map((c) => (c.type === "text" ? c.text : "")).join("\n");
      const { summary, fullOutput } = summarizeOutput(raw, res.failed);
      // The model receives clean full output (markers -> plain lines); the
      // grouped summary rides along in details for renderResult to display.
      const stripped = stripSummaryMarkers(raw) || "No output.";
      const truncation = truncateTail(stripped, { maxLines: DEFAULT_MAX_LINES, maxBytes: DEFAULT_MAX_BYTES });
      let text = truncation.content;
      if (truncation.truncated) {
        const tempFile = writeTempOutputFile(stripped);
        const where = tempFile ? ` Full output saved to: ${tempFile}` : " (temp file could not be written)";
        text +=
          `\n\n[Output truncated: ${truncation.outputLines} of ${truncation.totalLines} lines` +
          ` (${formatSize(truncation.outputBytes)} of ${formatSize(truncation.totalBytes)}).` +
          `${where}]`;
      }
      if (res.failed) {
        // Throwing (not returning isError:true) is what actually sets pi's
        // tool error flag; details are discarded on the thrown path, so
        // everything the model needs to know must be in this message.
        throw new Error(text);
      }
      return {
        content: [{ type: "text", text }],
        details: { commands, summary, fullOutput, isError: false } satisfies CheckMessageDetails,
      };
    },
    renderResult(result, options, theme) {
      const details = result.details as CheckMessageDetails | undefined;
      // Streaming/partial updates (or anything without a computed summary) just
      // show their plain text; pi's standard tool shell frames it.
      if (options.isPartial || !details?.summary) {
        const text = result.content.map((c) => (c.type === "text" ? c.text : "")).join("\n");
        return new Text(text, 0, 0);
      }
      const body = renderCheckBody({ content: details.summary, details }, options.expanded, theme);
      return new Text(body, 0, 0);
    },
  });
}
