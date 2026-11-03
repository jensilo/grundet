// wordDiff tokens are positional with no stable identity — index keys are correct here.
/* eslint-disable react/no-array-index-key */
import { ReferenceText } from "@/components/app/reference-text";
import { cn } from "@/lib/utils";
import { diffRetention, wordDiff } from "@/lib/word-diff";

interface DiffBlockProps {
  // When set, IN-/WO-/# references in surviving and added text become soft links.
  // Omitted for contexts without a single resolving project (e.g. cross-project feeds).
  projectId?: string;
  before: string;
  after: string;
  className?: string;
}

function DiffText({ projectId, text }: { projectId?: string; text: string }) {
  return projectId ? <ReferenceText projectId={projectId} text={text} /> : <>{text}</>;
}

// Below this fraction of surviving word characters, the change reads as a rewrite
// rather than an edit. Token-level highlighting of a rewrite interleaves noisy
// strike-throughs around the few coincidental survivors, so we instead show the
// whole previous text struck through followed by the whole new text.
const REWRITE_RETENTION_THRESHOLD = 0.5;

export function DiffBlock({ projectId, before, after, className }: DiffBlockProps) {
  const tokens = wordDiff(before, after);

  // Only treat genuine before→after replacements as rewrites; a pure addition or
  // removal already renders as a single clean run, so leave those as token diffs.
  const isRewrite =
    before.trim().length > 0 &&
    after.trim().length > 0 &&
    diffRetention(tokens) < REWRITE_RETENTION_THRESHOLD;

  return (
    <div
      className={cn(
        "overflow-hidden rounded-md border border-slate-200 px-3 py-1.5 font-mono text-[13px] leading-[1.6]",
        className
      )}
    >
      {isRewrite ? (
        <>
          <span className="bg-diff-removed-bg text-diff-removed-text block rounded-xs line-through">
            {before}
          </span>
          <span className="bg-diff-added-bg text-diff-added-text mt-1 block rounded-xs">
            <DiffText projectId={projectId} text={after} />
          </span>
        </>
      ) : (
        tokens.map((token, i) => {
          if (token.type === "removed") {
            return (
              <span
                key={i}
                className="bg-diff-removed-bg text-diff-removed-text rounded-xs line-through"
              >
                {token.text}
              </span>
            );
          }
          if (token.type === "added") {
            return (
              <span key={i} className="bg-diff-added-bg text-diff-added-text rounded-xs">
                <DiffText projectId={projectId} text={token.text} />
              </span>
            );
          }
          return (
            <span key={i}>
              <DiffText projectId={projectId} text={token.text} />
            </span>
          );
        })
      )}
    </div>
  );
}
