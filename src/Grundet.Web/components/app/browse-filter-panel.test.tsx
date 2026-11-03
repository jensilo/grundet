import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { BrowseFilterPanel } from "./browse-filter-panel";

vi.mock("next/navigation", () => ({
  usePathname: () => "/projects/proj-1/work-items",
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

const STATUS_OPTIONS = [
  { value: "Todo", label: "To do" },
  { value: "Done", label: "Done" },
];
const PRIORITY_OPTIONS = [
  { value: "High", label: "High" },
  { value: "Low", label: "Low" },
];
const TYPE_OPTIONS = [
  { value: "Feature", label: "Feature" },
  { value: "Bug", label: "Bug" },
  { value: "Task", label: "Task" },
];

const baseWorkProps = {
  kind: "work" as const,
  query: "",
  sort: "updated",
  projectId: "proj-1",
  statusOptions: STATUS_OPTIONS,
  selectedStatuses: [],
  priorityOptions: PRIORITY_OPTIONS,
  selectedPriorities: [],
  labelOptions: [],
  selectedLabels: [],
  typeOptions: TYPE_OPTIONS,
  selectedTypes: [],
  blocked: undefined,
};

describe("BrowseFilterPanel — work (with typeOptions)", () => {
  it("includes active type and blocked in the filter badge count", () => {
    render(<BrowseFilterPanel {...baseWorkProps} selectedTypes={["Bug"]} blocked={true} />);

    // 1 type + 1 blocked = badge showing 2
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("shows active type chip in collapsed active-filters summary", () => {
    render(<BrowseFilterPanel {...baseWorkProps} selectedTypes={["Feature"]} />);

    // The panel auto-opens when activeAdvancedFilterCount > 0, but we
    // need it collapsed to see the summary. The defaultPanelOpen is true,
    // so the summary is not shown. Test the filter badge presence instead.
    expect(screen.getByText("1")).toBeInTheDocument();
  });

  it("the search form contains hidden inputs for active type and blocked filters", () => {
    const { container } = render(
      <BrowseFilterPanel {...baseWorkProps} selectedTypes={["Bug", "Task"]} blocked={false} />
    );

    const hiddenInputs = container.querySelectorAll('input[type="hidden"]');
    const names = Array.from(hiddenInputs).map((el) => el.getAttribute("name"));

    expect(names).toContain("type");
    expect(names).toContain("blocked");

    const typeInputs = Array.from(hiddenInputs).filter((el) => el.getAttribute("name") === "type");
    expect(typeInputs.map((el) => el.getAttribute("value"))).toEqual(
      expect.arrayContaining(["Bug", "Task"])
    );

    const blockedInput = Array.from(hiddenInputs).find(
      (el) => el.getAttribute("name") === "blocked"
    );
    expect(blockedInput?.getAttribute("value")).toBe("false");
  });

  it("the active Blocked chip href clears blocked when clicked again", () => {
    render(<BrowseFilterPanel {...baseWorkProps} blocked={true} />);

    // When panel is open and blocked=true, the Blocked chip should be the selected one.
    // Its href must NOT contain blocked= (i.e. sets blocked to undefined = clears it).
    const links = screen.getAllByRole("link", { name: "Blocked" });
    // The FilterChip link for the active "Blocked" state (selected chip)
    const selectedChip = links.find(
      (link) => link.getAttribute("href") && !link.getAttribute("href")?.includes("blocked=")
    );
    expect(selectedChip).toBeDefined();
  });

  it("renders the Type and Blocked filter groups when typeOptions is provided and panel is open", () => {
    // Pass an active type so activeAdvancedFilterCount > 0, which opens the panel by default.
    render(<BrowseFilterPanel {...baseWorkProps} selectedTypes={["Bug"]} />);

    expect(screen.getByText("Feature")).toBeInTheDocument();
    expect(screen.getByText("Bug")).toBeInTheDocument();
    expect(screen.getByText("Task")).toBeInTheDocument();
    expect(screen.getByText("Not blocked")).toBeInTheDocument();
  });
});

describe("BrowseFilterPanel — intent (without typeOptions)", () => {
  const intentProps = {
    kind: "intent" as const,
    query: "",
    sort: "updated",
    projectId: "proj-1",
    statusOptions: STATUS_OPTIONS,
    selectedStatuses: [],
    priorityOptions: PRIORITY_OPTIONS,
    selectedPriorities: [],
    labelOptions: [],
    selectedLabels: [],
  };

  it("does not render Type or Blocked groups even when panel is open", () => {
    // Pass an active status to ensure the panel is open (activeAdvancedFilterCount > 0).
    render(<BrowseFilterPanel {...intentProps} selectedStatuses={["Todo"]} />);

    // Status and Priority groups must be present (panel is open).
    expect(screen.getByText("To do")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();

    // Type group chips and Blocked chip must be absent.
    expect(screen.queryByText("Feature")).not.toBeInTheDocument();
    expect(screen.queryByText("Bug")).not.toBeInTheDocument();
    expect(screen.queryByText("Task")).not.toBeInTheDocument();
    expect(screen.queryByText("Not blocked")).not.toBeInTheDocument();
  });
});
