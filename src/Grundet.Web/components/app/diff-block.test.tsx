import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { DiffBlock } from "./diff-block";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  resolveProjectReference: vi.fn().mockResolvedValue({ data: undefined, error: undefined }),
}));

describe("DiffBlock", () => {
  it("renders references in surviving text as soft links when projectId is set", () => {
    render(<DiffBlock projectId="p1" before="Linked to IN-5" after="Linked to IN-5 now" />);

    const link = screen.getByRole("link", { name: "IN-5" });
    expect(link).toHaveAttribute("href", "/projects/p1/ref/IN-5");
  });

  it("renders references in added text as soft links", () => {
    render(<DiffBlock projectId="p1" before="Plain text" after="Plain text blocks WO-9" />);

    const link = screen.getByRole("link", { name: "WO-9" });
    expect(link).toHaveAttribute("href", "/projects/p1/ref/WO-9");
  });

  it("renders plain text with no links when projectId is omitted", () => {
    render(<DiffBlock before="See IN-5" after="See IN-5 today" />);

    expect(screen.getByText(/IN-5/)).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
