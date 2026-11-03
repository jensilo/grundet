import { render } from "@testing-library/react";
import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { WorkItemComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-page";
import { listTeamMembers } from "@/lib/api/generated/sdk.gen";
import type { TeamMemberResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";

import NewWorkItemPage from "./page";

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
  listTeamMembers: vi.fn(),
}));

vi.mock(
  "@/app/my/(authenticated)/projects/[projectId]/work-items/_components/work-item-composer-page",
  () => ({
    WorkItemComposerPage: vi.fn(() => null),
  })
);

const mockedListTeamMembers = vi.mocked(listTeamMembers);
const mockedFindProject = vi.mocked(findProject);
const mockedNotFound = vi.mocked(notFound);
const mockedComposerPage = vi.mocked(WorkItemComposerPage);

const TEAM_MEMBERS: TeamMemberResponse[] = [
  { userId: "u1", displayName: "Alice", email: "a@example.com", role: "Lead" },
];

const PROJECT = { id: "proj-1", teamId: "team-1", name: "Core", description: null, createdAt: "" };

function callPage() {
  return NewWorkItemPage({ params: Promise.resolve({ projectId: "proj-1" }) });
}

describe("NewWorkItemPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedListTeamMembers.mockResolvedValue({ data: TEAM_MEMBERS, error: undefined } as Awaited<
      ReturnType<typeof listTeamMembers>
    >);
  });

  it("notFound() when the project resolves to null (cross-project access)", async () => {
    mockedFindProject.mockResolvedValue(null);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });

  it("feeds the project and team members into the composer host in create mode", async () => {
    render(await callPage());

    expect(mockedNotFound).not.toHaveBeenCalled();
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      mode: "create",
      projectId: "proj-1",
      projectName: "Core",
      teamMembers: TEAM_MEMBERS,
    });
  });
});
