import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listWorkItems } from "@/lib/api/generated/sdk.gen";

import { type SelectedWork, WorkLinkPicker } from "./work-link-picker";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  listWorkItems: vi.fn(),
}));

const mockedList = vi.mocked(listWorkItems);

function makeListResponse(items: { workItemId: string; ref: string; title: string }[]): unknown {
  return {
    data: { items },
    error: undefined,
    request: new Request("https://grundet.test"),
    response: new Response(),
  };
}

const RESULTS = [
  { workItemId: "w1", ref: "WO-1", title: "Build the login form" },
  { workItemId: "w2", ref: "WO-2", title: "Wire up logout" },
];

describe("WorkLinkPicker", () => {
  beforeEach(() => {
    mockedList.mockResolvedValue(
      makeListResponse(RESULTS) as Awaited<ReturnType<typeof listWorkItems>>
    );
  });

  it("loads work on focus and adds the chosen item", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(<WorkLinkPicker id="picker" projectId="proj-1" values={[]} onChange={onChange} />);

    await user.click(screen.getByRole("combobox"));
    await user.click(await screen.findByRole("option", { name: /WO-1/ }));

    expect(onChange).toHaveBeenCalledWith([
      { workItemId: "w1", ref: "WO-1", title: "Build the login form" },
    ]);
  });

  it("hides already-linked work passed via excludeWorkItemIds", async () => {
    const user = userEvent.setup();

    render(
      <WorkLinkPicker
        id="picker"
        projectId="proj-1"
        values={[]}
        onChange={vi.fn()}
        excludeWorkItemIds={["w1"]}
      />
    );

    await user.click(screen.getByRole("combobox"));
    expect(await screen.findByRole("option", { name: /WO-2/ })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /WO-1/ })).not.toBeInTheDocument();
  });

  it("renders selected work as chips and removes on click", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const values: SelectedWork[] = [
      { workItemId: "w1", ref: "WO-1", title: "Build the login form" },
    ];

    render(<WorkLinkPicker id="picker" projectId="proj-1" values={values} onChange={onChange} />);

    expect(screen.getByText("WO-1")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /remove WO-1/i }));
    expect(onChange).toHaveBeenCalledWith([]);
  });
});
