import { render, screen } from "@testing-library/react";
import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getProjectWorkItem, getWorkItemVersionHistory } from "@/lib/api/generated/sdk.gen";
import type { WorkItemResponse } from "@/lib/api/generated/types.gen";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import WorkItemHistoryPage from "./page";

// ---------------------------------------------------------------------------
// Mocks — history pages do NOT call findProject, so no project mock here.
// ---------------------------------------------------------------------------

vi.mock("@/lib/api/server", () => ({
  createServerClient: vi.fn(async () => ({})),
}));

vi.mock("next/navigation", () => ({
  notFound: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getProjectWorkItem: vi.fn(),
  getWorkItemVersionHistory: vi.fn(async () => ({ data: [], error: undefined })),
}));

const mockedGetProjectWorkItem = vi.mocked(getProjectWorkItem);
const mockedGetWorkItemVersionHistory = vi.mocked(getWorkItemVersionHistory);
const mockedNotFound = vi.mocked(notFound);

const WORK_ITEM: WorkItemResponse = {
  id: "wi-1",
  workItemId: "wi-1",
  projectId: "proj-1",
  title: "Wire up the export button.",
  description: null,
  status: "Todo",
  priority: "Medium",
  labels: [],
  assignedToUserId: null,
  blockedReason: null,
  reason: null,
  sequenceNumber: 1,
  ref: "W-1",
  type: "Task",
  changedAt: "2024-01-01T00:00:00Z",
  changedByUserId: "user-alice",
  changeType: "Created",
  reasonPrompted: false,
  assignedToDisplayName: null,
};

function callPage() {
  return WorkItemHistoryPage({
    params: Promise.resolve({ projectId: "proj-1", workItemId: "wi-1" }),
  });
}

describe("WorkItemHistoryPage – guard path", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("notFound() when the work-item lookup errors", async () => {
    mockedGetProjectWorkItem.mockResolvedValue({
      data: undefined,
      error: { status: 404 },
    } as Awaited<ReturnType<typeof getProjectWorkItem>>);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });

  it("does not call notFound() when the work item resolves", async () => {
    mockedGetProjectWorkItem.mockResolvedValue({
      data: { workItem: WORK_ITEM, links: [], assignedToDisplayName: null },
      error: undefined,
    } as Awaited<ReturnType<typeof getProjectWorkItem>>);

    await callPage();

    expect(mockedNotFound).not.toHaveBeenCalled();
  });

  it("renders the PageError when the version-history fetch errors", async () => {
    mockedGetProjectWorkItem.mockResolvedValue({
      data: { workItem: WORK_ITEM, links: [], assignedToDisplayName: null },
      error: undefined,
    } as Awaited<ReturnType<typeof getProjectWorkItem>>);
    mockedGetWorkItemVersionHistory.mockResolvedValue({
      data: undefined,
      error: { status: 500 },
    } as Awaited<ReturnType<typeof getWorkItemVersionHistory>>);

    render(await callPage());

    expect(mockedNotFound).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent("Could not load the version history.");
  });
});
