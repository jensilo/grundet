import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getProjectWorkItem } from "@/lib/api/generated/sdk.gen";
import type { WorkItemResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import WorkItemDetailPage from "./page";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock("@/lib/api/server", () => ({
  createServerClient: vi.fn(async () => ({})),
}));

vi.mock("next/navigation", () => ({
  notFound: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

vi.mock("@/lib/api/queries/projects", () => ({
  findProject: vi.fn(),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getProjectWorkItem: vi.fn(),
  getWorkItemVersionHistory: vi.fn(async () => ({ data: [], error: undefined })),
  getWorkItemAttachments: vi.fn(async () => ({ data: [], error: undefined })),
  getMe: vi.fn(async () => ({ data: { id: "u1" }, error: undefined })),
  listTeamMembers: vi.fn(async () => ({ data: [], error: undefined })),
}));

const mockedGetProjectWorkItem = vi.mocked(getProjectWorkItem);
const mockedFindProject = vi.mocked(findProject);
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

const PROJECT = { id: "proj-1", teamId: "team-1", name: "Core", description: null, createdAt: "" };

function okWorkItem(): Awaited<ReturnType<typeof getProjectWorkItem>> {
  return {
    data: { workItem: WORK_ITEM, links: [], assignedToDisplayName: null },
    error: undefined,
  } as Awaited<ReturnType<typeof getProjectWorkItem>>;
}

function callPage() {
  return WorkItemDetailPage({
    params: Promise.resolve({ projectId: "proj-1", workItemId: "wi-1" }),
  });
}

describe("WorkItemDetailPage – guard paths", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
  });

  it("notFound() when the work-item lookup errors (missing/unauthorized id)", async () => {
    mockedGetProjectWorkItem.mockResolvedValue({
      data: undefined,
      error: { status: 404 },
    } as Awaited<ReturnType<typeof getProjectWorkItem>>);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });

  it("notFound() when the project resolves to null (cross-project access)", async () => {
    mockedGetProjectWorkItem.mockResolvedValue(okWorkItem());
    mockedFindProject.mockResolvedValue(null);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });
});
