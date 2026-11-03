import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listProjectRequirements } from "@/lib/api/generated/sdk.gen";

import { IntentLinkPicker, type SelectedIntent } from "./intent-link-picker";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  listProjectRequirements: vi.fn(),
}));

const mockedList = vi.mocked(listProjectRequirements);

function makeListResponse(items: { requirementId: string; ref: string; text: string }[]): unknown {
  return {
    data: { items },
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

const RESULTS = [
  { requirementId: "r1", ref: "IN-1", text: "Users can log in" },
  { requirementId: "r2", ref: "IN-2", text: "Users can log out" },
];

describe("IntentLinkPicker", () => {
  beforeEach(() => {
    mockedList.mockResolvedValue(
      makeListResponse(RESULTS) as Awaited<ReturnType<typeof listProjectRequirements>>
    );
  });

  it("loads results on focus and adds the chosen intent", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<IntentLinkPicker id="picker" projectId="proj-1" values={[]} onChange={onChange} />);

    await user.click(screen.getByRole("combobox"));
    const option = await screen.findByRole("option", { name: /IN-1/ });
    await user.click(option);

    expect(onChange).toHaveBeenCalledWith([
      { requirementId: "r1", ref: "IN-1", text: "Users can log in" },
    ]);
  });

  it("hides already-linked intent passed via excludeRequirementIds", async () => {
    const user = userEvent.setup();

    render(
      <IntentLinkPicker
        id="picker"
        projectId="proj-1"
        values={[]}
        onChange={vi.fn()}
        excludeRequirementIds={["r1"]}
      />
    );

    await user.click(screen.getByRole("combobox"));
    expect(await screen.findByRole("option", { name: /IN-2/ })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /IN-1/ })).not.toBeInTheDocument();
  });

  it("renders selected intent as chips and removes on click", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const values: SelectedIntent[] = [
      { requirementId: "r1", ref: "IN-1", text: "Users can log in" },
    ];

    render(<IntentLinkPicker id="picker" projectId="proj-1" values={values} onChange={onChange} />);

    expect(screen.getByText("IN-1")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /remove IN-1/i }));
    expect(onChange).toHaveBeenCalledWith([]);
  });
});
