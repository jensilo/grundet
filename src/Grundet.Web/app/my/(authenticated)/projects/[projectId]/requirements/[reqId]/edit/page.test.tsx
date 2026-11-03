import { render } from "@testing-library/react";
import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RequirementComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-page";
import { getProjectRequirement } from "@/lib/api/generated/sdk.gen";
import type { RequirementResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import EditRequirementPage from "./page";

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

vi.mock("@/lib/api/queries/features", () => ({
  getFeatureById: vi.fn(async () => ({ id: "feat-1", name: "Checkout" })),
  getFeatureCountAndFirst: vi.fn(async () => ({
    count: 1,
    first: { id: "feat-1", name: "Checkout" },
    error: false,
  })),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getProjectRequirement: vi.fn(),
  getRequirementAttachments: vi.fn(async () => ({ data: [], error: undefined })),
}));

// Stub the client composer host so we can assert the props the RSC page feeds it — without
// pulling in useRouter. The composer's own payload behaviour is covered by requirement-composer.test.tsx.
vi.mock(
  "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-page",
  () => ({
    RequirementComposerPage: vi.fn(() => null),
  })
);

const mockedGetProjectRequirement = vi.mocked(getProjectRequirement);
const mockedFindProject = vi.mocked(findProject);
const mockedNotFound = vi.mocked(notFound);
const mockedComposerPage = vi.mocked(RequirementComposerPage);

const REQUIREMENT: RequirementResponse = {
  id: "req-1",
  requirementId: "req-1",
  featureId: "feat-1",
  sequenceNumber: 1,
  ref: "R-1",
  text: "The system shall validate user input.",
  status: "Draft",
  priority: "Should",
  labels: [],
  acceptanceCriteria: [],
  rationale: null,
  reason: null,
  changedAt: "2024-01-01T00:00:00Z",
  changedByUserId: "user-alice",
  changeType: "Created",
};

const PROJECT = { id: "proj-1", teamId: "team-1", name: "Core", description: null, createdAt: "" };

function okRequirement(): Awaited<ReturnType<typeof getProjectRequirement>> {
  return { data: REQUIREMENT, error: undefined } as Awaited<
    ReturnType<typeof getProjectRequirement>
  >;
}

function callPage() {
  return EditRequirementPage({
    params: Promise.resolve({ projectId: "proj-1", reqId: "req-1" }),
  });
}

describe("EditRequirementPage – guard paths", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
  });

  it("notFound() when the requirement lookup errors", async () => {
    mockedGetProjectRequirement.mockResolvedValue({
      data: undefined,
      error: { status: 404 },
    } as Awaited<ReturnType<typeof getProjectRequirement>>);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });

  it("notFound() when the project resolves to null (cross-project access)", async () => {
    mockedGetProjectRequirement.mockResolvedValue(okRequirement());
    mockedFindProject.mockResolvedValue(null);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });
});

describe("EditRequirementPage – composer prop-passing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedGetProjectRequirement.mockResolvedValue(okRequirement());
  });

  it("feeds the requirement, project, and feature into the composer host in edit mode", async () => {
    render(await callPage());

    expect(mockedNotFound).not.toHaveBeenCalled();
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      mode: "edit",
      initial: REQUIREMENT,
      projectId: "proj-1",
      projectName: "Core",
      currentFeature: { id: "feat-1", name: "Checkout" },
      featureCount: 1,
    });
  });
});
