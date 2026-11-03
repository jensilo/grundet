import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { AVATAR_PALETTE } from "@/lib/avatar-palette";

import { UserAvatar } from "./user-avatar";

// Mock the library so we test our contract (seed + variant + palette passthrough, initials
// fallback) rather than boring-avatars' byte-for-byte SVG output.
vi.mock("boring-avatars", () => ({
  default: ({ name, colors, variant }: { name: string; colors: string[]; variant: string }) => (
    <svg
      data-testid="identicon"
      data-seed={name}
      data-variant={variant}
      data-colors={colors.join(",")}
    />
  ),
}));

describe("UserAvatar", () => {
  it("seeds the identicon with the userId and the avatar palette", () => {
    render(<UserAvatar userId="user-123" name="Jane Doe" email="jane@example.com" />);

    const icon = screen.getByTestId("identicon");
    expect(icon).toHaveAttribute("data-seed", "user-123");
    expect(icon).toHaveAttribute("data-variant", "beam");
    expect(icon).toHaveAttribute("data-colors", AVATAR_PALETTE.join(","));
  });

  it("renders initials when no userId is present", () => {
    render(<UserAvatar name="Jane Doe" email="jane@example.com" />);

    expect(screen.queryByTestId("identicon")).not.toBeInTheDocument();
    expect(screen.getByText("JD")).toBeInTheDocument();
  });

  it("produces different markup for two different ids", () => {
    const { container: first } = render(<UserAvatar userId="aaaa" email="a@example.com" />);
    const { container: second } = render(<UserAvatar userId="bbbb" email="b@example.com" />);

    expect(first.innerHTML).not.toEqual(second.innerHTML);
  });
});
