// wordDiff tokens are positional with no stable identity — index keys are correct here.
/* eslint-disable react/no-array-index-key */
import { cn } from "@/lib/utils";
import { wordDiff } from "@/lib/word-diff";

import { MARKETING_CONTAINER } from "./layout";

// Marketing deviation from the real app's `components/app/version-entry.tsx`:
// the app collapses every row to author + verb + relative date + v{n} chip and
// expands only the latest by default. This showcase does the opposite on purpose
// — it keeps every row's substance (FieldChips + word diff + reason) visible
// inline, because its job is to show rich, glanceable history at a glance, not to
// be an interactive timeline. We adopt the app's visual vocabulary (spine + dot,
// v{n} chip, author/verb/date line, FieldChip, diff tokens) while deliberately
// staying content-rich. Do not "fix" this to match the app's collapse behaviour.

type Field = { label: string; before: string; after: string };

type Entry = {
  version: number;
  author: string;
  verb: string;
  when: string;
  isLatest?: boolean;
  fields?: Field[];
  diff?: { context: string; before: string; after: string };
  reason?: string;
  firstVersion?: boolean;
};

const entries: Entry[] = [
  {
    version: 3,
    author: "elin.sandberg",
    verb: "made changes",
    when: "1h ago",
    isLatest: true,
    fields: [{ label: "Status", before: "Draft", after: "Active" }],
    diff: {
      context: "Grade and attendance data must be",
      before: "shared with parents at the end of each term.",
      after: "accessible to parents in real time.",
    },
    reason:
      "Elin raised a parent council petition for transparency; Clara's motion for real-time access carried 11 – 4.",
  },
  {
    version: 2,
    author: "clara.bernard",
    verb: "made changes",
    when: "Yesterday",
    fields: [{ label: "Priority", before: "Should", after: "Must" }],
  },
  {
    version: 1,
    author: "elin.sandberg",
    verb: "created this",
    when: "3 days ago",
    firstVersion: true,
  },
];

export function ChangelogShowcase() {
  return (
    <section id="changelog" className="border-border border-b">
      <div className={MARKETING_CONTAINER}>
        <div className="grid grid-cols-1 gap-16 lg:grid-cols-[1fr_2fr] lg:gap-20 2xl:gap-28">
          <div className="lg:sticky lg:top-24 lg:self-start">
            <p className="text-muted-foreground font-mono text-[11px] tracking-[0.18em] uppercase">
              Version history
            </p>
            <h2 className="text-foreground mt-3 font-serif text-4xl leading-[1.05] tracking-tight md:text-5xl 2xl:text-6xl">
              See exactly how intent
              <span className="text-primary">.</span>
              <br /> got to where it is.
            </h2>
            <p className="text-muted-foreground mt-6 max-w-md text-[15px] leading-relaxed 2xl:text-base">
              Open any intent and scroll through its version history. Every change, every author,
              every reason preserved, searchable, and safe for humans and agents to trust.
            </p>
          </div>

          <div>
            <div className="mb-6 flex items-baseline justify-between">
              <p className="text-muted-foreground font-mono text-xs">intent / INT-142 / history</p>
              <p className="text-muted-foreground/70 font-mono text-[11px]">3 versions</p>
            </div>

            <div className="border-border bg-card rounded-xl border p-4 sm:p-5 2xl:p-6">
              <ol className="relative space-y-0">
                {entries.map((e, i) => (
                  <Row key={e.version} entry={e} isLast={i === entries.length - 1} />
                ))}
              </ol>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function Row({ entry, isLast }: { entry: Entry; isLast: boolean }) {
  const hasBody = !!(entry.diff || entry.fields || entry.reason || entry.firstVersion);

  return (
    <li className="relative pb-3 pl-6 last:pb-0">
      {/* Timeline spine */}
      {!isLast && (
        <span aria-hidden className="bg-border absolute top-7 bottom-0 left-[5px] w-px" />
      )}
      {/* Timeline dot */}
      <span
        aria-hidden
        className={cn(
          "absolute top-[22px] left-0 size-[11px] rounded-full border-2",
          entry.isLatest ? "border-primary bg-accent" : "border-border bg-card"
        )}
      />

      <div className="px-2 py-2">
        <div className="flex items-center gap-2">
          <span className="min-w-0 flex-1 truncate text-sm 2xl:text-[15px]">
            <span className="text-foreground font-medium">{entry.author}</span>{" "}
            <span className="text-muted-foreground">{entry.verb}</span>
          </span>
          <span className="text-muted-foreground shrink-0 text-xs">{entry.when}</span>
          <span
            className={cn(
              "shrink-0 rounded-sm px-1.5 py-0.5 font-mono text-[0.6875rem]",
              entry.isLatest ? "bg-accent text-accent-foreground" : "bg-muted text-muted-foreground"
            )}
          >
            v{entry.version}
          </span>
        </div>

        {hasBody && (
          <div className="mt-3 space-y-3">
            {entry.diff && <InlineDiff {...entry.diff} />}
            {entry.fields && (
              <div className="flex flex-wrap items-center gap-2 text-xs">
                {entry.fields.map((f) => (
                  <FieldChip key={f.label} {...f} />
                ))}
              </div>
            )}
            {entry.reason && (
              <p className="text-xs leading-relaxed">
                <span className="text-muted-foreground font-medium tracking-[0.04em] uppercase">
                  Because{" "}
                </span>
                <span className="text-muted-foreground">{entry.reason}</span>
              </p>
            )}
            {entry.firstVersion && (
              <p className="text-muted-foreground text-xs">First version – nothing to compare.</p>
            )}
          </div>
        )}
      </div>
    </li>
  );
}

// Mirrors the app's FieldChip styling (version-entry.tsx) with semantic tokens.
function FieldChip({ label, before, after }: Field) {
  return (
    <span className="border-border bg-muted text-muted-foreground inline-flex items-center gap-1.5 rounded-sm border px-2 py-0.5">
      {label}:<span className="text-muted-foreground font-medium line-through">{before}</span>
      <span className="text-muted-foreground/60">→</span>
      <span className="text-foreground font-medium">{after}</span>
    </span>
  );
}

// Word-level diff matching the app's diff tokens/framing (no line-number gutter).
// Rendered as a removed line then an added line — not a single interleaved flow —
// because this edit shares few words and interleaving reads as noise. We do not
// import DiffBlock: its sub-0.5-retention "rewrite" mode drops the word-level
// strike entirely, and we want the surviving words ("parents") to stay highlighted.
function InlineDiff({
  context,
  before,
  after,
}: {
  context: string;
  before: string;
  after: string;
}) {
  const tokens = wordDiff(before, after);
  const removed = tokens.filter((t) => t.type !== "added");
  const added = tokens.filter((t) => t.type !== "removed");

  return (
    <div className="border-border overflow-hidden rounded-md border font-mono text-[12.5px] leading-[1.6] 2xl:text-[13.5px]">
      <div className="text-muted-foreground px-3 py-1">{context}</div>
      <div className="bg-diff-removed-bg text-diff-removed-text px-3 py-1">
        {removed.map((t, i) =>
          t.type === "removed" ? (
            <span key={i} className="line-through">
              {t.text}
            </span>
          ) : (
            <span key={i}>{t.text}</span>
          )
        )}
      </div>
      <div className="bg-diff-added-bg text-diff-added-text px-3 py-1">
        {added.map((t, i) => (
          <span key={i}>{t.text}</span>
        ))}
      </div>
    </div>
  );
}
