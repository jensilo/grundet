// Draft persistence is opaque jsonb on the backend — the frontend owns the shape.
// Every draft is wrapped in this envelope so we can (a) discard incompatible drafts
// on a future schema change and (b) warn when the underlying entity moved on since
// the draft was started (edit mode).

export type DraftEnvelope<T> = {
  schemaVersion: number;
  // The entity version the draft was started from (its `changedAt`). Null for create drafts.
  baseChangedAt: string | null;
  form: T;
};

export type ParsedDraft<T> =
  { kind: "none" } | { kind: "incompatible" } | { kind: "restored"; form: T; stale: boolean };

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

/**
 * Parse opaque draft content into a typed form payload. Never throws — an absent or
 * malformed draft is a clean empty state, not an error.
 */
export function parseDraftEnvelope<T>(
  content: unknown,
  schemaVersion: number,
  currentBaseChangedAt: string | null
): ParsedDraft<T> {
  if (!isRecord(content)) {
    return { kind: "none" };
  }

  if (content.schemaVersion !== schemaVersion) {
    return { kind: "incompatible" };
  }

  if (!isRecord(content.form)) {
    return { kind: "incompatible" };
  }

  const baseChangedAt = typeof content.baseChangedAt === "string" ? content.baseChangedAt : null;
  const stale =
    currentBaseChangedAt !== null &&
    baseChangedAt !== null &&
    baseChangedAt !== currentBaseChangedAt;

  return { kind: "restored", form: content.form as T, stale };
}

export function buildDraftEnvelope<T>(
  form: T,
  schemaVersion: number,
  baseChangedAt: string | null
): DraftEnvelope<T> {
  return { schemaVersion, baseChangedAt, form };
}
