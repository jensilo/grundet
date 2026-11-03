import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import type { WorkItemResponse } from "@/lib/api/generated/types.gen";

import { WorkItemCardGrid } from "./work-item-card-grid";

const baseItem: WorkItemResponse = {
  id: "item-1",
  workItemId: "item-1",
  projectId: "proj-1",
  title: "Fix login bug",
  description: null,
  status: "Todo",
  priority: "High",
  labels: [],
  assignedToUserId: null,
  assignedToDisplayName: null,
  blockedReason: null,
  reason: null,
  sequenceNumber: 1,
  ref: "W-1",
  type: "Bug",
  changedAt: new Date().toISOString(),
  changedByUserId: "user-1",
  changeType: "Created",
  reasonPrompted: false,
};

const searchParams = {};
const pathname = "/projects/proj-1/work-items";
const projectId = "proj-1";

describe("WorkItemCardGrid", () => {
  it("renders the assignee display name when present", () => {
    const item: WorkItemResponse = { ...baseItem, assignedToDisplayName: "Alice Smith" };
    render(
      <WorkItemCardGrid
        workItems={[item]}
        projectId={projectId}
        pathname={pathname}
        searchParams={searchParams}
      />
    );

    expect(screen.getByTitle("Alice Smith")).toBeInTheDocument();
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
  });

  it("renders 'Unassigned' when assignedToDisplayName is null", () => {
    const item: WorkItemResponse = { ...baseItem, assignedToDisplayName: null };
    render(
      <WorkItemCardGrid
        workItems={[item]}
        projectId={projectId}
        pathname={pathname}
        searchParams={searchParams}
      />
    );

    expect(screen.getByTitle("Unassigned")).toBeInTheDocument();
    expect(screen.getByText("Unassigned")).toBeInTheDocument();
  });

  it("renders the Blocked badge as a link containing blocked=true without page param", () => {
    const item: WorkItemResponse = {
      ...baseItem,
      blockedReason: "Waiting on external dependency",
    };
    render(
      <WorkItemCardGrid
        workItems={[item]}
        projectId={projectId}
        pathname={pathname}
        searchParams={searchParams}
      />
    );

    const link = screen.getByRole("link", { name: "Blocked" });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", expect.stringContaining("blocked=true"));
    expect(link).not.toHaveAttribute("href", expect.stringContaining("page="));
  });

  it("does not render the Blocked badge when blockedReason is null", () => {
    render(
      <WorkItemCardGrid
        workItems={[baseItem]}
        projectId={projectId}
        pathname={pathname}
        searchParams={searchParams}
      />
    );

    expect(screen.queryByRole("link", { name: "Blocked" })).not.toBeInTheDocument();
  });

  it("renders an overlay link to the work item covering the card", () => {
    render(
      <WorkItemCardGrid
        workItems={[baseItem]}
        projectId={projectId}
        pathname={pathname}
        searchParams={searchParams}
      />
    );

    const overlay = screen.getByRole("link", { name: baseItem.title });
    expect(overlay).toHaveAttribute(
      "href",
      `/projects/${projectId}/work-items/${baseItem.workItemId}`
    );
  });

  it("renders references in the title as soft links", () => {
    const item: WorkItemResponse = { ...baseItem, title: "Blocks WO-3 until review" };
    render(
      <WorkItemCardGrid
        workItems={[item]}
        projectId={projectId}
        pathname={pathname}
        searchParams={searchParams}
      />
    );

    const ref = screen.getByRole("link", { name: "WO-3" });
    expect(ref).toHaveAttribute("href", `/projects/${projectId}/ref/WO-3`);
  });
});
