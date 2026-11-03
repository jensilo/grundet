import { describe, expect, it } from "vitest";

import type { ChangelogEntryResponse } from "@/lib/api/generated/types.gen";
import {
  changeTypeLabel,
  changeTypeTone,
  groupEntriesByDay,
  isExpandableKind,
} from "@/lib/changelog";

function entry(
  overrides: Partial<ChangelogEntryResponse> & { changedAt: string }
): ChangelogEntryResponse {
  return {
    entryId: overrides.entryId ?? crypto.randomUUID(),
    entityKind: overrides.entityKind ?? "Intent",
    logicalId: overrides.logicalId ?? crypto.randomUUID(),
    reference: overrides.reference ?? "IN-1",
    title: overrides.title ?? "Some change",
    changeType: overrides.changeType ?? "Updated",
    changedAt: overrides.changedAt,
    changedByUserId: overrides.changedByUserId ?? crypto.randomUUID(),
    changedByName: overrides.changedByName ?? "Alex",
    changedByIsActiveMember: overrides.changedByIsActiveMember ?? true,
    featureId: overrides.featureId ?? null,
    linkedWorkItemId: overrides.linkedWorkItemId ?? null,
    linkedRequirementId: overrides.linkedRequirementId ?? null,
  };
}

describe("groupEntriesByDay", () => {
  // Same instant → same day key in any time zone; a 3-day gap differs everywhere.
  const dayOne = "2026-06-13T12:00:00.000Z";
  const dayOneLater = "2026-06-13T12:00:00.000Z";
  const dayTwo = "2026-06-10T12:00:00.000Z";

  it("groups contiguous same-day entries and preserves order", () => {
    const groups = groupEntriesByDay([
      entry({ entryId: "a", changedAt: dayOne }),
      entry({ entryId: "b", changedAt: dayOneLater }),
      entry({ entryId: "c", changedAt: dayTwo }),
    ]);

    expect(groups).toHaveLength(2);
    expect(groups[0].entries.map((e) => e.entryId)).toEqual(["a", "b"]);
    expect(groups[1].entries.map((e) => e.entryId)).toEqual(["c"]);
  });

  it("does not merge non-contiguous days (input is assumed pre-sorted)", () => {
    const groups = groupEntriesByDay([
      entry({ entryId: "a", changedAt: dayOne }),
      entry({ entryId: "c", changedAt: dayTwo }),
      entry({ entryId: "b", changedAt: dayOneLater }),
    ]);

    expect(groups).toHaveLength(3);
  });

  it("returns no groups for an empty list", () => {
    expect(groupEntriesByDay([])).toEqual([]);
  });
});

describe("changeTypeTone", () => {
  it("treats additive changes as success", () => {
    expect(changeTypeTone("Created")).toBe("success");
    expect(changeTypeTone("Linked")).toBe("success");
  });

  it("treats removals as destructive", () => {
    expect(changeTypeTone("Deleted")).toBe("destructive");
    expect(changeTypeTone("Unlinked")).toBe("destructive");
  });

  it("treats everything else as neutral", () => {
    expect(changeTypeTone("Updated")).toBe("neutral");
  });
});

describe("changeTypeLabel", () => {
  it("falls back to the raw value for unknown types", () => {
    expect(changeTypeLabel("Mystery")).toBe("Mystery");
  });
});

describe("isExpandableKind", () => {
  it("is true only for Intent and Work", () => {
    expect(isExpandableKind("Intent")).toBe(true);
    expect(isExpandableKind("Work")).toBe(true);
    expect(isExpandableKind("Link")).toBe(false);
  });
});
