// Soft references let people point at intent and work from inside free text:
// IN-12, WO-4, or a bare #12. Requirements and work items share one per-project
// sequence counter, so a bare number is unambiguous. Nothing is stored as a hard
// link — references are parsed from text and resolved on demand.

export type ReferenceKind = "requirement" | "workItem";

export interface ReferenceToken {
  kind: "reference";
  // The matched source text, e.g. "IN-12" or "#7".
  raw: string;
  // Normalised display label, e.g. "IN-12", "WO-4", or "#7".
  label: string;
  // The value passed to the resolver (?ref= / route segment): "IN-12", "WO-4", or "7".
  query: string;
  // Character offset of the token in the source string (a stable React key).
  start: number;
}

export interface TextToken {
  kind: "text";
  value: string;
  // Character offset of the token in the source string (a stable React key).
  start: number;
}

export type ReferenceTextToken = TextToken | ReferenceToken;

// A reference must use the uppercase prefix and must not be glued to surrounding
// letters/digits (so "xWO-3", "IN-12abc", and lowercase "in-12" are not
// references). The left boundary also excludes a hyphen, so hyphenated words like
// "sign-in-2" or "check-in-12" do not produce false IN-/WO- links. Refs may still
// follow other punctuation and whitespace, e.g. "(IN-12)" or "ref: WO-4".
const REFERENCE_PATTERN = /(?<![A-Za-z0-9-])(IN-\d+|WO-\d+|#\d+)(?![A-Za-z0-9])/g;

function normalize(raw: string): { label: string; query: string } {
  if (raw.startsWith("#")) {
    const sequence = raw.slice(1);
    return { label: `#${sequence}`, query: sequence };
  }

  const [prefix, sequence] = raw.split("-");
  const canonical = `${prefix.toUpperCase()}-${sequence}`;
  return { label: canonical, query: canonical };
}

// Splits text into plain segments and reference tokens, preserving order.
export function parseReferenceTokens(text: string): ReferenceTextToken[] {
  if (!text) {
    return [];
  }

  const tokens: ReferenceTextToken[] = [];
  let lastIndex = 0;

  for (const match of text.matchAll(REFERENCE_PATTERN)) {
    const index = match.index ?? 0;
    if (index > lastIndex) {
      tokens.push({ kind: "text", value: text.slice(lastIndex, index), start: lastIndex });
    }

    const raw = match[0];
    const { label, query } = normalize(raw);
    tokens.push({ kind: "reference", raw, label, query, start: index });
    lastIndex = index + raw.length;
  }

  if (lastIndex < text.length) {
    tokens.push({ kind: "text", value: text.slice(lastIndex), start: lastIndex });
  }

  return tokens;
}

// Whether the text contains at least one resolvable reference.
export function hasReference(text: string | null | undefined): boolean {
  if (!text) {
    return false;
  }

  REFERENCE_PATTERN.lastIndex = 0;
  return REFERENCE_PATTERN.test(text);
}

export function referenceResolverHref(projectId: string, query: string): string {
  return `/projects/${projectId}/ref/${encodeURIComponent(query)}`;
}

export function referenceTargetHref(projectId: string, kind: ReferenceKind, id: string): string {
  return kind === "requirement"
    ? `/projects/${projectId}/requirements/${id}`
    : `/projects/${projectId}/work-items/${id}`;
}

// Product framing: intent (requirements) and work (work items).
export function referenceKindLabel(kind: ReferenceKind): string {
  return kind === "requirement" ? "Intent" : "Work";
}
