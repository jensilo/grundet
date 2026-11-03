import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { VersionHistoryDialog } from "@/components/app/version-history-dialog";
import type { RequirementVersionEntry } from "@/lib/api/generated/types.gen";

const versions: RequirementVersionEntry[] = [
  {
    versionId: "ver-1",
    versionNumber: 1,
    changedAt: "2026-06-13T12:00:00.000Z",
    changedByUserId: "u-1",
    changedByDisplayName: "Alex",
    changeType: "Created",
    changes: {
      text: null,
      acceptanceCriteria: null,
      labels: null,
      status: { before: "Draft", after: "Active" },
      priority: null,
      rationale: null,
      feature: null,
      attachments: null,
    },
    reason: null,
  },
];

describe("VersionHistoryDialog", () => {
  it("opens the full history from already-loaded versions and links to the history page", async () => {
    const user = userEvent.setup();
    render(
      <VersionHistoryDialog
        versions={versions}
        projectId="p-1"
        reference="IN-1"
        title="Add login"
        historyHref="/projects/p-1/requirements/req-1/history"
      />
    );

    // Closed by default — history content is not rendered.
    expect(screen.queryByText("Active")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /view full history/i }));

    expect(await screen.findByText("Add login")).toBeInTheDocument();
    // The full per-version diff renders inside the dialog.
    expect(screen.getByText("Active")).toBeInTheDocument();

    const pageLink = screen.getByRole("link", { name: /open as page/i });
    expect(pageLink).toHaveAttribute("href", "/projects/p-1/requirements/req-1/history");
  });
});
