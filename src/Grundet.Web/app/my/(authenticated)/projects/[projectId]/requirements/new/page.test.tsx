import { render, screen } from "@testing-library/react";
import { notFound } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RequirementComposerPage } from "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-page";
import { getProjectRequirement } from "@/lib/api/generated/sdk.gen";
import { getFeatureById, getFeatureCountAndFirst } from "@/lib/api/queries/features";
import { findProject } from "@/lib/api/queries/projects";

import NewRequirementPage from "./page";

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
  getFeatureById: vi.fn(async () => null),
  getFeatureCountAndFirst: vi.fn(),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getProjectRequirement: vi.fn(),
}));

vi.mock(
  "@/app/my/(authenticated)/projects/[projectId]/requirements/_components/requirement-composer-page",
  () => ({
    RequirementComposerPage: vi.fn(() => null),
  })
);

const mockedFindProject = vi.mocked(findProject);
const mockedFeatureCountAndFirst = vi.mocked(getFeatureCountAndFirst);
const mockedGetFeatureById = vi.mocked(getFeatureById);
const mockedGetProjectRequirement = vi.mocked(getProjectRequirement);
const mockedNotFound = vi.mocked(notFound);
const mockedComposerPage = vi.mocked(RequirementComposerPage);

const PROJECT = { id: "proj-1", teamId: "team-1", name: "Core", description: null, createdAt: "" };

const FEATURE_TWO = { id: "feat-2", name: "Search" };

function callPage(searchParams: Record<string, string | string[] | undefined> = {}) {
  return NewRequirementPage({
    params: Promise.resolve({ projectId: "proj-1" }),
    searchParams: Promise.resolve(searchParams),
  });
}

describe("NewRequirementPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedFindProject.mockResolvedValue(PROJECT);
    mockedFeatureCountAndFirst.mockResolvedValue({
      count: 2,
      first: { id: "feat-1", name: "Checkout" },
      error: false,
    });
  });

  it("notFound() when the project resolves to null (cross-project access)", async () => {
    mockedFindProject.mockResolvedValue(null);

    await expect(callPage()).rejects.toThrow("NEXT_NOT_FOUND");
    expect(mockedNotFound).toHaveBeenCalled();
  });

  it("renders the no-features empty state instead of the composer when the project has no features", async () => {
    mockedFeatureCountAndFirst.mockResolvedValue({ count: 0, first: null, error: false });

    render(await callPage());

    expect(screen.getByText(/no features yet/i)).toBeInTheDocument();
    expect(mockedComposerPage).not.toHaveBeenCalled();
  });

  it("feeds the default (first) feature and project into the composer host in create mode", async () => {
    render(await callPage());

    expect(mockedNotFound).not.toHaveBeenCalled();
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      mode: "create",
      featureId: "feat-1",
      currentFeature: { id: "feat-1", name: "Checkout" },
      featureCount: 2,
      projectId: "proj-1",
      projectName: "Core",
    });
  });

  it("feeds the explicitly requested ?featureId= feature into the composer host", async () => {
    mockedGetFeatureById.mockResolvedValue(FEATURE_TWO);

    render(await callPage({ featureId: "feat-2" }));

    expect(mockedGetFeatureById).toHaveBeenCalledWith(expect.anything(), "proj-1", "feat-2");
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      featureId: "feat-2",
      currentFeature: FEATURE_TWO,
    });
  });

  it("adopts the parent feature when ?fromRequirement= resolves to a known requirement", async () => {
    mockedGetProjectRequirement.mockResolvedValue({
      data: { featureId: "feat-2" },
      error: undefined,
    } as Awaited<ReturnType<typeof getProjectRequirement>>);
    mockedGetFeatureById.mockResolvedValue(FEATURE_TWO);

    render(await callPage({ fromRequirement: "req-99" }));

    expect(mockedGetProjectRequirement).toHaveBeenCalledWith(
      expect.objectContaining({ path: { projectId: "proj-1", requirementId: "req-99" } })
    );
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      featureId: "feat-2",
      currentFeature: FEATURE_TWO,
    });
  });

  it("falls back to the first feature when ?fromRequirement= does not resolve", async () => {
    mockedGetProjectRequirement.mockResolvedValue({
      data: undefined,
      error: { status: 404 },
    } as Awaited<ReturnType<typeof getProjectRequirement>>);

    render(await callPage({ fromRequirement: "req-missing" }));

    expect(mockedNotFound).not.toHaveBeenCalled();
    expect(mockedGetFeatureById).not.toHaveBeenCalled();
    const props = mockedComposerPage.mock.calls[0][0];
    expect(props).toMatchObject({
      featureId: "feat-1",
      currentFeature: { id: "feat-1", name: "Checkout" },
    });
  });
});
