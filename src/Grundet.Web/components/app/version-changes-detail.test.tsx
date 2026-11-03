import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { VersionChangesDetail } from "@/components/app/version-changes-detail";
import type { RequirementVersionChanges } from "@/lib/api/generated/types.gen";

// AttachmentImage streams bytes through the generated client; stub it so the gallery thumbnails
// render their fallback synchronously without a real request.
vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getAttachmentContent: vi.fn().mockResolvedValue({ data: undefined, error: { status: 404 } }),
}));

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

describe("VersionChangesDetail", () => {
  it("shows the first-version copy only for the genuine initial version", () => {
    render(<VersionChangesDetail changes={null} projectId="p1" isInitial={true} />);
    expect(screen.getByText("First version – nothing to compare.")).toBeInTheDocument();
  });

  it("does not call a later no-op version the first version", () => {
    render(<VersionChangesDetail changes={null} projectId="p1" isInitial={false} />);
    expect(screen.queryByText("First version – nothing to compare.")).not.toBeInTheDocument();
    expect(screen.getByText("No field-level changes recorded.")).toBeInTheDocument();
  });

  it("pairs attachments as old → new rows when added and removed counts match", async () => {
    const changes: RequirementVersionChanges = {
      ...emptyRequirementChanges(),
      attachments: { added: ["new-1", "new-2"], removed: ["old-1", "old-2"] },
    };

    render(<VersionChangesDetail changes={changes} projectId="p1" isInitial={false} />);

    expect(screen.getByText("Image(s)")).toBeInTheDocument();
    // Replacement layout: no Removed/Added group headings, one thumbnail per id.
    expect(screen.queryByText("Removed")).not.toBeInTheDocument();
    expect(screen.queryByText("Added")).not.toBeInTheDocument();
    const fallbacks = await screen.findAllByText("Unavailable");
    expect(fallbacks).toHaveLength(4);
  });

  it("groups attachments under Removed and Added when counts differ", async () => {
    const changes: RequirementVersionChanges = {
      ...emptyRequirementChanges(),
      attachments: { added: ["new-1", "new-2"], removed: ["old-1"] },
    };

    render(<VersionChangesDetail changes={changes} projectId="p1" isInitial={false} />);

    expect(screen.getByText("Removed")).toBeInTheDocument();
    expect(screen.getByText("Added")).toBeInTheDocument();
    const fallbacks = await screen.findAllByText("Unavailable");
    expect(fallbacks).toHaveLength(3);
  });

  it("shows only the Added group for pure additions", async () => {
    const changes: RequirementVersionChanges = {
      ...emptyRequirementChanges(),
      attachments: { added: ["new-1", "new-2"], removed: [] },
    };

    render(<VersionChangesDetail changes={changes} projectId="p1" isInitial={false} />);

    expect(screen.getByText("Added")).toBeInTheDocument();
    expect(screen.queryByText("Removed")).not.toBeInTheDocument();
    const fallbacks = await screen.findAllByText("Unavailable");
    expect(fallbacks).toHaveLength(2);
  });

  it("renders a Because (rationale) diff for a requirement", () => {
    const changes: RequirementVersionChanges = {
      ...emptyRequirementChanges(),
      rationale: { before: "Old reason it mattered.", after: "New reason it matters." },
    };

    render(<VersionChangesDetail changes={changes} projectId="p1" isInitial={false} />);

    expect(screen.getByText("Because")).toBeInTheDocument();
    expect(screen.getByText("Old reason it mattered.")).toBeInTheDocument();
    expect(screen.getByText("New reason it matters.")).toBeInTheDocument();
  });

  it("renders a Feature diff for a requirement moved to a different feature", () => {
    const changes: RequirementVersionChanges = {
      ...emptyRequirementChanges(),
      feature: { before: "Old Feature", after: "New Feature" },
    };

    render(<VersionChangesDetail changes={changes} projectId="p1" isInitial={false} />);

    expect(screen.getByText("Feature")).toBeInTheDocument();
    expect(screen.getByText("Old Feature")).toBeInTheDocument();
    expect(screen.getByText("New Feature")).toBeInTheDocument();
  });

  it("renders the reason as a labelled note, never a strikethrough diff", () => {
    const changes: RequirementVersionChanges = {
      ...emptyRequirementChanges(),
      text: { before: "old text", after: "new text" },
    };

    render(
      <VersionChangesDetail
        changes={changes}
        projectId="p1"
        isInitial={false}
        reason="Fixed a typo."
      />
    );

    expect(screen.getByText("Reason")).toBeInTheDocument();
    expect(screen.getByText("Fixed a typo.")).toBeInTheDocument();
    // A diffed field would render both a struck-through "before" and an "after" value.
    expect(screen.queryByText("old text", { selector: ".line-through" })).not.toBeInTheDocument();
  });

  it("shows the reason note for a reason-only version instead of the no-changes copy", () => {
    render(
      <VersionChangesDetail
        changes={null}
        projectId="p1"
        isInitial={false}
        reason="Left a note only."
      />
    );

    expect(screen.getByText("Left a note only.")).toBeInTheDocument();
    expect(screen.queryByText("No field-level changes recorded.")).not.toBeInTheDocument();
  });
});
