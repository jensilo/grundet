import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { CommandPaletteProvider, useCommandPalette } from "@/components/app/command-palette";
import { searchWorkspace } from "@/lib/api/generated/sdk.gen";
import type { WorkspaceSearchResponse } from "@/lib/api/generated/types.gen";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  usePathname: () => "/dashboard",
  useRouter: () => ({ push }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  searchWorkspace: vi.fn(),
}));

const response: WorkspaceSearchResponse = {
  requirements: [
    {
      type: "requirement",
      id: "req-1",
      title: "System shall export telemetry data",
      status: "Active",
      subTitle: "Reporting",
      projectId: "proj-1",
      projectName: "Core",
      teamName: "Platform",
    },
  ],
  workItems: [
    {
      type: "workitem",
      id: "wi-1",
      title: "Build telemetry ingestion pipeline",
      status: "Todo",
      subTitle: "#4",
      projectId: "proj-2",
      projectName: "Pipelines",
      teamName: "Platform",
    },
  ],
  features: [],
};

function Trigger(): React.JSX.Element {
  const { open } = useCommandPalette();
  return (
    <button type="button" onClick={open}>
      open palette
    </button>
  );
}

function renderPalette() {
  return render(
    <CommandPaletteProvider projects={[]}>
      <Trigger />
    </CommandPaletteProvider>
  );
}

describe("CommandPalette", () => {
  beforeEach(() => {
    push.mockClear();
    vi.mocked(searchWorkspace).mockResolvedValue({
      data: response,
    } as Awaited<ReturnType<typeof searchWorkspace>>);
  });

  it("opens with Cmd+K and searches across the workspace", async () => {
    const user = userEvent.setup();
    renderPalette();

    expect(screen.queryByRole("textbox", { name: /search intent/i })).not.toBeInTheDocument();

    await user.keyboard("{Meta>}k{/Meta}");

    const input = await screen.findByRole("textbox", { name: /search intent/i });
    await user.type(input, "telemetry");

    expect(await screen.findByText("System shall export telemetry data")).toBeInTheDocument();
    expect(screen.getByText("Build telemetry ingestion pipeline")).toBeInTheDocument();
    // Grouped by domain term.
    expect(screen.getByText("Intent")).toBeInTheDocument();
    expect(screen.getByText("Work")).toBeInTheDocument();
  });

  it("navigates to the result detail page on selection", async () => {
    const user = userEvent.setup();
    renderPalette();

    await user.click(screen.getByRole("button", { name: /open palette/i }));
    const input = await screen.findByRole("textbox", { name: /search intent/i });
    await user.type(input, "telemetry");

    await user.click(await screen.findByText("Build telemetry ingestion pipeline"));

    expect(push).toHaveBeenCalledWith("/projects/proj-2/work-items/wi-1");
  });
});
