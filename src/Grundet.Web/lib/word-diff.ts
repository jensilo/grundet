export type DiffToken = { text: string; type: "unchanged" | "removed" | "added" };

export function wordDiff(before: string, after: string): DiffToken[] {
  const tokenize = (s: string): string[] => s.match(/\S+|\s+/g) ?? [];
  const b = tokenize(before);
  const a = tokenize(after);
  const m = b.length;
  const n = a.length;

  const dp: number[][] = Array.from({ length: m + 1 }, () => new Array(n + 1).fill(0));
  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      dp[i][j] =
        b[i - 1] === a[j - 1] ? dp[i - 1][j - 1] + 1 : Math.max(dp[i - 1][j], dp[i][j - 1]);
    }
  }

  const tokens: DiffToken[] = [];
  let i = m,
    j = n;
  while (i > 0 || j > 0) {
    if (i > 0 && j > 0 && b[i - 1] === a[j - 1]) {
      tokens.unshift({ text: b[i - 1], type: "unchanged" });
      i--;
      j--;
    } else if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
      tokens.unshift({ text: a[j - 1], type: "added" });
      j--;
    } else {
      tokens.unshift({ text: b[i - 1], type: "removed" });
      i--;
    }
  }

  return tokens;
}

/**
 * Fraction of word characters (whitespace ignored) that survive unchanged between
 * the two strings, in [0, 1]. 1 means identical; near 0 means a rewrite. Lets the
 * renderer choose token-level highlighting for edits vs. a whole-block before/after
 * view for rewrites, where token-level diffing would interleave noisy strike-throughs.
 */
export function diffRetention(tokens: DiffToken[]): number {
  const wordLength = (text: string): number => text.replace(/\s/g, "").length;

  let unchanged = 0;
  let removed = 0;
  let added = 0;
  for (const token of tokens) {
    const length = wordLength(token.text);
    if (token.type === "unchanged") unchanged += length;
    else if (token.type === "removed") removed += length;
    else added += length;
  }

  const denominator = Math.max(unchanged + removed, unchanged + added);
  return denominator === 0 ? 1 : unchanged / denominator;
}
