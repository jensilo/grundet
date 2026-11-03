import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listFeatures } from "@/lib/api/generated/sdk.gen";

import { FeaturePicker } from "./feature-picker";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  listFeatures: vi.fn(),
}));

const mockedList = vi.mocked(listFeatures);

function makeListResponse(items: { id: string; name: string }[]): unknown {
  return {
    data: { items },
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

const FEATURES = [
  { id: "feat-1", name: "Authentication" },
  { id: "feat-2", name: "Billing" },
];

describe("FeaturePicker", () => {
  beforeEach(() => {
    mockedList.mockResolvedValue(
      makeListResponse(FEATURES) as Awaited<ReturnType<typeof listFeatures>>
    );
  });

  it("shows the current feature name on the trigger", () => {
    render(
      <FeaturePicker
        id="feature"
        projectId="proj-1"
        value={{ id: "feat-1", name: "Authentication" }}
        onChange={vi.fn()}
      />
    );

    expect(screen.getByRole("button", { name: /authentication/i })).toBeInTheDocument();
  });

  it("loads features on open and selects the chosen one", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(
      <FeaturePicker
        id="feature"
        projectId="proj-1"
        value={{ id: "feat-1", name: "Authentication" }}
        onChange={onChange}
      />
    );

    await user.click(screen.getByRole("button", { name: /authentication/i }));
    await user.click(await screen.findByRole("option", { name: "Billing" }));

    expect(onChange).toHaveBeenCalledWith({ id: "feat-2", name: "Billing" });
  });

  it("passes the typed term to the search query", async () => {
    const user = userEvent.setup();

    render(
      <FeaturePicker
        id="feature"
        projectId="proj-1"
        value={{ id: "feat-1", name: "Authentication" }}
        onChange={vi.fn()}
      />
    );

    await user.click(screen.getByRole("button", { name: /authentication/i }));
    await user.type(screen.getByPlaceholderText(/search features/i), "bill");

    // The search is debounced (250ms) — poll until the term-carrying call lands.
    await vi.waitFor(() => {
      expect(mockedList).toHaveBeenCalledWith(
        expect.objectContaining({ query: expect.objectContaining({ q: "bill" }) })
      );
    });
  });
});

describe("FeaturePicker — clearable (filter mode)", () => {
  beforeEach(() => {
    mockedList.mockResolvedValue(
      makeListResponse(FEATURES) as Awaited<ReturnType<typeof listFeatures>>
    );
  });

  it("shows the clear label as the trigger when nothing is selected", () => {
    render(
      <FeaturePicker
        id="feature"
        projectId="proj-1"
        value={null}
        onChange={vi.fn()}
        clearLabel="All features"
      />
    );

    expect(screen.getByRole("button", { name: /all features/i })).toBeInTheDocument();
  });

  it("emits null when the clear entry is chosen", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(
      <FeaturePicker
        id="feature"
        projectId="proj-1"
        value={{ id: "feat-1", name: "Authentication" }}
        onChange={onChange}
        clearLabel="All features"
      />
    );

    await user.click(screen.getByRole("button", { name: /authentication/i }));
    await user.click(await screen.findByRole("option", { name: "All features" }));

    expect(onChange).toHaveBeenCalledWith(null);
  });
});
