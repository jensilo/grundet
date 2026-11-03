import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getProjectRequirement } from "@/lib/api/generated/sdk.gen";
import type { RequirementResponse } from "@/lib/api/generated/types.gen";
import { findProject } from "@/lib/api/queries/projects";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import RequirementDetailPage from "./page";

// ---------------------------------------------------------------------------
// Mocks — vi.mock is hoisted, so each page test copies this block.
// ---------------------------------------------------------------------------

vi.mock("@/lib/api/server", () => ({
  createServerClient: vi.fn(async () => ({})),
}));

vi.mock("next/navigation", () => ({
  // The real notFound() throws to perform the control-flow jump — mirror that so we can assert it.
  notFound: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

vi.mock("@/lib/api/queries/projects", () => ({
  findProject: vi.fn(),
}));

vi.mock("@/lib/api/queries/features", () => ({
  getFeatureById: vi.fn(async () => null),
  getFeatureCountAndFirst: vi.fn(async () => ({ count: 0, first: null, error: false })),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getProjectRequirement: vi.fn(),
  getRequirementVersionHistory: vi.fn(async () => ({ data: [], error: undefined })),
  getRequirementWorkItems: vi.fn(async () => ({ data: [], error: undefined })),
  getRequirementAttachments: vi.fn(async () => ({ data: [], error: undefined })),
}));

const mockedGetProjectRequirement = vi.mocked(getProjectRequirement);
const mockedFindProject = vi.mocked(findProject);
const mockedNotFound = vi.mocked(notFound);

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

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

function errorRequirement(): Awaited<ReturnType<typeof getProjectRequirement>> {
  return { data: undefined, error: { status: 404 } } as Awaited<
    ReturnType<typeof getProjectRequirement>
  >;
}

function callPage() {
  return RequirementDetailPage({
    params: Promise.resolve({ projectId: "proj-1", reqId: "req-1" }),
    searchParams: Promise.resolve({}),
  });
}

// ---------------------------------------------------------------------------
// Guard-path tests (priority 2)
// ---------------------------------------------------------------------------

describe("RequirementDetailPage – guard paths", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
  });

  it("notFound() when the requirement lookup errors (missing/unauthorized id)", async () => {
    mockedGetProjectRequirement.mockResolvedValue(errorRequirement());

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
