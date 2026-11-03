import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OnboardingForm } from "./onboarding-form";

const updateMeMock = vi.fn();

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  updateMe: (...args: unknown[]) => updateMeMock(...args),
}));

async function submitName(name: string) {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Display name"), name);
  await user.click(screen.getByRole("button", { name: "Continue" }));
}

describe("OnboardingForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    updateMeMock.mockResolvedValue({ data: {}, error: undefined });
  });

  it("advances in place to the orientation step", async () => {
    const onContinue = vi.fn();
    render(<OnboardingForm onContinue={onContinue} />);

    expect(screen.getByRole("heading", { name: "What should we call you?" })).toBeInTheDocument();

    await submitName("Alex");

    expect(updateMeMock).toHaveBeenCalledWith({ body: { displayName: "Alex" } });
    expect(onContinue).toHaveBeenCalledWith("Alex");
  });

  it("shows a generic error and stays on the step when saving fails without detail", async () => {
    const onContinue = vi.fn();
    updateMeMock.mockResolvedValue({ data: undefined, error: { title: "boom" } });
    render(<OnboardingForm onContinue={onContinue} />);

    await submitName("Alex");

    expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    expect(onContinue).not.toHaveBeenCalled();
  });

  it("shows the ProblemDetails detail verbatim when the backend rejects the name", async () => {
    const onContinue = vi.fn();
    updateMeMock.mockResolvedValue({
      data: undefined,
      error: { title: "Bad Request", detail: "Display name cannot exceed 120 characters." },
    });
    render(<OnboardingForm onContinue={onContinue} />);

    await submitName("Alex");

    expect(screen.getByText("Display name cannot exceed 120 characters.")).toBeInTheDocument();
    expect(screen.queryByText(/Something went wrong/i)).not.toBeInTheDocument();
    expect(onContinue).not.toHaveBeenCalled();
  });
});
