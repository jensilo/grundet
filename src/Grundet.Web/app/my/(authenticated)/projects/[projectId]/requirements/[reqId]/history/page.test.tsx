import { render, screen } from "@testing-library/react";
import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getProjectRequirement, getRequirementVersionHistory } from "@/lib/api/generated/sdk.gen";
import type { RequirementResponse } from "@/lib/api/generated/types.gen";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import RequirementHistoryPage from "./page";

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
  getProjectRequirement: vi.fn(),
  getRequirementVersionHistory: vi.fn(async () => ({ data: [], error: undefined })),
}));

const mockedGetProjectRequirement = vi.mocked(getProjectRequirement);
const mockedGetRequirementVersionHistory = vi.mocked(getRequirementVersionHistory);
const mockedNotFound = vi.mocked(notFound);

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

function callPage() {
  return RequirementHistoryPage({
    params: Promise.resolve({ projectId: "proj-1", reqId: "req-1" }),
  });
}

describe("RequirementHistoryPage – guard path", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("notFound() when the requirement lookup errors", async () => {
    mockedGetProjectRequirement.mockResolvedValue({
      data: undefined,
      error: { status: 404 },
    } as Awaited<ReturnType<typeof getProjectRequirement>>);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });

  it("does not call notFound() when the requirement resolves", async () => {
    mockedGetProjectRequirement.mockResolvedValue({
      data: REQUIREMENT,
      error: undefined,
    } as Awaited<ReturnType<typeof getProjectRequirement>>);

    await callPage();

    expect(mockedNotFound).not.toHaveBeenCalled();
  });

  it("renders the PageError when the version-history fetch errors", async () => {
    mockedGetProjectRequirement.mockResolvedValue({
      data: REQUIREMENT,
      error: undefined,
    } as Awaited<ReturnType<typeof getProjectRequirement>>);
    mockedGetRequirementVersionHistory.mockResolvedValue({
      data: undefined,
      error: { status: 500 },
    } as Awaited<ReturnType<typeof getRequirementVersionHistory>>);

    render(await callPage());

    expect(mockedNotFound).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent("Could not load the version history.");
  });
});
