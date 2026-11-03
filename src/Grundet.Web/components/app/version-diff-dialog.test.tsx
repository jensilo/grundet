import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { type DiffDialogTarget, VersionDiffDialog } from "@/components/app/version-diff-dialog";
import {
  getRequirementVersionHistory,
  getWorkItemVersionHistory,
} from "@/lib/api/generated/sdk.gen";

vi.mock("@/lib/api/generated/sdk.gen", () => {
  const requirementVersion = {
    versionId: "ver-1",
    versionNumber: 3,
    changedAt: "2026-06-13T12:00:00.000Z",
    changedByUserId: "u-1",
    changedByDisplayName: "Alex",
    changeType: "Updated",
    changes: {
      text: null,
      acceptanceCriteria: null,
      labels: null,
      status: { before: "Draft", after: "Active" },
      priority: null,
    },
    reason: null,
  };

  return {
    getRequirementVersionHistory: vi.fn(async () => ({
      data: [requirementVersion],
      error: undefined,
    })),
    getWorkItemVersionHistory: vi.fn(async () => ({ data: [], error: undefined })),
  };
});

const target: DiffDialogTarget = {
  entityKind: "Intent",
  logicalId: "req-1",
  versionId: "ver-1",
  reference: "IN-1",
  title: "Add login",
  changedByIsActiveMember: true,
};

describe("VersionDiffDialog", () => {
  it("fetches the version for the target and renders its field-level changes", async () => {
    render(<VersionDiffDialog target={target} projectId="proj-1" onClose={() => {}} />);

    expect(await screen.findByText("Add login")).toBeInTheDocument();
    // Status diff renders before → after.
    expect(await screen.findByText("Draft")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();

    expect(getRequirementVersionHistory).toHaveBeenCalledWith({
      path: { requirementId: "req-1" },
    });
    expect(getWorkItemVersionHistory).not.toHaveBeenCalled();
  });

  it("does not fetch when there is no target", () => {
    vi.mocked(getRequirementVersionHistory).mockClear();
    render(<VersionDiffDialog target={null} projectId="proj-1" onClose={() => {}} />);

    expect(getRequirementVersionHistory).not.toHaveBeenCalled();
  });
});
