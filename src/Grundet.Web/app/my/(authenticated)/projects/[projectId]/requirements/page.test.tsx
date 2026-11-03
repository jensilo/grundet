import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listProjectRequirements } from "@/lib/api/generated/sdk.gen";
import type { FeatureResponse } from "@/lib/api/generated/types.gen";
import { listAllFeatures } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";

// TODO: revisit when permissions-model lands — this asserts the *current* guard semantics.
import RequirementsPage from "./page";

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
  listAllFeatures: vi.fn(),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  listProjectRequirements: vi.fn(),
}));

const mockedFindProject = vi.mocked(findProject);
const mockedListAllFeatures = vi.mocked(listAllFeatures);
const mockedListProjectRequirements = vi.mocked(listProjectRequirements);
const mockedNotFound = vi.mocked(notFound);

const PROJECT = { id: "proj-1", teamId: "team-1", name: "Core", description: null, createdAt: "" };

const FEATURE: FeatureResponse = {
  id: "feat-1",
  projectId: "proj-1",
  name: "Checkout",
  description: null,
  createdAt: "",
};

function okPaged(): Awaited<ReturnType<typeof listProjectRequirements>> {
  return {
    data: {
      items: [],
      page: "1",
      pageSize: "24",
      total: "0",
      hasMore: false,
      totalPages: "0",
      lastSeenId: null,
    },
    error: undefined,
  } as Awaited<ReturnType<typeof listProjectRequirements>>;
}

function callPage(searchParams: Record<string, string | string[] | undefined> = {}) {
  return RequirementsPage({
    params: Promise.resolve({ projectId: "proj-1" }),
    searchParams: Promise.resolve(searchParams),
  });
}

describe("RequirementsPage – cross-tenant featureId guard (priority 2)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedListAllFeatures.mockResolvedValue({ features: [FEATURE], error: false });
    mockedListProjectRequirements.mockResolvedValue(okPaged());
  });

  it("notFound() when ?featureId belongs to another project (not in this project's features)", async () => {
    await expect(callPage({ featureId: "feat-from-other-project" })).rejects.toThrow(
      "NEXT_NOT_FOUND"
    );
    expect(mockedNotFound).toHaveBeenCalled();
    // The cross-tenant filter must be rejected before any requirement is fetched.
    expect(mockedListProjectRequirements).not.toHaveBeenCalled();
  });

  it("does not call notFound() when ?featureId is a feature of this project", async () => {
    await callPage({ featureId: "feat-1" });

    expect(mockedNotFound).not.toHaveBeenCalled();
    expect(mockedListProjectRequirements).toHaveBeenCalledWith(
      expect.objectContaining({ query: expect.objectContaining({ featureId: "feat-1" }) })
    );
  });
});

describe("RequirementsPage – pagination query wiring (priority 3)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedListAllFeatures.mockResolvedValue({ features: [FEATURE], error: false });
    mockedListProjectRequirements.mockResolvedValue(okPaged());
  });

  it("defaults to page 1 when no page param is present", async () => {
    await callPage();

    expect(mockedListProjectRequirements).toHaveBeenCalledWith(
      expect.objectContaining({ query: expect.objectContaining({ page: "1" }) })
    );
  });

  it("passes through an explicit page param", async () => {
    await callPage({ page: "3" });

    expect(mockedListProjectRequirements).toHaveBeenCalledWith(
      expect.objectContaining({ query: expect.objectContaining({ page: "3" }) })
    );
  });

  it("clamps a below-range page param up to 1", async () => {
    await callPage({ page: "0" });

    expect(mockedListProjectRequirements).toHaveBeenCalledWith(
      expect.objectContaining({ query: expect.objectContaining({ page: "1" }) })
    );
  });

  it("clamps an absurdly large page param down to the 100000 ceiling", async () => {
    await callPage({ page: "999999" });

    expect(mockedListProjectRequirements).toHaveBeenCalledWith(
      expect.objectContaining({ query: expect.objectContaining({ page: "100000" }) })
    );
  });
});
