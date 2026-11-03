import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { ChangelogTimeline } from "@/components/app/changelog-timeline";
import type { ChangelogEntryResponse } from "@/lib/api/generated/types.gen";

// The dialog fetches via the generated client; isolate the timeline render from it.
vi.mock("@/components/app/version-diff-dialog", () => ({
  VersionDiffDialog: () => null,
}));

const DAY = "2026-06-13T12:00:00.000Z";

const entries: ChangelogEntryResponse[] = [
  {
    entryId: "v-intent",
    entityKind: "Intent",
    logicalId: "req-1",
    reference: "IN-1",
    title: "Add login",
    changeType: "Created",
    changedAt: DAY,
    changedByUserId: "u-1",
    changedByName: "Alex",
    changedByIsActiveMember: true,
    featureId: "f-1",
    linkedWorkItemId: null,
    linkedRequirementId: null,
  },
  {
    entryId: "v-work",
    entityKind: "Work",
    logicalId: "wi-2",
    reference: "WO-2",
    title: "Build the form",
    changeType: "Updated",
    changedAt: DAY,
    changedByUserId: "u-1",
    changedByName: "Alex",
    changedByIsActiveMember: true,
    featureId: null,
    linkedWorkItemId: null,
    linkedRequirementId: null,
  },
  // Link rows carry the work item in `reference` and the requirement in `title`,
  // with both logical ids exposed so each end links to its own detail page.
  {
    entryId: "v-link",
    entityKind: "Link",
    logicalId: "link-3",
    reference: "WO-7",
    title: "IN-5",
    changeType: "Linked",
    changedAt: DAY,
    changedByUserId: "u-1",
    changedByName: "Alex",
    changedByIsActiveMember: true,
    featureId: null,
    linkedWorkItemId: "wi-7",
    linkedRequirementId: "req-5",
  },
];

describe("ChangelogTimeline", () => {
  it("renders references, titles, and change labels for mixed entity kinds", () => {
    render(<ChangelogTimeline entries={entries} projectId="p1" />);

    expect(screen.getByText("IN-1")).toBeInTheDocument();
    expect(screen.getByText("WO-2")).toBeInTheDocument();
    expect(screen.getByText("Add login")).toBeInTheDocument();
    expect(screen.getByText("WO-7")).toBeInTheDocument();
    expect(screen.getByText("IN-5")).toBeInTheDocument();
    expect(screen.getByText("Created")).toBeInTheDocument();
    expect(screen.getByText("Linked")).toBeInTheDocument();
  });

  it("links both ends of a Link row to their detail pages", () => {
    render(<ChangelogTimeline entries={entries} projectId="p1" />);

    // Title (IN-…) → requirement, reference (WO-…) → work item.
    expect(screen.getByRole("link", { name: "IN-5" })).toHaveAttribute(
      "href",
      "/projects/p1/requirements/req-5"
    );
    expect(screen.getByRole("link", { name: "WO-7" })).toHaveAttribute(
      "href",
      "/projects/p1/work-items/wi-7"
    );
  });

  it("links Intent and Work titles to their detail pages", () => {
    render(<ChangelogTimeline entries={entries} projectId="p1" />);

    expect(screen.getByRole("link", { name: "Add login" })).toHaveAttribute(
      "href",
      "/projects/p1/requirements/req-1"
    );
    expect(screen.getByRole("link", { name: "Build the form" })).toHaveAttribute(
      "href",
      "/projects/p1/work-items/wi-2"
    );
  });

  it("links Intent and Work reference chips to the same detail page as the title", () => {
    render(<ChangelogTimeline entries={entries} projectId="p1" />);

    expect(screen.getByRole("link", { name: "IN-1" })).toHaveAttribute(
      "href",
      "/projects/p1/requirements/req-1"
    );
    expect(screen.getByRole("link", { name: "WO-2" })).toHaveAttribute(
      "href",
      "/projects/p1/work-items/wi-2"
    );
  });

  it("marks authors who have left the team as former members", () => {
    const formerAuthor: ChangelogEntryResponse[] = [
      { ...entries[0], entryId: "v-former", changedByName: "Ada", changedByIsActiveMember: false },
    ];
    render(<ChangelogTimeline entries={formerAuthor} projectId="p1" />);

    expect(screen.getByText(/Ada/)).toBeInTheDocument();
    expect(screen.getByText(/former member/)).toBeInTheDocument();
  });

  it("falls back to an unknown-member label without a former marker when the author is gone", () => {
    const deletedAuthor: ChangelogEntryResponse[] = [
      { ...entries[0], entryId: "v-gone", changedByName: null, changedByIsActiveMember: false },
    ];
    render(<ChangelogTimeline entries={deletedAuthor} projectId="p1" />);

    expect(screen.getByText(/Unknown member/)).toBeInTheDocument();
    expect(screen.queryByText(/former member/)).not.toBeInTheDocument();
  });

  it('offers "See changes" only for expandable Intent and Work rows', () => {
    render(<ChangelogTimeline entries={entries} projectId="p1" />);

    expect(screen.getAllByRole("button", { name: "See changes" })).toHaveLength(2);
    // Link rows carry no per-item diff, so they offer no "See changes" affordance.
    expect(screen.getByRole("link", { name: "IN-5" })).toBeInTheDocument();
  });
});
