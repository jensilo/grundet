// Field-level comparison helpers shared by the requirement and work-item composers to decide
// whether a form holds "any real change" against its saved baseline. They mirror the backend's
// BuildVersionChanges predicate (RequirementService/WorkItemService) closely enough to avoid the
// composer allowing a `reason` that the backend would then reject as a no-op save. A `reason` is a
// per-version change comment: it only makes sense alongside an actual change.

export function trimmedEqual(a: string, b: string): boolean {
  return a.trim() === b.trim();
}

// Labels are compared case-insensitively and order-independently, matching FieldDiff.ForLabels.
export function labelsEqualIgnoringOrder(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  const normalize = (labels: string[]) => labels.map((label) => label.trim().toLowerCase()).sort();
  const left = normalize(a);
  const right = normalize(b);
  return left.every((label, index) => label === right[index]);
}

// Attachment sets are compared order-independently, matching FieldDiff.ForAttachments.
export function idsEqualIgnoringOrder(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  const set = new Set(a);
  return b.every((id) => set.has(id));
}

// Collapse leading/trailing and internal whitespace runs to a single space, matching the
// backend's FieldDiff.Normalize — an internal-whitespace-only edit ("Given  data" → "Given data")
// is not a change there, so the composer must not treat it as one either.
function normalizeCriterion(value: string): string {
  return value.trim().replace(/\s+/g, " ");
}

// A reordering of acceptance criteria alone still registers as a "moved" change on the backend
// (FieldDiff.ForCriteria), so this stays position-sensitive rather than set-based.
export function acceptanceCriteriaEqual(a: string[], b: string[]): boolean {
  return (
    a.length === b.length &&
    a.every((value, index) => normalizeCriterion(value) === normalizeCriterion(b[index] ?? ""))
  );
}
