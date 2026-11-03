import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { VersionEntry } from "@/components/app/version-entry";
import type {
  RequirementVersionChanges,
  RequirementVersionEntry,
  WorkItemVersionChanges,
  WorkItemVersionEntry,
} from "@/lib/api/generated/types.gen";

function emptyRequirementChanges(): RequirementVersionChanges {
  return {
    text: null,
    acceptanceCriteria: null,
    labels: null,
    status: null,
    priority: null,
    rationale: null,
    feature: null,
    attachments: null,
  };
}

function emptyWorkChanges(): WorkItemVersionChanges {
  return {
    title: null,
    description: null,
    status: null,
    priority: null,
    type: null,
    assignedToUserId: null,
    blockedReason: null,
    labels: null,
    attachments: null,
  };
}

function requirementVersion(
  changes: RequirementVersionChanges | null,
  reason: string | null = null
): RequirementVersionEntry {
  return {
    versionId: "v-2",
    versionNumber: 2,
    changedAt: new Date().toISOString(),
    changedByUserId: "u-1",
    changedByDisplayName: "Ada",
    changeType: "Updated",
    changes,
    reason,
  };
}

function workItemVersion(
  changes: WorkItemVersionChanges | null,
  reason: string | null = null
): WorkItemVersionEntry {
  return {
    versionId: "v-2",
    versionNumber: 2,
    changedAt: new Date().toISOString(),
    changedByUserId: "u-1",
    changedByDisplayName: "Ada",
    changeType: "Updated",
    changes,
    reason,
  };
}

describe("VersionEntry summary", () => {
  it("surfaces an image change for a requirement even when a primary field also changed", () => {
    // Regression: the requirement summary used to gate the "… changed." line behind `!hasPrimary`,
    // so an image change combined with a text edit silently disappeared.
    const version = requirementVersion({
      ...emptyRequirementChanges(),
      text: { before: "old text", after: "new text" },
      attachments: { added: ["a-1"], removed: [] },
    });

    render(
      <VersionEntry
        version={version}
        projectId="p1"
        expanded={true}
        onToggle={vi.fn()}
        isLatest={true}
        isLast={true}
      />
    );

    expect(screen.getByText("Image(s) changed.")).toBeInTheDocument();
  });

  it("surfaces an image change for a work item combined with a scalar change", () => {
    const version = workItemVersion({
      ...emptyWorkChanges(),
      status: { before: "Todo", after: "Done" },
      attachments: { added: ["a-1"], removed: ["a-2"] },
    });

    render(
      <VersionEntry
        version={version}
        projectId="p1"
        expanded={true}
        onToggle={vi.fn()}
        isLatest={true}
        isLast={true}
      />
    );

    expect(screen.getByText("Image(s) changed.")).toBeInTheDocument();
  });

  it("does not list Reason in the summary of changed fields", () => {
    const version = requirementVersion(
      { ...emptyRequirementChanges(), labels: { added: ["x"], removed: [] } },
      "Because the label was wrong."
    );

    render(
      <VersionEntry
        version={version}
        projectId="p1"
        expanded={true}
        onToggle={vi.fn()}
        isLatest={true}
        isLast={true}
      />
    );

    expect(screen.getByText("Labels changed.")).toBeInTheDocument();
  });

  it("renders the version's reason as a note in the expanded section", () => {
    const version = requirementVersion(
      { ...emptyRequirementChanges(), text: { before: "old", after: "new" } },
      "Tightened the wording."
    );

    render(
      <VersionEntry
        version={version}
        projectId="p1"
        expanded={true}
        onToggle={vi.fn()}
        isLatest={true}
        isLast={true}
      />
    );

    expect(screen.getByText("Tightened the wording.")).toBeInTheDocument();
  });

  it("shows the reason note instead of 'No field-level changes recorded' for a reason-only version", () => {
    const version = workItemVersion(null, "Left a note only.");

    render(
      <VersionEntry
        version={version}
        projectId="p1"
        expanded={true}
        onToggle={vi.fn()}
        isLatest={true}
        isLast={true}
      />
    );

    expect(screen.getByText("Left a note only.")).toBeInTheDocument();
    expect(screen.queryByText("No field-level changes recorded.")).not.toBeInTheDocument();
  });
});
