import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { submitAlphaWaitlist } from "@/lib/api/generated/sdk.gen";

import { AlphaWaitlistForm } from "./alpha-waitlist-form";

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  submitAlphaWaitlist: vi.fn(async () => ({ data: { message: "ok" } })),
}));

const mockedSubmitAlphaWaitlist = vi.mocked(submitAlphaWaitlist);

describe("AlphaWaitlistForm", () => {
  beforeEach(() => {
    mockedSubmitAlphaWaitlist.mockResolvedValue({
      data: { message: "ok" },
      error: undefined,
      request: new Request("https://grundet.test/join-alpha"),
      response: new Response(),
    });
  });

  it("submits trimmed personal requests", async () => {
    const user = userEvent.setup();
    render(<AlphaWaitlistForm />);

    await user.type(screen.getByLabelText("Name*"), "  Alex  ");
    await user.type(screen.getByLabelText("Email*"), "alex@example.com");
    await user.type(
      screen.getByLabelText(/Where does product intent get lost/i),
      "  Testing Grundet  "
    );
    await user.click(screen.getByRole("button", { name: "Request alpha access" }));

    expect(mockedSubmitAlphaWaitlist).toHaveBeenCalledWith({
      body: {
        accountType: "Personal",
        name: "Alex",
        email: "alex@example.com",
        teamSize: null,
        lineOfWork: null,
        organizationName: null,
        organizationCountry: null,
        reason: "Testing Grundet",
      },
    });
    expect(await screen.findByText("Request received")).toBeInTheDocument();
  });

  it("shows business fields only for business accounts", async () => {
    const user = userEvent.setup();
    render(<AlphaWaitlistForm />);

    expect(screen.queryByLabelText(/Team size/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Organization name/i)).not.toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: "Business" }));

    expect(screen.getByLabelText(/Team size/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Line of work/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Organization name/i)).toBeRequired();
    expect(screen.getByLabelText(/Organization country/i)).toBeRequired();

    await user.click(screen.getByRole("radio", { name: "Personal" }));

    expect(screen.queryByLabelText(/Team size/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Organization name/i)).not.toBeInTheDocument();
  });
});
