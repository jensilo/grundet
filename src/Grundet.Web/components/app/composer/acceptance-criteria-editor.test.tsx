import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";

import { AcceptanceCriteriaEditor } from "./acceptance-criteria-editor";

function Harness({ initial = [] as string[] }): React.JSX.Element {
  const [values, setValues] = useState<string[]>(initial);
  return (
    <>
      <AcceptanceCriteriaEditor id="criteria" values={values} onChange={setValues} />
      <output data-testid="serialized">{JSON.stringify(values)}</output>
    </>
  );
}

const serialized = (): string => screen.getByTestId("serialized").textContent ?? "";

describe("AcceptanceCriteriaEditor", () => {
  it("adds a criterion on Enter and clears the draft input", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const input = screen.getByPlaceholderText("Add a criterion and press Enter");
    await user.type(input, "Given a user{Enter}");

    expect(serialized()).toBe(JSON.stringify(["Given a user"]));
    expect(input).toHaveValue("");
  });

  it("trims and ignores empty criteria", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const input = screen.getByPlaceholderText("Add a criterion and press Enter");
    await user.type(input, "   {Enter}");
    expect(serialized()).toBe(JSON.stringify([]));

    await user.type(input, "  spaced  {Enter}");
    expect(serialized()).toBe(JSON.stringify(["spaced"]));
  });

  it("removes a criterion", async () => {
    const user = userEvent.setup();
    render(<Harness initial={["first", "second"]} />);

    await user.click(screen.getByRole("button", { name: "Remove criterion 1" }));

    expect(serialized()).toBe(JSON.stringify(["second"]));
  });

  it("reorders criteria with up/down and round-trips as string[]", async () => {
    const user = userEvent.setup();
    render(<Harness initial={["a", "b", "c"]} />);

    await user.click(screen.getByRole("button", { name: "Move criterion 3 up" }));
    expect(serialized()).toBe(JSON.stringify(["a", "c", "b"]));

    await user.click(screen.getByRole("button", { name: "Move criterion 1 down" }));
    expect(serialized()).toBe(JSON.stringify(["c", "a", "b"]));
  });

  it("edits a criterion in place", async () => {
    const user = userEvent.setup();
    render(<Harness initial={["typo"]} />);

    const row = screen.getByRole("textbox", { name: "Acceptance criterion 1" });
    await user.clear(row);
    await user.type(row, "fixed");

    expect(serialized()).toBe(JSON.stringify(["fixed"]));
  });
});
