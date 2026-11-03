import { render } from "@testing-library/react";
import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { WorkItemComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-page";
import { getProjectWorkItem, listTeamMembers } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse, WorkItemResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import EditWorkItemPage from "./page";

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
  getWorkItemAttachments: vi.fn(async () => ({ data: [], error: undefined })),
  listTeamMembers: vi.fn(),
}));

// Stub the client composer host so we can assert the props the RSC page feeds it — without
// pulling in useRouter. The composer's own payload behaviour is covered by work-item-composer.test.tsx.
vi.mock(
  "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-page",
  () => ({
    WorkItemComposerPage: vi.fn(() => null),
  })
);

const mockedGetProjectWorkItem = vi.mocked(getProjectWorkItem);
const mockedListTeamMembers = vi.mocked(listTeamMembers);
const mockedFindProject = vi.mocked(findProject);
const mockedNotFound = vi.mocked(notFound);
const mockedComposerPage = vi.mocked(WorkItemComposerPage);

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

const TEAM_MEMBERS: TeamMemberResponse[] = [
  { userId: "u1", displayName: "Alice", email: "a@example.com", role: "Lead" },
];

const PROJECT = { id: "proj-1", teamId: "team-1", name: "Core", description: null, createdAt: "" };

function okWorkItem(): Awaited<ReturnType<typeof getProjectWorkItem>> {
  return {
    data: { workItem: WORK_ITEM, links: [], assignedToDisplayName: null },
    error: undefined,
  } as Awaited<ReturnType<typeof getProjectWorkItem>>;
}

function callPage() {
  return EditWorkItemPage({
    params: Promise.resolve({ projectId: "proj-1", workItemId: "wi-1" }),
  });
}

describe("EditWorkItemPage – guard paths", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedListTeamMembers.mockResolvedValue({ data: TEAM_MEMBERS, error: undefined } as Awaited<
      ReturnType<typeof listTeamMembers>
    >);
  });

  it("notFound() when the work-item lookup errors", async () => {
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

describe("EditWorkItemPage – composer prop-passing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedListTeamMembers.mockResolvedValue({ data: TEAM_MEMBERS, error: undefined } as Awaited<
      ReturnType<typeof listTeamMembers>
    >);
    mockedGetProjectWorkItem.mockResolvedValue(okWorkItem());
  });

  it("feeds the work item, project, and team members into the composer host in edit mode", async () => {
    render(await callPage());

    expect(mockedNotFound).not.toHaveBeenCalled();
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      mode: "edit",
      initial: WORK_ITEM,
      projectId: "proj-1",
      projectName: "Core",
      teamMembers: TEAM_MEMBERS,
    });
  });
});
