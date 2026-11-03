import { CheckSquare, Link2, ListChecks, type LucideIcon } from "lucide-react";

import type { ChangelogEntryResponse } from "@/lib/api/generated/types.gen";
import { dayKey } from "@/lib/format-date";

// Entity kinds and change types mirror the backend enum text exactly (see
// Features/Changelog/ChangelogService.cs). The aggregate endpoint emits these
// as plain strings, so the frontend matches on the same canonical values.

export const ENTITY_KIND_OPTIONS = [
  { value: "Intent", label: "Intent" },
  { value: "Work", label: "Work" },
  { value: "Link", label: "Link" },
] as const;

export const CHANGE_TYPE_OPTIONS = [
  { value: "Created", label: "Created" },
  { value: "Updated", label: "Updated" },
  { value: "Deleted", label: "Deleted" },
  { value: "Linked", label: "Linked" },
  { value: "Unlinked", label: "Unlinked" },
] as const;

// Keyed by entity-kind string. Consumers read this as a member access and render
// the resulting component reference (never a call during render), satisfying the
// react-hooks/static-components rule. Falls back via `?? ENTITY_FALLBACK_ICON`.
export const ENTITY_ICONS: Record<string, LucideIcon> = {
  Intent: ListChecks,
  Work: CheckSquare,
  Link: Link2,
};

export const ENTITY_FALLBACK_ICON: LucideIcon = ListChecks;

export function changeTypeLabel(changeType: string): string {
  return CHANGE_TYPE_OPTIONS.find((option) => option.value === changeType)?.label ?? changeType;
}

// Functional accent for the change verb. Authenticated UI is limited to slate +
// indigo + functional tokens (success/warning/destructive), so additive change
// reads success, removal reads destructive, everything else stays neutral.
export type ChangeTone = "success" | "destructive" | "neutral";

export function changeTypeTone(changeType: string): ChangeTone {
  switch (changeType) {
    case "Created":
    case "Linked":
      return "success";
    case "Deleted":
    case "Unlinked":
      return "destructive";
    default:
      return "neutral";
  }
}

// Only Intent and Work carry per-item version diffs; Link rows are summary-only
// in v1 (the diff endpoints cover requirements and work items only).
export function isExpandableKind(entityKind: string): entityKind is "Intent" | "Work" {
  return entityKind === "Intent" || entityKind === "Work";
}

export interface ChangelogDayGroup {
  key: string;
  // Representative timestamp for the group (its first entry's changedAt).
  // The day *label* is tz-sensitive display and is rendered client-side via
  // <LocalDayLabel> in the viewer's own time zone — see components/app/local-date.tsx.
  // `key` itself stays UTC (see {@link dayKey}) so grouping never depends on viewer tz.
  iso: string;
  entries: ChangelogEntryResponse[];
}

/**
 * Groups changelog entries into contiguous calendar-day buckets, preserving the
 * incoming (reverse-chronological) order both across and within groups.
 */
export function groupEntriesByDay(entries: ChangelogEntryResponse[]): ChangelogDayGroup[] {
  const groups: ChangelogDayGroup[] = [];

  for (const entry of entries) {
    const key = dayKey(entry.changedAt);
    const current = groups[groups.length - 1];

    if (current && current.key === key) {
      current.entries.push(entry);
    } else {
      groups.push({ key, iso: entry.changedAt, entries: [entry] });
    }
  }

  return groups;
}
