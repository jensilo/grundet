import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getProjectLabels } from "@/lib/api/generated/sdk.gen";

import { LabelChipInput } from "./label-chip-input";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  getProjectLabels: vi.fn(),
}));

const mockedGetProjectLabels = vi.mocked(getProjectLabels);

function Harness({ initial = [] as string[] }): React.JSX.Element {
  const [values, setValues] = useState<string[]>(initial);
  return (
    <>
      <LabelChipInput id="labels" projectId="p1" values={values} onChange={setValues} />
      <output data-testid="serialized">{JSON.stringify(values)}</output>
    </>
  );
}

const serialized = (): string => screen.getByTestId("serialized").textContent ?? "";

describe("LabelChipInput", () => {
  beforeEach(() => {
    // count arrives typed as number | string (OpenAPI int quirk).
    mockedGetProjectLabels.mockResolvedValue({
      data: {
        labels: [
          { value: "backend", count: "5" },
          { value: "auth", count: 3 },
          { value: "frontend", count: 1 },
        ],
      },
      error: undefined,
      request: new Request("https://grundet.test/labels"),
      response: new Response(),
    } as Awaited<ReturnType<typeof getProjectLabels>>);
  });

  it("adds a free-entry label on Enter", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const input = screen.getByRole("combobox");
    await user.type(input, "custom{Enter}");

    expect(serialized()).toBe(JSON.stringify(["custom"]));
    expect(input).toHaveValue("");
  });

  it("does not add duplicate labels (case-insensitive)", async () => {
    const user = userEvent.setup();
    render(<Harness initial={["Auth"]} />);

    await user.type(screen.getByRole("combobox"), "auth{Enter}");

    expect(serialized()).toBe(JSON.stringify(["Auth"]));
  });

  it("filters suggestions as the user types and selects via keyboard", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const input = screen.getByRole("combobox");
    await user.click(input);

    // Suggestions load on focus; all three appear.
    expect(await screen.findByRole("option", { name: /backend/ })).toBeInTheDocument();

    await user.type(input, "fr");
    expect(screen.getByRole("option", { name: /frontend/ })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /backend/ })).not.toBeInTheDocument();

    // ArrowDown highlights the first option, Enter selects it instead of the raw text.
    await user.keyboard("{ArrowDown}{Enter}");
    expect(serialized()).toBe(JSON.stringify(["frontend"]));
  });

  it("selects a suggestion on click", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole("combobox"));
    await user.click(await screen.findByRole("option", { name: /auth/ }));

    expect(serialized()).toBe(JSON.stringify(["auth"]));
  });

  it("removes the last chip on Backspace when the input is empty", async () => {
    const user = userEvent.setup();
    render(<Harness initial={["one", "two"]} />);

    await user.click(screen.getByRole("combobox"));
    await user.keyboard("{Backspace}");

    expect(serialized()).toBe(JSON.stringify(["one"]));
  });

  it("removes a chip via its remove button", async () => {
    const user = userEvent.setup();
    render(<Harness initial={["keep", "drop"]} />);

    await user.click(screen.getByRole("button", { name: "Remove drop" }));

    expect(serialized()).toBe(JSON.stringify(["keep"]));
  });
});
