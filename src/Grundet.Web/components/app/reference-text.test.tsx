import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { ReferenceText } from "./reference-text";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  resolveProjectReference: vi.fn().mockResolvedValue({ data: undefined, error: undefined }),
}));

describe("ReferenceText", () => {
  it("renders plain text without links when there are no references", () => {
    render(<ReferenceText projectId="p1" text="nothing to link here" />);
    expect(screen.getByText("nothing to link here")).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("renders each reference as a link pointing at the project resolver", () => {
    render(<ReferenceText projectId="p1" text="See IN-12 and WO-3 and #5." />);

    const reqLink = screen.getByRole("link", { name: "IN-12" });
    const wiLink = screen.getByRole("link", { name: "WO-3" });
    const bareLink = screen.getByRole("link", { name: "#5" });

    expect(reqLink).toHaveAttribute("href", "/projects/p1/ref/IN-12");
    expect(wiLink).toHaveAttribute("href", "/projects/p1/ref/WO-3");
    expect(bareLink).toHaveAttribute("href", "/projects/p1/ref/5");
  });

  it("keeps the surrounding text intact", () => {
    render(<ReferenceText projectId="p1" text="blocks WO-4 until review" />);
    expect(screen.getByText(/blocks/)).toBeInTheDocument();
    expect(screen.getByText(/until review/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "WO-4" })).toBeInTheDocument();
  });
});
